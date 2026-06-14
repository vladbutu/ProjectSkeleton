// AI-generated
using Silk.NET.Maths;
using Silk.NET.SDL;

namespace TheAdventure;

public struct Particle
{
    public float X;
    public float Y;
    public float VelocityX;
    public float VelocityY;
    public float Life;
    public float MaxLife;
    public float Size;
    public byte Red;
    public byte Green;
    public byte Blue;
    public bool Gravity;
}

public sealed class ParticleSystem
{
    private readonly List<Particle> _particles = [];

    public int Count => _particles.Count;

    public void Spawn(
        float x, float y, int count,
        byte red, byte green, byte blue,
        float minSpeed, float maxSpeed,
        float minLife, float maxLife,
        float size, bool gravity)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = Random.Shared.NextDouble() * Math.Tau;
            float speed = Lerp(minSpeed, maxSpeed, (float)Random.Shared.NextDouble());

            _particles.Add(new Particle
            {
                X = x,
                Y = y,
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed,
                Life = Lerp(minLife, maxLife, (float)Random.Shared.NextDouble()),
                MaxLife = maxLife,
                Size = size,
                Red = red,
                Green = green,
                Blue = blue,
                Gravity = gravity,
            });
        }
    }

    public void SpawnConfetti(int width, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var (r, g, b) = ColorUtilities.FromHue((float)Random.Shared.NextDouble());

            _particles.Add(new Particle
            {
                X = Random.Shared.Next(0, Math.Max(1, width)),
                Y = -Random.Shared.Next(0, 200),
                VelocityX = Lerp(-40f, 40f, (float)Random.Shared.NextDouble()),
                VelocityY = Lerp(80f, 220f, (float)Random.Shared.NextDouble()),
                Life = Lerp(2.5f, 4.5f, (float)Random.Shared.NextDouble()),
                MaxLife = 4.5f,
                Size = Random.Shared.Next(4, 9),
                Red = r,
                Green = g,
                Blue = b,
                Gravity = false,
            });
        }
    }

    public void Update(double deltaSeconds)
    {
        float dt = (float)deltaSeconds;

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];

            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            if (p.Gravity)
            {
                p.VelocityY += 600f * dt;
            }

            p.X += p.VelocityX * dt;
            p.Y += p.VelocityY * dt;

            _particles[i] = p;
        }
    }

    public void Clear() => _particles.Clear();

    public unsafe void Render(Sdl sdl, Renderer* renderer, float scaleX, float scaleY, int offsetX, int offsetY)
    {
        foreach (var p in _particles)
        {
            byte alpha = (byte)Math.Clamp(p.Life / p.MaxLife * 255f, 0f, 255f);
            int s = Math.Max(1, (int)(p.Size * scaleX));

            var rect = new Rectangle<int>(
                new Vector2D<int>((int)(p.X * scaleX) + offsetX, (int)(p.Y * scaleY) + offsetY),
                new Vector2D<int>(s, s));

            sdl.SetRenderDrawColor(renderer, p.Red, p.Green, p.Blue, alpha);
            sdl.RenderFillRect(renderer, &rect);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

public sealed class ScreenShake
{
    private float _magnitude;

    public void Add(float magnitude) => _magnitude = Math.Max(_magnitude, magnitude);

    public void Update(double deltaSeconds)
    {
        _magnitude = Math.Max(0f, _magnitude - (float)deltaSeconds * 60f);
    }

    public (int X, int Y) Offset()
    {
        if (_magnitude <= 0.5f)
        {
            return (0, 0);
        }

        int range = (int)_magnitude;
        return (Random.Shared.Next(-range, range + 1), Random.Shared.Next(-range, range + 1));
    }
}

public sealed class ScreenFlash
{
    private float _intensity;
    private byte _red;
    private byte _green;
    private byte _blue;

    public void Trigger(byte red, byte green, byte blue, float intensity)
    {
        _red = red;
        _green = green;
        _blue = blue;
        _intensity = Math.Clamp(intensity, 0f, 1f);
    }

    public void Update(double deltaSeconds)
    {
        _intensity = Math.Max(0f, _intensity - (float)deltaSeconds * 1.6f);
    }

    public unsafe void Render(Sdl sdl, Renderer* renderer, int width, int height)
    {
        if (_intensity <= 0.01f)
        {
            return;
        }

        byte alpha = (byte)Math.Clamp(_intensity * 200f, 0f, 200f);
        var rect = new Rectangle<int>(new Vector2D<int>(0, 0), new Vector2D<int>(width, height));

        sdl.SetRenderDrawColor(renderer, _red, _green, _blue, alpha);
        sdl.RenderFillRect(renderer, &rect);
    }
}

public static class ColorUtilities
{
    public static (byte R, byte G, byte B) FromHue(float hue)
    {
        hue = hue - (float)Math.Floor(hue);
        float h = hue * 6f;
        float x = 1f - Math.Abs(h % 2f - 1f);

        (float r, float g, float b) = (int)h switch
        {
            0 => (1f, x, 0f),
            1 => (x, 1f, 0f),
            2 => (0f, 1f, x),
            3 => (0f, x, 1f),
            4 => (x, 0f, 1f),
            _ => (1f, 0f, x),
        };

        return ((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f));
    }
}
// end AI-generated
