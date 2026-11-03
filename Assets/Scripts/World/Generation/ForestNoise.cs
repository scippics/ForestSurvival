using UnityEngine;

// Общая текстура шума вместо картинок: все шейдеры леса берут из неё фактуру в мировых координатах.
// Текстура зациклена, поэтому не даёт швов. R и G — крупные пятна, B — мелкое зерно, A — волокна коры.
public static class ForestNoise
{
    const int Size = 256;
    const int LatticeSize = 512;
    const int Seed = 4242;
    const float Contrast = 1.7f;

    static float[] lattice;

    public static void Install()
    {
        var random = new System.Random(Seed);
        lattice = new float[LatticeSize * LatticeSize];
        for (int i = 0; i < lattice.Length; i++)
            lattice[i] = (float)random.NextDouble();

        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size;
                float v = (float)y / Size;
                pixels[y * Size + x] = new Color32(
                    ToByte(Fractal(u, v, 8, 8, 4, 0)),
                    ToByte(Fractal(u, v, 16, 16, 4, 37)),
                    ToByte(Fractal(u, v, 64, 64, 2, 91)),
                    ToByte(Fractal(u, v, 64, 8, 3, 151)));
            }
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
        {
            name = "ForestNoise",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        Shader.SetGlobalTexture("_ForestNoise", texture);
        lattice = null;
    }

    static byte ToByte(float value)
    {
        return (byte)(Mathf.Clamp01((value - 0.5f) * Contrast + 0.5f) * 255f);
    }

    static float Fractal(float u, float v, int periodX, int periodY, int octaves, int offset)
    {
        float total = 0f;
        float amplitude = 0.5f;
        float sum = 0f;
        for (int octave = 0; octave < octaves; octave++)
        {
            total += Periodic(u * periodX, v * periodY, periodX, periodY, offset + octave * 13) * amplitude;
            sum += amplitude;
            amplitude *= 0.5f;
            periodX *= 2;
            periodY *= 2;
        }
        return total / sum;
    }

    static float Periodic(float x, float y, int periodX, int periodY, int offset)
    {
        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0f, 1f, x - ix);
        float ty = Mathf.SmoothStep(0f, 1f, y - iy);
        float near = Mathf.Lerp(Sample(ix, iy, periodX, periodY, offset), Sample(ix + 1, iy, periodX, periodY, offset), tx);
        float far = Mathf.Lerp(Sample(ix, iy + 1, periodX, periodY, offset), Sample(ix + 1, iy + 1, periodX, periodY, offset), tx);
        return Mathf.Lerp(near, far, ty);
    }

    static float Sample(int x, int y, int periodX, int periodY, int offset)
    {
        int wrappedX = (x % periodX + periodX) % periodX;
        int wrappedY = (y % periodY + periodY) % periodY;
        return lattice[(wrappedY + offset) % LatticeSize * LatticeSize + (wrappedX + offset * 7) % LatticeSize];
    }
}
