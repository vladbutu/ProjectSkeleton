namespace TheAdventure;

public readonly record struct GridPosition(int X, int Y)
{
    public static GridPosition operator +(GridPosition left, GridPosition right) =>
        new(left.X + right.X, left.Y + right.Y);
}

public readonly record struct Direction(int X, int Y)
{
    public static readonly Direction None = new(0, 0);
    public static readonly Direction Up = new(0, -1);
    public static readonly Direction Down = new(0, 1);
    public static readonly Direction Left = new(-1, 0);
    public static readonly Direction Right = new(1, 0);

    public bool IsOpposite(Direction other) => X == -other.X && Y == -other.Y;
}

public sealed class Snake
{
    private readonly List<GridPosition> _body = [];

    // AI-generated
    private readonly HashSet<GridPosition> _occupied = [];
    // end AI-generated

    public Snake(GridPosition start, int initialLength, Direction facing)
    {
        Facing = facing;
        for (int i = 0; i < initialLength; i++)
        {
            var cell = new GridPosition(start.X - i * facing.X, start.Y - i * facing.Y);
            _body.Add(cell);
            _occupied.Add(cell);
        }
    }

    public Direction Facing { get; set; }

    public IReadOnlyList<GridPosition> Body => _body;

    public GridPosition Head => _body[0];

    public int Length => _body.Count;

    public bool Occupies(GridPosition position) => _occupied.Contains(position);

    public void Advance(GridPosition newHead, bool grow)
    {
        _body.Insert(0, newHead);
        _occupied.Add(newHead);
        if (!grow)
        {
            var tail = _body[^1];
            _body.RemoveAt(_body.Count - 1);
            _occupied.Remove(tail);
        }
    }

    // AI-generated
    public bool WouldHitSelf(GridPosition newHead, bool grow)
    {
        if (!grow && newHead == _body[^1])
        {
            return false;
        }

        return _occupied.Contains(newHead);
    }
    // end AI-generated
}
