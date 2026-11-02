using System.Collections.Generic;
using UnityEngine;

// Меши леса строятся кодом. У всех высота нормирована на 1, а начало координат стоит у основания:
// шейдер ветра считает изгиб от локального Y, а реальный размер задаёт матрица экземпляра.
// В цвете вершин данные для шейдера: красный — затенение (0 — тень), зелёный — кончик ветки.
public static class ForestMeshes
{
    const int TrunkSides = 8;
    const int CrownSides = 18;
    const int CrownTiers = 10;
    const int GrassBlades = 6;
    const int FernFronds = 8;
    const int FernSegments = 4;
    const int RockSides = 9;
    const int RockRings = 5;
    const int TyreSides = 26;
    const int Seed = 90210;

    public static Mesh BuildTrunk(float baseRadius)
    {
        var builder = new MeshBuilder();
        var random = new System.Random(Seed);
        float[] heights = { 0f, 0.05f, 0.14f, 0.3f, 0.55f, 0.8f, 1f };
        float[] radii = { 1.65f, 1.2f, 1f, 0.82f, 0.58f, 0.32f, 0.08f };

        // Основание ствола расширено корнями и неровно: ствол перестаёт быть правильным цилиндром.
        for (int ring = 0; ring < heights.Length; ring++)
        {
            float shade = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(0f, 0.14f, heights[ring]));
            for (int side = 0; side < TrunkSides; side++)
            {
                float angle = side * Mathf.PI * 2f / TrunkSides;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float roughness = 1f + ((float)random.NextDouble() - 0.5f) * 0.14f;
                builder.Add(radial * (baseRadius * radii[ring] * roughness) + Vector3.up * heights[ring], radial, shade, 0f);
            }
        }
        builder.ConnectRings(0, heights.Length, TrunkSides);
        return builder.ToMesh("Trunk");
    }

    // Ель из наложенных ярусов лап. Каждый ярус — зубчатая юбка с провисшими кончиками: сверху
    // её закрывает ярус выше, поэтому верх тёмный, а кончики светлые.
    public static Mesh BuildCrown()
    {
        var builder = new MeshBuilder();
        var random = new System.Random(Seed + 1);

        for (int tier = 0; tier < CrownTiers; tier++)
        {
            float t = tier / (CrownTiers - 1f);
            float bottom = Mathf.Lerp(0.17f, 0.82f, t);
            float top = bottom + Mathf.Lerp(0.3f, 0.18f, t);
            float radius = Mathf.Lerp(0.86f, 0.1f, Mathf.Pow(t, 0.92f));
            float twist = (float)random.NextDouble() * Mathf.PI * 2f;
            int start = builder.VertexCount;

            for (int side = 0; side < CrownSides; side++)
            {
                float angle = twist + side * Mathf.PI * 2f / CrownSides;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                builder.Add(radial * (radius * 0.28f) + Vector3.up * top, (radial * 0.5f + Vector3.up).normalized, 0.35f, 0f);
            }

            for (int side = 0; side < CrownSides; side++)
            {
                float angle = twist + side * Mathf.PI * 2f / CrownSides;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bool isBranch = side % 2 == 1;
                float jitter = 1f + ((float)random.NextDouble() - 0.5f) * 0.3f;
                float reach = radius * (isBranch ? 1f : 0.66f) * jitter;
                float droop = isBranch ? radius * 0.05f : -radius * 0.04f;
                builder.Add(radial * reach + Vector3.up * (bottom - droop), (radial + Vector3.up * 0.55f).normalized, 1f, isBranch ? 1f : 0.35f);
            }

            for (int side = 0; side < CrownSides; side++)
            {
                float angle = twist + side * Mathf.PI * 2f / CrownSides;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                builder.Add(radial * (radius * 0.2f) + Vector3.up * (bottom + (top - bottom) * 0.25f), Vector3.down, 0.12f, 0f);
            }

            builder.ConnectRings(start, 3, CrownSides);
        }
        return builder.ToMesh("Crown");
    }

    // Голые сучья сухого дерева. Вид снизу важнее точности, поэтому это тонкие четырёхгранные колья.
    public static Mesh BuildDeadBranches()
    {
        var builder = new MeshBuilder();
        var random = new System.Random(Seed + 2);
        for (int i = 0; i < 12; i++)
        {
            float height = Mathf.Lerp(0.32f, 0.92f, i / 11f);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float reach = Mathf.Lerp(0.5f, 0.14f, height);
            Vector3 root = Vector3.up * height;
            Vector3 end = root + radial * reach + Vector3.up * (reach * 0.12f * (i % 3 - 1));
            builder.AddStick(root, end, 0.016f);
        }
        return builder.ToMesh("DeadBranches");
    }

    // Пучок узких изогнутых травинок. Нормали смотрят вверх, иначе пучки мерцали бы от поворота к свету.
    public static Mesh BuildGrass()
    {
        var builder = new MeshBuilder();
        var random = new System.Random(Seed + 3);
        for (int blade = 0; blade < GrassBlades; blade++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            var lean = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 root = lean * ((float)random.NextDouble() * 0.28f);
            float height = Mathf.Lerp(0.6f, 1f, (float)random.NextDouble());
            float bend = Mathf.Lerp(0.15f, 0.42f, (float)random.NextDouble());
            Vector3 side = Vector3.Cross(lean, Vector3.up) * 0.075f;

            int start = builder.VertexCount;
            builder.Add(root - side, Vector3.up, 0.15f, 0f);
            builder.Add(root + side, Vector3.up, 0.15f, 0f);
            Vector3 middle = root + lean * (bend * 0.35f) + Vector3.up * (height * 0.6f);
            builder.Add(middle - side * 0.7f, Vector3.up, 0.7f, 0.5f);
            builder.Add(middle + side * 0.7f, Vector3.up, 0.7f, 0.5f);
            builder.Add(root + lean * bend + Vector3.up * height, Vector3.up, 1f, 1f);

            builder.AddQuad(start, start + 1, start + 2, start + 3);
            builder.AddTriangle(start + 2, start + 3, start + 4);
        }
        return builder.ToMesh("Grass");
    }

    // Папоротник: дуги-вайи расходятся от центра и никнут к концам.
    public static Mesh BuildFern()
    {
        var builder = new MeshBuilder();
        var random = new System.Random(Seed + 4);
        for (int frond = 0; frond < FernFronds; frond++)
        {
            float angle = (frond + (float)random.NextDouble() * 0.6f) * Mathf.PI * 2f / FernFronds;
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 side = Vector3.Cross(outward, Vector3.up);
            float length = Mathf.Lerp(0.42f, 0.55f, (float)random.NextDouble());
            int start = builder.VertexCount;

            for (int segment = 0; segment <= FernSegments; segment++)
            {
                float t = segment / (float)FernSegments;
                float height = Mathf.Sin(t * Mathf.PI * 0.72f) * 1f;
                Vector3 center = outward * (length * t) + Vector3.up * height;

                // Сверху вайя широкая, а у основания и на кончике сходит на нет.
                float width = Mathf.Sin(Mathf.Clamp01(t * 0.92f + 0.08f) * Mathf.PI) * 0.1f;
                float shade = Mathf.Lerp(0.25f, 1f, t);
                builder.Add(center - side * width, Vector3.up, shade, t);
                builder.Add(center + side * width, Vector3.up, shade, t);
            }

            for (int segment = 0; segment < FernSegments; segment++)
            {
                int left = start + segment * 2;
                builder.AddQuad(left, left + 1, left + 2, left + 3);
            }
        }
        return builder.ToMesh("Fern");
    }

    // Валун: сплющенная сфера с неровными вершинами.
    public static Mesh BuildRock()
    {
        var builder = new MeshBuilder();
        var random = new System.Random(Seed + 5);
        for (int ring = 0; ring < RockRings; ring++)
        {
            float t = ring / (RockRings - 1f);
            float polar = Mathf.Lerp(Mathf.PI * 0.62f, 0.05f, t);
            for (int side = 0; side < RockSides; side++)
            {
                float angle = side * Mathf.PI * 2f / RockSides;
                float bump = 1f + ((float)random.NextDouble() - 0.5f) * 0.32f;
                var direction = new Vector3(Mathf.Sin(polar) * Mathf.Cos(angle), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(angle));
                builder.Add(direction * bump * 0.6f + Vector3.up * 0.3f, direction, Mathf.Lerp(0.45f, 1f, t), 0f);
            }
        }
        builder.ConnectRings(0, RockRings, RockSides);
        return builder.ToMesh("Rock", true);
    }

    // Покрышка с грунтозацепами вдоль оси X: чередование радиуса даёт зубчатый силуэт.
    public static Mesh BuildTyre()
    {
        var builder = new MeshBuilder();
        float[] offsets = { -0.5f, -0.5f, -0.42f, 0.42f, 0.5f, 0.5f };
        float[] radii = { 0.32f, 0.47f, 0.5f, 0.5f, 0.47f, 0.32f };
        for (int ring = 0; ring < offsets.Length; ring++)
        {
            for (int side = 0; side < TyreSides; side++)
            {
                float angle = side * Mathf.PI * 2f / TyreSides;
                var radial = new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));
                bool isKnob = side % 2 == 0 && ring > 0 && ring < offsets.Length - 1;
                builder.Add(radial * radii[ring] * (isKnob ? 1f : 0.93f) + Vector3.right * offsets[ring], radial, 1f, 0f);
            }
        }
        builder.ConnectRings(0, offsets.Length, TyreSides);
        return builder.ToMesh("Tyre", true);
    }

    class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public int VertexCount => vertices.Count;

        public void Add(Vector3 position, Vector3 normal, float shade, float tip)
        {
            vertices.Add(position);
            normals.Add(normal);
            colors.Add(new Color(shade, tip, 0f, 1f));
        }

        public void AddStick(Vector3 from, Vector3 to, float radius)
        {
            Vector3 along = (to - from).normalized;
            Vector3 side = Vector3.Cross(along, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, along).normalized;
            int start = VertexCount;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 offset = corner == 0 ? side : corner == 1 ? up : corner == 2 ? -side : -up;
                Add(from + offset * radius, offset, 0.8f, 0f);
            }
            Add(to, along, 1f, 0f);

            for (int corner = 0; corner < 4; corner++)
                AddTriangle(start + corner, start + (corner + 1) % 4, start + 4);
        }

        public void ConnectRings(int start, int rings, int sides)
        {
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int current = start + ring * sides;
                    int above = current + sides;
                    AddQuad(current + side, current + next, above + side, above + next);
                }
            }
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(b, d, c);
        }

        public Mesh ToMesh(string name, bool recalculateNormals = false)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            if (recalculateNormals)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
