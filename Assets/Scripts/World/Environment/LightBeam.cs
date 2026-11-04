using UnityEngine;

// Видимый в воздухе конус света. Включается и гаснет вместе со своим источником; днём едва заметен.
public class LightBeam : MonoBehaviour
{
    const int Sides = 20;
    const float DayFactor = 0.35f;

    static Mesh coneMesh;

    Light source;
    Renderer beamRenderer;
    Material material;
    float strength;

    public static void Attach(Light source, float length, float strength)
    {
        var beamObject = new GameObject("LightBeam");
        beamObject.transform.SetParent(source.transform, false);

        float radius = Mathf.Tan(source.spotAngle * 0.5f * Mathf.Deg2Rad) * length * 0.75f;
        beamObject.transform.localScale = new Vector3(radius, radius, length);

        beamObject.AddComponent<MeshFilter>().sharedMesh = coneMesh != null ? coneMesh : coneMesh = BuildCone();
        var beamRenderer = beamObject.AddComponent<MeshRenderer>();
        beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;
        beamRenderer.sharedMaterial = new Material(Resources.Load<Shader>("ForestBeam"));

        var beam = beamObject.AddComponent<LightBeam>();
        beam.source = source;
        beam.beamRenderer = beamRenderer;
        beam.material = beamRenderer.sharedMaterial;
        beam.strength = strength;
    }

    // Единичный конус вдоль Z. Красный канал гасит луч к дальнему концу.
    static Mesh BuildCone()
    {
        var vertices = new Vector3[Sides * 2];
        var normals = new Vector3[Sides * 2];
        var colors = new Color[Sides * 2];
        var triangles = new int[Sides * 6];
        for (int side = 0; side < Sides; side++)
        {
            float angle = side * Mathf.PI * 2f / Sides;
            var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            vertices[side] = radial * 0.03f;
            vertices[side + Sides] = radial + Vector3.forward;
            normals[side] = radial;
            normals[side + Sides] = radial;
            colors[side] = Color.white;
            colors[side + Sides] = Color.black;

            int next = (side + 1) % Sides;
            int index = side * 6;
            triangles[index] = side;
            triangles[index + 1] = next;
            triangles[index + 2] = side + Sides;
            triangles[index + 3] = next;
            triangles[index + 4] = next + Sides;
            triangles[index + 5] = side + Sides;
        }

        var mesh = new Mesh { name = "LightBeamCone", vertices = vertices, normals = normals, colors = colors, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    void LateUpdate()
    {
        beamRenderer.enabled = source.enabled;
        if (source.enabled)
            material.color = source.color * (strength * (DayNightCycle.IsDark ? 1f : DayFactor));
    }
}
