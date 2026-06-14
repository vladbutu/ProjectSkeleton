using Silk.NET.Maths;
using Silk.NET.SDL;

namespace TheAdventure;

public enum GameStatus
{
    Ready,
    Running,
    Lost,
    Won,
}

public sealed class AdventureGame
{
    private readonly int _boardWidth;
    private readonly int _boardHeight;
    private readonly ISaveStore<SnakeSaveData> _saveStore;

    private Snake _snake;
    private GridPosition _food;
    private Direction _pendingDirection;

    private TimeSpan _moveTimer;

    private GridPosition? _bonusFood;
    private double _bonusTimeLeft;

    private readonly List<GridPosition> _obstacles = [];
    private readonly HashSet<GridPosition> _obstacleSet = [];

    private bool _wrapEdges = true;

    private int _comboCount;
    private double _comboTimeLeft;
    private int _normalFoodEaten;

    private readonly ParticleSystem _particles = new();
    private readonly ScreenShake _shake = new();
    private readonly ScreenFlash _flash = new();
    private double _animationTime;
    private bool _celebrated;
    private bool _endedWithNewHighScore;

    private const int InitialLength = 4;
    private const double BaseStepSeconds = 0.14;
    private const double MinStepSeconds = 0.06;

    private const double BonusLifetime = 6.0;
    private const double BonusSpawnChance = 0.35;
    private const int BonusMinValue = 2;
    private const int BonusMaxValue = 8;

    private const int ObstacleCount = 10;
    private const double ComboWindow = 2.5;
    private const int ComboMaxMultiplier = 5;
    private const int ObstacleMovesMin = 2;
    private const int ObstacleMovesMax = 3;
    private const int MinObstacleMoveDistance = 4;
    public AdventureGame(int boardWidth, int boardHeight, ISaveStore<SnakeSaveData> saveStore)
    {
        _boardWidth = boardWidth;
        _boardHeight = boardHeight;
        _saveStore = saveStore;

        _snake = CreateSnake();
        _pendingDirection = _snake.Facing;
        _food = PickFoodCell();
    }

    public GameStatus Status { get; private set; } = GameStatus.Ready;

    public int Score { get; private set; }

    public int HighScore { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var loaded = await _saveStore.LoadAsync(cancellationToken).ConfigureAwait(false)
            ?? new SnakeSaveData(HighScore: 0);

        HighScore = Math.Max(0, loaded.HighScore);
    }

    public void HandleKeyDown(KeyCode key)
    {
        var requested = key switch
        {
            KeyCode.Up or KeyCode.W => Direction.Up,
            KeyCode.Down or KeyCode.S => Direction.Down,
            KeyCode.Left or KeyCode.A => Direction.Left,
            KeyCode.Right or KeyCode.D => Direction.Right,
            _ => Direction.None,
        };

        if (requested == Direction.None)
        {
            if ((Status == GameStatus.Ready || Status == GameStatus.Lost || Status == GameStatus.Won)
                && (key == KeyCode.Return || key == KeyCode.Space))
            {
                StartRun();
            }

            return;
        }

        if (Status == GameStatus.Ready)
        {
            StartRun();
        }

        if (Status != GameStatus.Running)
        {
            return;
        }

        if (!requested.IsOpposite(_snake.Facing))
        {
            _pendingDirection = requested;
        }
    }

    public void RequestRestart()
    {
        if (Status == GameStatus.Lost || Status == GameStatus.Ready || Status == GameStatus.Won)
        {
            StartRun();
        }
    }

    public void Update(TimeSpan elapsed, ReadOnlySpan<byte> keyboardState)
    {
        double dt = elapsed.TotalSeconds;
        _animationTime += dt;
        _particles.Update(dt);
        _shake.Update(dt);
        _flash.Update(dt);

        if (Status == GameStatus.Won && !_celebrated)
        {
            _particles.SpawnConfetti(860, 220);
            _celebrated = true;
        }

        if (Status != GameStatus.Running)
        {
            return;
        }

        if (_bonusFood is not null)
        {
            _bonusTimeLeft -= dt;
            if (_bonusTimeLeft <= 0)
            {
                _bonusFood = null;
            }
        }

        if (_comboTimeLeft > 0)
        {
            _comboTimeLeft -= dt;
            if (_comboTimeLeft <= 0)
            {
                _comboCount = 0;
            }
        }

        _moveTimer += elapsed;
        var step = TimeSpan.FromSeconds(CurrentStepSeconds());

        while (_moveTimer >= step)
        {
            _moveTimer -= step;
            Step();
            if (Status != GameStatus.Running)
            {
                break;
            }
        }
    }

    private double CurrentStepSeconds()
    {
        double speed = BaseStepSeconds - _normalFoodEaten * 0.0025;
        return Math.Max(MinStepSeconds, speed);
    }

    private void Step()
    {
        _snake.Facing = _pendingDirection;
        var newHead = _snake.Head + new GridPosition(_snake.Facing.X, _snake.Facing.Y);

        if (_wrapEdges)
        {
            newHead = new GridPosition(
                ((newHead.X % _boardWidth) + _boardWidth) % _boardWidth,
                ((newHead.Y % _boardHeight) + _boardHeight) % _boardHeight);
        }
        else if (newHead.X < 0 || newHead.X >= _boardWidth
            || newHead.Y < 0 || newHead.Y >= _boardHeight)
        {
            EndRun();
            return;
        }

        if (_obstacleSet.Contains(newHead))
        {
            EndRun();
            return;
        }

        bool grow = newHead == _food;

        if (_snake.WouldHitSelf(newHead, grow))
        {
            EndRun();
            return;
        }

        _snake.Advance(newHead, grow);

        if (_bonusFood == newHead)
        {
            int value = BonusValue() * RegisterCombo();
            Score += value;
            SpawnBonusBurst(_bonusFood.Value);
            _shake.Add(8f);
            _flash.Trigger(255, 215, 90, 0.35f);
            _bonusFood = null;
        }

        if (grow)
        {
            _normalFoodEaten++;
            Score += RegisterCombo();

            SpawnFoodBurst(_food);
            _shake.Add(5f);

            if (_snake.Length >= _boardWidth * _boardHeight)
            {
                WinRun();
                return;
            }

            _food = PickFoodCell();
            TrySpawnBonus();
            MoveObstaclesAfterFood();
        }
    }

    private int RegisterCombo()
    {
        _comboCount = _comboTimeLeft > 0 ? _comboCount + 1 : 1;
        _comboTimeLeft = ComboWindow;
        return Math.Min(_comboCount, ComboMaxMultiplier);
    }

    private int BonusValue()
    {
        double fraction = Math.Clamp(_bonusTimeLeft / BonusLifetime, 0.0, 1.0);
        return BonusMinValue + (int)Math.Round((BonusMaxValue - BonusMinValue) * fraction);
    }

    private void TrySpawnBonus()
    {
        if (_bonusFood is not null || Random.Shared.NextDouble() > BonusSpawnChance)
        {
            return;
        }

        var cell = PickBonusCell();
        if (cell is not null)
        {
            _bonusFood = cell;
            _bonusTimeLeft = BonusLifetime;
        }
    }

    private GridPosition? PickBonusCell()
    {
        var options = (from y in Enumerable.Range(0, _boardHeight)
                       from x in Enumerable.Range(0, _boardWidth)
                       let pos = new GridPosition(x, y)
                       where !_snake.Occupies(pos) && pos != _food && !_obstacleSet.Contains(pos)
                       select pos).ToArray();

        if (options.Length == 0)
        {
            return null;
        }

        return options[Random.Shared.Next(options.Length)];
    }

    private void StartRun()
    {
        _snake = CreateSnake();
        _pendingDirection = _snake.Facing;
        GenerateObstacles();
        _food = PickFoodCell();
        _bonusFood = null;
        _bonusTimeLeft = 0;
        _comboCount = 0;
        _comboTimeLeft = 0;
        _normalFoodEaten = 0;
        _moveTimer = TimeSpan.Zero;
        Score = 0;
        Status = GameStatus.Running;
        _endedWithNewHighScore = false;

        _particles.Clear();
        _celebrated = false;
    }

    private void GenerateObstacles()
    {
        _obstacles.Clear();
        _obstacleSet.Clear();

        int startRow = _boardHeight / 2;
        int attempts = 0;

        while (_obstacles.Count < ObstacleCount && attempts < 200)
        {
            attempts++;

            bool horizontal = Random.Shared.Next(2) == 0;
            int length = Random.Shared.Next(2, 4);
            int originX = Random.Shared.Next(0, _boardWidth);
            int originY = Random.Shared.Next(0, _boardHeight);

            var segment = new List<GridPosition>();
            bool valid = true;

            for (int i = 0; i < length; i++)
            {
                var cell = horizontal
                    ? new GridPosition(originX + i, originY)
                    : new GridPosition(originX, originY + i);

                bool inBounds = cell.X >= 0 && cell.X < _boardWidth && cell.Y >= 0 && cell.Y < _boardHeight;
                bool clearOfSnake = !_snake.Occupies(cell) && Math.Abs(cell.Y - startRow) > 1;

                if (!inBounds || !clearOfSnake || _obstacleSet.Contains(cell))
                {
                    valid = false;
                    break;
                }

                segment.Add(cell);
            }

            if (!valid)
            {
                continue;
            }

            foreach (var cell in segment)
            {
                _obstacles.Add(cell);
                _obstacleSet.Add(cell);
            }
        }
    }

    private void MoveObstaclesAfterFood()
    {
        if (_obstacles.Count == 0)
        {
            return;
        }

        int moves = Random.Shared.Next(ObstacleMovesMin, ObstacleMovesMax + 1);

        for (int i = 0; i < moves; i++)
        {
            if (_obstacles.Count == 0)
            {
                break;
            }

            var movableIndices = Enumerable.Range(0, _obstacles.Count)
                .Where(index => !WouldLeaveIsolatedObstacle(_obstacles[index]))
                .ToArray();
            if (movableIndices.Length == 0)
            {
                break;
            }

            int obstacleIndex = movableIndices[Random.Shared.Next(movableIndices.Length)];
            var oldPos = _obstacles[obstacleIndex];

            var newPos = PickFairObstacleCell(oldPos);
            if (newPos is null)
            {
                continue;
            }
            _obstacles[obstacleIndex] = newPos.Value;
            _obstacleSet.Remove(oldPos);
            _obstacleSet.Add(newPos.Value);
        }
    }

    private GridPosition? PickFairObstacleCell(GridPosition movingObstacle)
    {
        var head = _snake.Head;

        var options = (from y in Enumerable.Range(0, _boardHeight)
                       from x in Enumerable.Range(0, _boardWidth)
                       let pos = new GridPosition(x, y)
                       where !_snake.Occupies(pos)
                       && pos != _food
                       && _bonusFood != pos
                       && !_obstacleSet.Contains(pos)
                       && Math.Abs(pos.X - head.X) + Math.Abs(pos.Y - head.Y) >= 3
                       && ManhattanDistance(pos, movingObstacle) >= MinObstacleMoveDistance
                           && HasAdjacentObstacle(pos, movingObstacle)
                       select pos).ToArray();

        if (options.Length == 0)
        {
            return null;
        }

        var ranked = options
            .OrderByDescending(pos => ManhattanDistance(pos, movingObstacle))
            .ToArray();

        int farPoolSize = Math.Max(1, ranked.Length / 3);
        return ranked[Random.Shared.Next(farPoolSize)];
    }

    private static int ManhattanDistance(GridPosition a, GridPosition b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private bool WouldLeaveIsolatedObstacle(GridPosition movingObstacle)
    {
        var neighbors = GetOrthogonalNeighbors(movingObstacle);
        foreach (var neighbor in neighbors)
        {
            if (!_obstacleSet.Contains(neighbor))
            {
                continue;
            }

            if (CountAdjacentObstacles(neighbor, movingObstacle) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private int CountAdjacentObstacles(GridPosition position, GridPosition exclude)
    {
        int count = 0;
        foreach (var neighbor in GetOrthogonalNeighbors(position))
        {
            if (neighbor == exclude)
            {
                continue;
            }

            if (_obstacleSet.Contains(neighbor))
            {
                count++;
            }
        }

        return count;
    }

    private GridPosition[] GetOrthogonalNeighbors(GridPosition position)
    {
        return
        [
            new GridPosition(position.X + 1, position.Y),
            new GridPosition(position.X - 1, position.Y),
            new GridPosition(position.X, position.Y + 1),
            new GridPosition(position.X, position.Y - 1),
        ];
    }

    private bool HasAdjacentObstacle(GridPosition position, GridPosition movingObstacle)
    {
        var neighbors = GetOrthogonalNeighbors(position);

        foreach (var neighbor in neighbors)
        {
            bool inBounds = neighbor.X >= 0 && neighbor.X < _boardWidth
                && neighbor.Y >= 0 && neighbor.Y < _boardHeight;
            if (!inBounds)
            {
                continue;
            }

            if (_obstacleSet.Contains(neighbor) && neighbor != movingObstacle)
            {
                return true;
            }
        }

        return false;
    }



    private void EndRun()
    {
        Status = GameStatus.Lost;
        _endedWithNewHighScore = Score > HighScore;

        var headPixel = CellCenter(_snake.Head);
        _particles.Spawn(headPixel.X, headPixel.Y, 60, 235, 80, 80, 60f, 320f, 0.4f, 1.1f, 6f, gravity: true);
        _shake.Add(22f);
        _flash.Trigger(235, 60, 60, 0.8f);

        if (Score > HighScore)
        {
            HighScore = Score;
            PersistProgress();
        }
    }

    private void WinRun()
    {
        Status = GameStatus.Won;
        _endedWithNewHighScore = Score > HighScore;

        _flash.Trigger(120, 240, 150, 0.7f);

        if (Score > HighScore)
        {
            HighScore = Score;
            PersistProgress();
        }
    }

    private void SpawnFoodBurst(GridPosition cell)
    {
        var center = CellCenter(cell);
        _particles.Spawn(center.X, center.Y, 24, 255, 200, 90, 40f, 220f, 0.25f, 0.6f, 5f, gravity: false);
    }

    private void SpawnBonusBurst(GridPosition cell)
    {
        var center = CellCenter(cell);
        _particles.Spawn(center.X, center.Y, 40, 255, 215, 90, 60f, 280f, 0.3f, 0.8f, 6f, gravity: false);
    }

    private (float X, float Y) CellCenter(GridPosition cell)
    {
        float cellWidth = 860f / _boardWidth;
        float cellHeight = 860f / _boardHeight;
        return (cell.X * cellWidth + cellWidth / 2f, cell.Y * cellHeight + cellHeight / 2f);
    }

    private Snake CreateSnake()
    {
        var start = new GridPosition(_boardWidth / 2, _boardHeight / 2);
        return new Snake(start, InitialLength, Direction.Right);
    }

    private GridPosition PickFoodCell()
    {
        var freeCells = from y in Enumerable.Range(0, _boardHeight)
                        from x in Enumerable.Range(0, _boardWidth)
                        let pos = new GridPosition(x, y)
                        where !_snake.Occupies(pos) && !_obstacleSet.Contains(pos)
                        select pos;

        var options = freeCells.ToArray();
        if (options.Length == 0)
        {
            return _snake.Head;
        }

        return options[Random.Shared.Next(options.Length)];
    }

    private void PersistProgress()
    {
        _saveStore.SaveAsync(
            new SnakeSaveData(HighScore),
            CancellationToken.None).GetAwaiter().GetResult();
    }

    public string BuildWindowTitle()
    {
        return Status switch
        {
            GameStatus.Ready =>
                $"Snake | High Score {HighScore} | Arrows/WASD or Enter to start | Wrap ON",
            GameStatus.Running =>
                $"Snake | Score {Score} | Length {_snake.Length} | High {HighScore}{ComboTitleSuffix()}{BonusTitleSuffix()} | Wrap ON",
            GameStatus.Lost =>
                _endedWithNewHighScore
                    ? $"Snake | Game Over! Score {Score} | New High Score {HighScore} | Press R or Enter to retry"
                    : $"Snake | Game Over! Score {Score} | High Score {HighScore} | Press R or Enter to retry",
            GameStatus.Won =>
                $"Snake | You Win! Score {Score} | High {HighScore} | Press R or Enter to play again",
            _ => "Snake",
        };
    }

    private string BonusTitleSuffix()
    {
        return _bonusFood is null
            ? string.Empty
            : $" | BONUS +{BonusValue()} ({_bonusTimeLeft:0.0}s)";
    }

    private string ComboTitleSuffix()
    {
        int multiplier = Math.Min(_comboCount, ComboMaxMultiplier);
        return multiplier > 1 ? $" | Combo x{multiplier}" : string.Empty;
    }

    public unsafe void Render(Sdl sdl, Renderer* renderer, int viewportWidth, int viewportHeight)
    {
        sdl.SetRenderDrawBlendMode(renderer, BlendMode.Blend);

        sdl.SetRenderDrawColor(renderer, 14, 18, 24, 255);
        sdl.RenderClear(renderer);

        var cellWidth = Math.Max(1, viewportWidth / _boardWidth);
        var cellHeight = Math.Max(1, viewportHeight / _boardHeight);

        var (shakeX, shakeY) = _shake.Offset();
        float scaleX = viewportWidth / 860f;
        float scaleY = viewportHeight / 860f;

        DrawGrid(sdl, renderer, viewportWidth, viewportHeight, cellWidth, cellHeight, shakeX, shakeY);

        foreach (var wall in _obstacles)
        {
            DrawCell(sdl, renderer, wall, cellWidth, cellHeight, 90, 100, 120, shakeX, shakeY);
        }

        float pulse = 0.5f + 0.5f * (float)Math.Sin(_animationTime * 6.0);
        byte foodGreen = (byte)(70 + pulse * 60);
        DrawCellScaled(sdl, renderer, _food, cellWidth, cellHeight, 0.65f + pulse * 0.35f,
            235, foodGreen, 80, shakeX, shakeY);

        if (_bonusFood is GridPosition bonus)
        {
            bool nearExpiry = _bonusTimeLeft < 2.0;
            bool blinkOn = !nearExpiry || Math.Sin(_animationTime * 18.0) > 0;
            if (blinkOn)
            {
                float bonusPulse = 0.5f + 0.5f * (float)Math.Sin(_animationTime * 9.0);
                DrawCellScaled(sdl, renderer, bonus, cellWidth, cellHeight, 0.7f + bonusPulse * 0.45f,
                    255, 215, 90, shakeX, shakeY);
            }
        }

        var body = _snake.Body;
        for (int i = body.Count - 1; i >= 0; i--)
        {
            if (i == 0)
            {
                DrawCell(sdl, renderer, body[i], cellWidth, cellHeight, 120, 240, 150, shakeX, shakeY);
            }
            else
            {
                float hue = (float)(_animationTime * 0.25 - i * 0.025);
                var (sr, sg, sb) = ColorUtilities.FromHue(hue);
                byte fade = (byte)Math.Clamp(200 - i * 4, 90, 200);
                byte red = (byte)((sr * 0.35f) + 50);
                byte green = (byte)((sg * 0.35f) + fade * 0.6f);
                byte blue = (byte)((sb * 0.35f) + 90);
                DrawCell(sdl, renderer, body[i], cellWidth, cellHeight, red, green, blue, shakeX, shakeY);
            }
        }

        _particles.Render(sdl, renderer, scaleX, scaleY, shakeX, shakeY);
        _flash.Render(sdl, renderer, viewportWidth, viewportHeight);

        if (Status == GameStatus.Lost)
        {
            DrawGameOverOverlay(sdl, renderer, viewportWidth, viewportHeight);
        }

        DrawScoreBar(sdl, renderer, viewportWidth);
    }

    private unsafe static void DrawGrid(Sdl sdl, Renderer* renderer, int width, int height, int cellWidth, int cellHeight,
        int offsetX, int offsetY)
    {
        sdl.SetRenderDrawColor(renderer, 26, 32, 44, 255);

        for (int x = 0; x <= width; x += cellWidth)
        {
            sdl.RenderDrawLine(renderer, x + offsetX, offsetY, x + offsetX, height + offsetY);
        }

        for (int y = 0; y <= height; y += cellHeight)
        {
            sdl.RenderDrawLine(renderer, offsetX, y + offsetY, width + offsetX, y + offsetY);
        }
    }

    private unsafe static void DrawCell(Sdl sdl, Renderer* renderer, GridPosition position, int cellWidth, int cellHeight,
        byte red, byte green, byte blue, int offsetX, int offsetY)
    {
        var rect = new Rectangle<int>(
            new Vector2D<int>(position.X * cellWidth + 1 + offsetX, position.Y * cellHeight + 1 + offsetY),
            new Vector2D<int>(Math.Max(1, cellWidth - 2), Math.Max(1, cellHeight - 2)));

        sdl.SetRenderDrawColor(renderer, red, green, blue, 255);
        sdl.RenderFillRect(renderer, &rect);
    }

    private unsafe static void DrawCellScaled(Sdl sdl, Renderer* renderer, GridPosition position,
        int cellWidth, int cellHeight, float scale, byte red, byte green, byte blue, int offsetX, int offsetY)
    {
        int w = Math.Max(1, (int)((cellWidth - 2) * scale));
        int h = Math.Max(1, (int)((cellHeight - 2) * scale));
        int x = position.X * cellWidth + (cellWidth - w) / 2 + offsetX;
        int y = position.Y * cellHeight + (cellHeight - h) / 2 + offsetY;

        var rect = new Rectangle<int>(new Vector2D<int>(x, y), new Vector2D<int>(w, h));

        sdl.SetRenderDrawColor(renderer, red, green, blue, 255);
        sdl.RenderFillRect(renderer, &rect);
    }

    private unsafe void DrawScoreBar(Sdl sdl, Renderer* renderer, int width)
    {
        int reference = Math.Max(1, Math.Max(HighScore, 1));
        int scoreWidth = Math.Clamp((int)(Score / (double)reference * width), 0, width);

        var scoreRect = new Rectangle<int>(new Vector2D<int>(0, 0), new Vector2D<int>(scoreWidth, 6));

        sdl.SetRenderDrawColor(renderer, 120, 240, 150, 255);
        sdl.RenderFillRect(renderer, &scoreRect);
    }

    private unsafe void DrawGameOverOverlay(Sdl sdl, Renderer* renderer, int viewportWidth, int viewportHeight)
    {
        int panelWidth = Math.Min(viewportWidth - 80, 620);
        int panelHeight = 220;
        int panelX = (viewportWidth - panelWidth) / 2;
        int panelY = (viewportHeight - panelHeight) / 2;

        var panel = new Rectangle<int>(new Vector2D<int>(panelX, panelY), new Vector2D<int>(panelWidth, panelHeight));

        sdl.SetRenderDrawColor(renderer, 8, 10, 14, 210);
        sdl.RenderFillRect(renderer, &panel);

        DrawCenteredText(sdl, renderer, "GAME OVER", panelX, panelY + 18, panelWidth, 4, 235, 80, 80);
        DrawCenteredText(sdl, renderer, $"SCORE {Score}", panelX, panelY + 90, panelWidth, 4, 220, 225, 230);
        DrawCenteredText(sdl, renderer, $"HIGH SCORE {HighScore}", panelX, panelY + 138, panelWidth, 4, 255, 215, 90);

        if (_endedWithNewHighScore)
        {
            DrawCenteredText(sdl, renderer, "NEW BEST!", panelX, panelY + 184, panelWidth, 3, 120, 240, 150);
        }
    }

    private unsafe void DrawCenteredText(Sdl sdl, Renderer* renderer, string text, int x, int y, int width, int scale,
        byte red, byte green, byte blue)
    {
        int textWidth = MeasureTextWidth(text, scale);
        int startX = x + Math.Max(0, (width - textWidth) / 2);
        DrawText(sdl, renderer, text, startX, y, scale, red, green, blue);
    }

    private unsafe void DrawText(Sdl sdl, Renderer* renderer, string text, int x, int y, int scale,
        byte red, byte green, byte blue)
    {
        int cursorX = x;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == ' ')
            {
                cursorX += scale * 4;
                continue;
            }

            var glyph = GetGlyph(c);
            for (int row = 0; row < glyph.Length; row++)
            {
                string line = glyph[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] != '#')
                    {
                        continue;
                    }

                    var rect = new Rectangle<int>(
                        new Vector2D<int>(cursorX + col * scale, y + row * scale),
                        new Vector2D<int>(scale, scale));

                    sdl.SetRenderDrawColor(renderer, red, green, blue, 255);
                    sdl.RenderFillRect(renderer, &rect);
                }
            }

            cursorX += scale * 6;
        }
    }

    private static int MeasureTextWidth(string text, int scale)
    {
        int width = 0;
        foreach (char raw in text)
        {
            width += raw == ' ' ? scale * 4 : scale * 6;
        }

        return width;
    }

    private static string[] GetGlyph(char c)
    {
        return c switch
        {
            'A' => [" ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
            'B' => ["#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### "],
            'C' => [" ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### "],
            'D' => ["#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### "],
            'E' => ["#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####"],
            'G' => [" ### ", "#   #", "#    ", "#  ##", "#   #", "#   #", " ### "],
            'H' => ["#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
            'I' => ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####"],
            'M' => ["#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #"],
            'N' => ["#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #"],
            'O' => [" ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
            'R' => ["#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #"],
            'S' => [" ####", "#    ", "#    ", " ### ", "    #", "    #", "#### "],
            'T' => ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  "],
            'V' => ["#   #", "#   #", "#   #", "#   #", " # # ", " # # ", "  #  "],
            'W' => ["#   #", "#   #", "#   #", "# # #", "## ##", "#   #", "#   #"],
            'Y' => ["#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  "],
            '0' => [" ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### "],
            '1' => ["  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####"],
            '2' => [" ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####"],
            '3' => ["#### ", "    #", "    #", " ### ", "    #", "    #", "#### "],
            '4' => ["#   #", "#   #", "#   #", "#####", "    #", "    #", "    #"],
            '5' => ["#####", "#    ", "#    ", "#### ", "    #", "    #", "#### "],
            '6' => [" ### ", "#    ", "#    ", "#### ", "#   #", "#   #", " ### "],
            '7' => ["#####", "    #", "   # ", "  #  ", "  #  ", "  #  ", "  #  "],
            '8' => [" ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### "],
            '9' => [" ### ", "#   #", "#   #", " ####", "    #", "    #", " ### "],
            '!' => ["  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  "],
            _ => ["#####", "#   #", "#   #", "#   #", "#   #", "#   #", "#####"],
        };
    }
}

