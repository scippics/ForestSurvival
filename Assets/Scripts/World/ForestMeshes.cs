using System.Collections.Generic;
using UnityEngine;

// Меши леса строятся кодом. У всех высота нормирована на 1, а начало координат стоит у основания:
// шейдер ветра считает изгиб от локального Y, а реальный размер задаёт матрица экземпляра.
public static class ForestMeshes
{
    const int TreeSides = 8;

    public static Mesh BuildTrunk(float baseRadius)
    {
        var builder = new MeshBuilder();
        builder.AddLathe(new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
            new[] { baseRadius, baseRadius * 0.8f, baseRadius * 0.6f, baseRadius * 0.4f, baseRadius * 0.15f });
        return builder.ToMesh("Trunk");
    }

    public static Mesh BuildCrown()
    {
        var builder = new MeshBuilder();
        builder.AddLathe(new[] { 0.22f, 0.42f, 0.62f }, new[] { 1f, 0.55f, 0.02f });
        builder.AddLathe(new[] { 0.42f, 0.6f, 0.82f }, new[] { 0.78f, 0.42f, 0.02f });
        builder.AddLathe(new[] { 0.62f, 0.8f, 1f }, new[] { 0.55f, 0.28f, 0.01f });
        return builder.ToMesh("Crown");
    }

    // Два скрещённых пучка. Нормали смотрят вверх, иначе пучки мерцали бы от поворота к свету.
    public static Mesh BuildGrass()
    {
        var builder = new MeshBuilder();
        builder.AddBlade(Vector3.right);
        builder.AddBlade(Vector3.forward);
        return builder.ToMesh("Grass");
    }

    // Плоский квадрат 1x1 на Y = 0: шейдер ветра его не двигает.
    public static Mesh BuildGroundQuad()
    {
        var mesh = new Mesh { name = "GroundQuad" };
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
        });
        mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> triangles = new List<int>();

        public void AddLathe(float[] heights, float[] radii)
        {
            int start = vertices.Count;
            float slope = (radii[0] - radii[radii.Length - 1]) / Mathf.Max(heights[heights.Length - 1] - heights[0], 0.001f);

            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side < TreeSides; side++)
                {
                    float angle = side * Mathf.PI * 2f / TreeSides;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices.Add(radial * radii[ring] + Vector3.up * heights[ring]);
                    normals.Add((radial + Vector3.up * slope).normalized);
                }
            }

            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                for (int side = 0; side < TreeSides; side++)
                {
                    int next = (side + 1) % TreeSides;
                    int current = start + ring * TreeSides;
                    int above = current + TreeSides;
                    AddQuad(current + side, current + next, above + side, above + next);
                }
            }
        }

        public void AddBlade(Vector3 axis)
        {
            float[] heights = { 0f, 0.5f, 1f };
            float[] widths = { 0.5f, 0.35f, 0.06f };
            int start = vertices.Count;

            for (int row = 0; row < heights.Length; row++)
            {
                vertices.Add(axis * -widths[row] + Vector3.up * heights[row]);
                vertices.Add(axis * widths[row] + Vector3.up * heights[row]);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
            }

            for (int row = 0; row < heights.Length - 1; row++)
            {
                int left = start + row * 2;
                AddQuad(left, left + 1, left + 2, left + 3);
            }
        }

        void AddQuad(int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(d);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
