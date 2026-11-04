using UnityEngine;

// Готовые модели камней и кустов приводятся к тому же виду, что и построенные кодом: стоят вертикально,
// основание на нуле, размер единый. Поэтому ветер, мох и рассадка работают с ними без исключений.
public static class NatureMeshes
{
    const string Folder = "Nature/";
    const float RockWidth = 1.2f;
    const float BushWidth = 1.4f;
    const float BushBaseShade = 0.55f;

    public static Mesh[] Rocks(Mesh fallback)
    {
        return LoadAll(fallback, RockWidth, new[] { "Rock_1", "Rock_2", "Rock_3", "Rock_4", "Rock_5" });
    }

    public static Mesh[] Bushes(Mesh fallback)
    {
        return LoadAll(fallback, BushWidth, new[] { "Bush", "Bush_Large", "Bush_Small" });
    }

    public static Texture2D BushLeaves => Resources.Load<Texture2D>(Folder + "Bush_Leaves");

    static Mesh[] LoadAll(Mesh fallback, float width, string[] names)
    {
        var meshes = new System.Collections.Generic.List<Mesh>();
        foreach (string name in names)
        {
            Mesh mesh = Bake(name, width);
            if (mesh != null)
                meshes.Add(mesh);
        }
        return meshes.Count > 0 ? meshes.ToArray() : new[] { fallback };
    }

    static Mesh Bake(string name, float width)
    {
        var model = Resources.Load<GameObject>(Folder + name);
        MeshFilter filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
        if (filter == null || !filter.sharedMesh.isReadable)
            return null;

        Mesh source = filter.sharedMesh;
        Matrix4x4 place = filter.transform.localToWorldMatrix;
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;

        var bounds = new Bounds(place.MultiplyPoint3x4(vertices[0]), Vector3.zero);
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = place.MultiplyPoint3x4(vertices[i]);
            normals[i] = place.MultiplyVector(normals[i]).normalized;
            bounds.Encapsulate(vertices[i]);
        }

        float scale = width / Mathf.Max(bounds.size.x, bounds.size.z);
        var colors = new Color[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i] - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            vertices[i] = v * scale;

            // Красный канал — тень внутри и у земли, как у наших моделей: снизу темнее.
            float up = v.y / Mathf.Max(bounds.size.y, 0.0001f);
            colors[i] = new Color(Mathf.Lerp(BushBaseShade, 1f, up), up * 0.5f, 0f, 1f);
        }

        var mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = source.uv;
        mesh.colors = colors;
        mesh.triangles = source.triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
