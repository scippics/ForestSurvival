using System.Collections.Generic;
using UnityEngine;

// Текстуры, которые рисуются кодом: шкалы приборов, дисплей магнитолы, номерной знак.
public static class ForestTextures
{
    const int DialSize = 256;
    const float ArcDegrees = 270f;

    // Шрифт 5x7: каждый символ — семь строк по пять бит. Кириллица совпадает по начертанию с латиницей,
    // поэтому здесь только те буквы, что есть в подписях.
    static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
    {
        ['0'] = new byte[] { 14, 17, 19, 21, 25, 17, 14 },
        ['1'] = new byte[] { 4, 12, 4, 4, 4, 4, 14 },
        ['2'] = new byte[] { 14, 17, 1, 2, 4, 8, 31 },
        ['3'] = new byte[] { 30, 1, 1, 14, 1, 1, 30 },
        ['4'] = new byte[] { 2, 6, 10, 18, 31, 2, 2 },
        ['5'] = new byte[] { 31, 16, 30, 1, 1, 17, 14 },
        ['6'] = new byte[] { 6, 8, 16, 30, 17, 17, 14 },
        ['7'] = new byte[] { 31, 1, 2, 4, 8, 8, 8 },
        ['8'] = new byte[] { 14, 17, 17, 14, 17, 17, 14 },
        ['9'] = new byte[] { 14, 17, 17, 15, 1, 2, 12 },
        ['.'] = new byte[] { 0, 0, 0, 0, 0, 12, 12 },
        ['/'] = new byte[] { 1, 1, 2, 4, 8, 16, 16 },
        ['-'] = new byte[] { 0, 0, 0, 31, 0, 0, 0 },
        [' '] = new byte[] { 0, 0, 0, 0, 0, 0, 0 },
        ['К'] = new byte[] { 17, 18, 20, 24, 20, 18, 17 },
        ['М'] = new byte[] { 17, 27, 21, 21, 17, 17, 17 },
        ['Ч'] = new byte[] { 17, 17, 17, 15, 1, 1, 1 },
        ['Х'] = new byte[] { 17, 17, 10, 4, 10, 17, 17 },
        ['Т'] = new byte[] { 31, 4, 4, 4, 4, 4, 4 },
        ['Е'] = new byte[] { 31, 16, 16, 30, 16, 16, 31 },
        ['Р'] = new byte[] { 30, 17, 17, 30, 16, 16, 16 },
        ['Л'] = new byte[] { 7, 9, 9, 9, 9, 9, 17 },
        ['О'] = new byte[] { 14, 17, 17, 17, 17, 17, 14 },
        ['Н'] = new byte[] { 17, 17, 17, 31, 17, 17, 17 },
        ['А'] = new byte[] { 14, 17, 17, 31, 17, 17, 17 },
        ['В'] = new byte[] { 30, 17, 17, 30, 17, 17, 30 },
        ['С'] = new byte[] { 14, 17, 16, 16, 16, 17, 14 },
        ['У'] = new byte[] { 17, 17, 17, 15, 1, 17, 14 },
        ['И'] = new byte[] { 17, 17, 19, 21, 25, 17, 17 },
        ['Д'] = new byte[] { 14, 18, 18, 18, 18, 31, 17 },
        ['Г'] = new byte[] { 31, 16, 16, 16, 16, 16, 16 },
        ['Б'] = new byte[] { 31, 16, 16, 30, 17, 17, 30 },
        ['Ю'] = new byte[] { 18, 21, 21, 29, 21, 21, 18 },
        ['Я'] = new byte[] { 15, 17, 17, 15, 5, 9, 17 },
        ['Ы'] = new byte[] { 17, 17, 17, 29, 19, 19, 29 },
        ['Ь'] = new byte[] { 16, 16, 16, 30, 17, 17, 30 },
        ['Ш'] = new byte[] { 21, 21, 21, 21, 21, 21, 31 },
        ['Щ'] = new byte[] { 21, 21, 21, 21, 21, 31, 1 },
        ['Ж'] = new byte[] { 21, 21, 14, 4, 14, 21, 21 },
        ['З'] = new byte[] { 30, 1, 1, 14, 1, 1, 30 },
        ['Ф'] = new byte[] { 4, 14, 21, 21, 14, 4, 4 },
        ['Ц'] = new byte[] { 18, 18, 18, 18, 18, 31, 1 },
        ['П'] = new byte[] { 31, 17, 17, 17, 17, 17, 17 },
        ['Э'] = new byte[] { 30, 1, 1, 15, 1, 1, 30 },
        ['Й'] = new byte[] { 21, 17, 19, 21, 25, 17, 17 },
        ['Ъ'] = new byte[] { 24, 8, 8, 14, 9, 9, 14 }
    };

    static Color32[] pixels;
    static int width, height;

    static void Begin(int w, int h, Color32 clear)
    {
        width = w;
        height = h;
        pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
    }

    static Texture2D Finish(string name)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        pixels = null;
        return texture;
    }

    static void Dot(int x, int y, Color32 color)
    {
        if (x >= 0 && y >= 0 && x < width && y < height)
            pixels[y * width + x] = color;
    }

    static void Rect(int x, int y, int w, int h, Color32 color)
    {
        for (int iy = 0; iy < h; iy++)
            for (int ix = 0; ix < w; ix++)
                Dot(x + ix, y + iy, color);
    }

    static void Line(Vector2 from, Vector2 to, float thickness, Color32 color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
        int radius = Mathf.CeilToInt(thickness * 0.5f);
        for (int s = 0; s <= steps; s++)
        {
            Vector2 p = Vector2.Lerp(from, to, s / (float)steps);
            for (int iy = -radius; iy <= radius; iy++)
                for (int ix = -radius; ix <= radius; ix++)
                    if (ix * ix + iy * iy <= thickness * thickness * 0.25f)
                        Dot(Mathf.RoundToInt(p.x) + ix, Mathf.RoundToInt(p.y) + iy, color);
        }
    }

    // Строка с центром в указанной точке. Ось Y текстуры идёт вверх, первая строка глифа — верхняя.
    static void Text(string text, int centerX, int centerY, int scale, Color32 color)
    {
        int total = text.Length * 6 * scale - scale;
        int x = centerX - total / 2;
        int top = centerY + 7 * scale / 2;
        foreach (char raw in text)
        {
            if (Glyphs.TryGetValue(char.ToUpperInvariant(raw), out byte[] glyph))
            {
                for (int row = 0; row < 7; row++)
                    for (int column = 0; column < 5; column++)
                        if ((glyph[row] >> (4 - column) & 1) != 0)
                            Rect(x + column * scale, top - (row + 1) * scale, scale, scale, color);
            }
            x += 6 * scale;
        }
    }

    // Шкала: дуга 270° по часовой стрелке от нижнего левого края. Смотреть на неё будут с лицевой стороны
    // квада, где положительное направление X совпадает с правой стороной водителя.
    public static Texture2D Dial(int max, int step, string caption)
    {
        var scale = new Color32(235, 240, 225, 255);
        var dim = new Color32(120, 130, 115, 255);
        Begin(DialSize, DialSize, new Color32(0, 0, 0, 255));

        var center = new Vector2(DialSize * 0.5f, DialSize * 0.5f);
        float outer = DialSize * 0.47f;
        int subdivisions = 5;
        int major = max / step;

        for (int i = 0; i <= major * subdivisions; i++)
        {
            float fraction = i / (float)(major * subdivisions);
            float angle = (225f - ArcDegrees * fraction) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            bool isMajor = i % subdivisions == 0;

            Line(center + direction * (outer - (isMajor ? 20f : 9f)), center + direction * outer, isMajor ? 4f : 2f, isMajor ? scale : dim);
            if (isMajor)
            {
                Vector2 label = center + direction * (outer - 42f);
                Text((i / subdivisions * step).ToString(), Mathf.RoundToInt(label.x), Mathf.RoundToInt(label.y), 2, scale);
            }
        }

        Text(caption, DialSize / 2, DialSize / 2 - 46, 2, dim);
        return Finish("Dial_" + caption);
    }

    // Окно изнутри лампы: свет гуще у стола, а по краям висят шторы со складками.
    public static Texture2D LitWindow()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f);
                float v = y / (size - 1f);
                float glow = Mathf.Clamp01(1.05f - Vector2.Distance(new Vector2(u, v), new Vector2(0.62f, 0.3f)) * 1.35f);
                float edge = Mathf.Min(u, 1f - u);
                float curtain = Mathf.SmoothStep(0.12f, 0.24f, edge);
                float folds = 0.8f + 0.2f * Mathf.Sin(u * 90f);
                float brightness = Mathf.Lerp(0.14f * folds, 0.25f + 0.75f * glow, curtain);
                texture.SetPixel(x, y, new Color(brightness, brightness, brightness));
            }
        }
        texture.Apply();
        return texture;
    }

    public static Texture2D RadioDisplay()
    {
        Begin(256, 58, new Color32(14, 6, 0, 255));
        Text("104.7 МГЦ", 128, 29, 3, new Color32(255, 255, 255, 255));
        return Finish("RadioDisplay");
    }

    public static Texture2D LicensePlate(string number)
    {
        Begin(256, 56, new Color32(205, 200, 180, 255));
        for (int x = 0; x < 256; x++)
        {
            Dot(x, 1, new Color32(20, 20, 20, 255));
            Dot(x, 54, new Color32(20, 20, 20, 255));
        }
        Text(number, 128, 28, 5, new Color32(18, 18, 18, 255));
        return Finish("LicensePlate");
    }

    public static Texture2D Sign(string first, string second, Color32 background, Color32 ink)
    {
        Begin(320, 128, background);
        Text(first, 160, second.Length > 0 ? 82 : 64, 4, ink);
        if (second.Length > 0)
            Text(second, 160, 38, 4, ink);
        return Finish("Sign_" + first);
    }
}
