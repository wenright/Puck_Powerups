using System;
using System.Collections.Generic;
using UnityEngine;

namespace Powerups;

public static class PowerupPalette
{
    public static Color Get(string powerupName)
    {
        return powerupName switch
        {
            PowerupNames.Magnet => new Color32(255, 70, 220, 255),
            PowerupNames.Rage => new Color32(174, 78, 255, 255),
            PowerupNames.Grapple => new Color32(48, 220, 220, 255),
            PowerupNames.Glue => new Color32(255, 210, 55, 255),
            PowerupNames.Kick => new Color32(95, 205, 255, 255),
            PowerupNames.Tornado => new Color32(160, 214, 72, 255),
            PowerupNames.Jetpack => new Color32(40, 235, 255, 255),
            PowerupNames.LowGrav => new Color32(80, 230, 125, 255),
            PowerupNames.Turbo => new Color32(255, 135, 35, 255),
            PowerupNames.Backflip => new Color32(230, 235, 245, 255),
            PowerupNames.Slowmo => new Color32(210, 145, 70, 255),
            _ => new Color32(180, 190, 205, 255),
        };
    }
}

public static class PowerupIconFactory
{
    private const int Size = 64;
    private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

    public static Texture2D Get(string powerupName)
    {
        string key = powerupName ?? string.Empty;
        if (Cache.TryGetValue(key, out Texture2D texture) && texture) return texture;

        IconCanvas canvas = new IconCanvas(Size, PowerupPalette.Get(key));
        canvas.FillCircle(32, 32, 29, new Color32(8, 12, 20, 225));
        canvas.Circle(32, 32, 28, 2);

        switch (key)
        {
            case PowerupNames.Magnet:
                DrawMagnet(canvas);
                break;
            case PowerupNames.Rage:
                DrawLightning(canvas);
                break;
            case PowerupNames.Grapple:
                DrawHook(canvas);
                break;
            case PowerupNames.Glue:
                DrawDrop(canvas);
                break;
            case PowerupNames.Kick:
                DrawBoot(canvas);
                break;
            case PowerupNames.Tornado:
                DrawTornado(canvas);
                break;
            case PowerupNames.Jetpack:
                DrawRocket(canvas);
                break;
            case PowerupNames.LowGrav:
                DrawLowGravity(canvas);
                break;
            case PowerupNames.Turbo:
                DrawTurbo(canvas);
                break;
            case PowerupNames.Backflip:
                DrawBackflip(canvas);
                break;
            case PowerupNames.Slowmo:
                DrawClock(canvas);
                break;
            default:
                DrawUnknown(canvas);
                break;
        }

        texture = canvas.ToTexture($"Powerup Icon - {(string.IsNullOrEmpty(key) ? "Unknown" : key)}");
        Cache[key] = texture;
        return texture;
    }

    private static void DrawMagnet(IconCanvas c)
    {
        c.Line(20, 17, 20, 37, 6);
        c.Line(44, 17, 44, 37, 6);
        for (int radius = 12; radius <= 17; radius++) c.Arc(32, 36, radius, 180, 360, 2);
        c.Rect(16, 14, 24, 21);
        c.Rect(40, 14, 48, 21);
    }

    private static void DrawLightning(IconCanvas c)
    {
        c.Polygon(new[] { new Vector2Int(34, 10), new Vector2Int(18, 35), new Vector2Int(29, 35), new Vector2Int(25, 54), new Vector2Int(47, 27), new Vector2Int(36, 27) });
    }

    private static void DrawHook(IconCanvas c)
    {
        c.Line(39, 12, 39, 37, 5);
        c.Arc(31, 37, 10, 180, 365, 5);
        c.Line(39, 12, 47, 18, 4);
        c.Line(47, 18, 43, 23, 4);
    }

    private static void DrawDrop(IconCanvas c)
    {
        c.Polygon(new[] { new Vector2Int(32, 10), new Vector2Int(18, 34), new Vector2Int(18, 43), new Vector2Int(24, 51), new Vector2Int(32, 54), new Vector2Int(40, 51), new Vector2Int(46, 43), new Vector2Int(46, 34) });
        c.FillCircle(28, 39, 4, new Color32(8, 12, 20, 210));
    }

    private static void DrawBoot(IconCanvas c)
    {
        c.Polygon(new[] { new Vector2Int(18, 13), new Vector2Int(36, 13), new Vector2Int(34, 34), new Vector2Int(49, 41), new Vector2Int(48, 50), new Vector2Int(19, 50), new Vector2Int(14, 43), new Vector2Int(21, 35) });
        c.Line(17, 43, 48, 43, 3, new Color32(8, 12, 20, 220));
    }

    private static void DrawTornado(IconCanvas c)
    {
        c.Line(12, 17, 52, 17, 4);
        c.Line(17, 25, 47, 25, 4);
        c.Line(21, 33, 43, 33, 4);
        c.Line(25, 41, 39, 41, 4);
        c.Line(29, 49, 35, 49, 4);
    }

    private static void DrawRocket(IconCanvas c)
    {
        c.Polygon(new[] { new Vector2Int(32, 8), new Vector2Int(21, 22), new Vector2Int(22, 43), new Vector2Int(32, 49), new Vector2Int(42, 43), new Vector2Int(43, 22) });
        c.FillCircle(32, 26, 5, new Color32(8, 12, 20, 230));
        c.Line(26, 49, 23, 57, 4);
        c.Line(32, 50, 32, 59, 4);
        c.Line(38, 49, 41, 57, 4);
    }

    private static void DrawLowGravity(IconCanvas c)
    {
        c.Line(32, 52, 32, 14, 5);
        c.Line(32, 14, 22, 25, 5);
        c.Line(32, 14, 42, 25, 5);
        c.Arc(21, 44, 8, 210, 510, 3);
        c.Arc(43, 44, 8, 30, 330, 3);
    }

    private static void DrawTurbo(IconCanvas c)
    {
        c.Line(10, 20, 31, 20, 4);
        c.Line(15, 31, 35, 31, 4);
        c.Line(10, 42, 31, 42, 4);
        c.Polygon(new[] { new Vector2Int(29, 12), new Vector2Int(53, 31), new Vector2Int(29, 52), new Vector2Int(36, 31) });
    }

    private static void DrawBackflip(IconCanvas c)
    {
        c.Arc(32, 32, 19, 35, 315, 5);
        c.Polygon(new[] { new Vector2Int(46, 13), new Vector2Int(53, 27), new Vector2Int(38, 24) });
        c.Line(24, 32, 32, 24, 3);
        c.Line(32, 24, 40, 32, 3);
    }

    private static void DrawClock(IconCanvas c)
    {
        c.Circle(32, 33, 19, 4);
        c.Line(32, 33, 32, 20, 4);
        c.Line(32, 33, 43, 39, 4);
        c.Line(25, 10, 39, 10, 4);
    }

    private static void DrawUnknown(IconCanvas c)
    {
        c.Arc(32, 26, 10, 190, 520, 5);
        c.Line(32, 35, 32, 42, 5);
        c.FillCircle(32, 50, 3);
    }

    private sealed class IconCanvas
    {
        private readonly int size;
        private readonly Color32[] pixels;
        private readonly Color32 ink;

        public IconCanvas(int size, Color ink)
        {
            this.size = size;
            this.ink = ink;
            pixels = new Color32[size * size];
        }

        public void Set(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            pixels[y * size + x] = color;
        }

        public void FillCircle(int cx, int cy, int radius)
        {
            FillCircle(cx, cy, radius, ink);
        }

        public void FillCircle(int cx, int cy, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radiusSquared) Set(cx + x, cy + y, color);
                }
            }
        }

        public void Circle(int cx, int cy, int radius, int thickness)
        {
            Arc(cx, cy, radius, 0, 360, thickness);
        }

        public void Arc(int cx, int cy, int radius, float startDegrees, float endDegrees, int thickness)
        {
            int steps = Mathf.Max(16, Mathf.CeilToInt((endDegrees - startDegrees) * radius / 35f));
            Vector2 previous = PointOnCircle(cx, cy, radius, startDegrees);
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)steps);
                Vector2 current = PointOnCircle(cx, cy, radius, angle);
                Line(Mathf.RoundToInt(previous.x), Mathf.RoundToInt(previous.y), Mathf.RoundToInt(current.x), Mathf.RoundToInt(current.y), thickness);
                previous = current;
            }
        }

        public void Line(int x0, int y0, int x1, int y1, int thickness)
        {
            Line(x0, y0, x1, y1, thickness, ink);
        }

        public void Line(int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int radius = Mathf.Max(0, thickness / 2);

            while (true)
            {
                FillCircle(x0, y0, radius, color);
                if (x0 == x1 && y0 == y1) break;
                int twiceError = 2 * error;
                if (twiceError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (twiceError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        public void Rect(int xMin, int yMin, int xMax, int yMax)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++) Set(x, y, ink);
            }
        }

        public void Polygon(Vector2Int[] points)
        {
            int minY = size;
            int maxY = 0;
            foreach (Vector2Int point in points)
            {
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            for (int y = minY; y <= maxY; y++)
            {
                List<int> intersections = new List<int>();
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2Int a = points[i];
                    Vector2Int b = points[(i + 1) % points.Length];
                    if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                    {
                        intersections.Add(Mathf.RoundToInt(a.x + (y - a.y) * (b.x - a.x) / (float)(b.y - a.y)));
                    }
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    for (int x = intersections[i]; x <= intersections[i + 1]; x++) Set(x, y, ink);
                }
            }
        }

        public Texture2D ToTexture(string name)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Vector2 PointOnCircle(float cx, float cy, float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(cx + Mathf.Cos(radians) * radius, cy + Mathf.Sin(radians) * radius);
        }
    }
}
