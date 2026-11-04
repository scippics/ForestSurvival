using UnityEngine;

// Следы колёс и сапог: один меш на всё кольцевым буфером. Старые следы затираются новыми,
// поэтому стоимость постоянная, сколько бы игрок ни ездил. Рисует только то, что осталось на мягкой земле.
public class GroundTracks : MonoBehaviour
{
    const int MaxQuads = 3000;
    const float Lift = 0.018f;
    const float BoundsSize = 4000f;

    public static GroundTracks Instance { get; private set; }

    public int Written { get; private set; }

    readonly Vector3[] vertices = new Vector3[MaxQuads * 4];
    readonly Vector2[] uvs = new Vector2[MaxQuads * 4];
    readonly Color32[] colors = new Color32[MaxQuads * 4];
    Mesh mesh;
    int next;
    bool dirty;

    void Awake()
    {
        Instance = this;

        var triangles = new int[MaxQuads * 6];
        for (int i = 0; i < MaxQuads; i++)
        {
            int corner = i * 4;
            int t = i * 6;
            triangles[t] = corner;
            triangles[t + 1] = corner + 1;
            triangles[t + 2] = corner + 2;
            triangles[t + 3] = corner + 2;
            triangles[t + 4] = corner + 1;
            triangles[t + 5] = corner + 3;
        }

        mesh = new Mesh { name = "GroundTracks" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * BoundsSize);
        mesh.MarkDynamic();

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Resources.Load<Shader>("ForestTrack"));
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    // Мягкая ли земля под этой точкой: от 0 до 1. Дорога мягкая всегда, а лес раскисает только в дождь.
    public static float Softness(Vector3 point)
    {
        float mud = OptimizedForestGenerator.MudDepth(point.x, point.z);
        return Mathf.Clamp01(Mathf.Max(mud, 0.12f) + ForestWeather.Rain * 0.45f);
    }

    public void AddWheel(Vector3 from, Vector3 to, Vector3 normal, float width, float strength)
    {
        Vector3 along = to - from;
        float length = along.magnitude;
        if (length < 0.001f)
            return;
        Vector3 side = Vector3.Cross(normal, along / length).normalized * (width * 0.5f);
        Vector3 up = normal * Lift;
        Write(from - side + up, from + side + up, to - side + up, to + side + up, strength, 0f, length);
    }

    public void AddBoot(Vector3 point, Vector3 forward, Vector3 normal, float strength)
    {
        Vector3 along = Vector3.ProjectOnPlane(forward, normal).normalized * 0.16f;
        Vector3 side = Vector3.Cross(normal, along).normalized * 0.075f;
        Vector3 up = normal * Lift;
        Write(point - along - side + up, point - along + side + up, point + along - side + up, point + along + side + up, strength, 1f, 1f);
    }

    void Write(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float strength, float kind, float repeat)
    {
        int corner = next * 4;
        vertices[corner] = a;
        vertices[corner + 1] = b;
        vertices[corner + 2] = c;
        vertices[corner + 3] = d;

        // Метры по длине идут в UV: узор протектора не растягивается на быстрой езде.
        float v0 = kind > 0.5f ? 0f : Written * 0.37f % 1f;
        float v1 = v0 + (kind > 0.5f ? 1f : repeat);
        uvs[corner] = new Vector2(0f, v0);
        uvs[corner + 1] = new Vector2(1f, v0);
        uvs[corner + 2] = new Vector2(0f, v1);
        uvs[corner + 3] = new Vector2(1f, v1);

        var color = new Color32((byte)(Mathf.Clamp01(strength) * 255f), (byte)(kind * 255f), 0, 255);
        colors[corner] = colors[corner + 1] = colors[corner + 2] = colors[corner + 3] = color;

        next = (next + 1) % MaxQuads;
        Written++;
        dirty = true;
    }

    void LateUpdate()
    {
        if (!dirty)
            return;
        dirty = false;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors32 = colors;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * BoundsSize);
    }
}

// Колея за каждым колесом. Лучом вниз, а не от WheelCollider: так след есть и у машины, которую ведёт другой игрок.
[RequireComponent(typeof(VehicleController))]
public class WheelTracks : MonoBehaviour
{
    const float StepDistance = 0.35f;
    const float TyreWidth = 0.26f;
    const float MaxGap = 3f;

    VehicleController car;
    Vector3[] last;
    bool[] valid;

    void Start()
    {
        car = GetComponent<VehicleController>();
        last = new Vector3[car.Wheels.Count];
        valid = new bool[car.Wheels.Count];
    }

    void FixedUpdate()
    {
        if (GroundTracks.Instance == null)
            return;

        for (int i = 0; i < last.Length; i++)
        {
            WheelCollider wheelCollider = car.Wheels[i].Collider;
            Vector3 wheel = wheelCollider.transform.position;
            if (valid[i] && (wheel - last[i]).sqrMagnitude < StepDistance * StepDistance)
                continue;

            // Точку касания знает само колесо: луч сверху попал бы в кузов, если колесо стоит под ним.
            if (!wheelCollider.GetGroundHit(out WheelHit hit) || !hit.collider.CompareTag(OptimizedForestGenerator.TerrainTag))
            {
                valid[i] = false;
                continue;
            }

            if (valid[i] && (hit.point - last[i]).sqrMagnitude < MaxGap * MaxGap)
                GroundTracks.Instance.AddWheel(last[i], hit.point, hit.normal, TyreWidth, GroundTracks.Softness(hit.point));
            last[i] = hit.point;
            valid[i] = true;
        }
    }
}

// Следы сапог: ложатся только там, где земля достаточно мягкая, через шаг, поочерёдно справа и слева.
[RequireComponent(typeof(CapsuleCollider))]
public class Footprints : MonoBehaviour
{
    const float StepLength = 0.82f;
    const float FootOffset = 0.13f;
    const float MinSoftness = 0.3f;
    const float RayLength = 1.5f;

    CapsuleCollider capsule;
    Vector3 lastStep;
    float side = 1f;

    void Start()
    {
        capsule = GetComponent<CapsuleCollider>();
        lastStep = transform.position;
    }

    void Update()
    {
        if (GroundTracks.Instance == null || !capsule.enabled)
            return;

        Vector3 moved = transform.position - lastStep;
        moved.y = 0f;
        if (moved.sqrMagnitude < StepLength * StepLength)
            return;
        lastStep = transform.position;
        side = -side;

        Vector3 forward = moved.normalized;
        Vector3 foot = transform.position + Vector3.Cross(Vector3.up, forward) * (side * FootOffset);
        if (!Physics.Raycast(foot, Vector3.down, out RaycastHit hit, RayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || !hit.collider.CompareTag(OptimizedForestGenerator.TerrainTag))
            return;

        float soft = GroundTracks.Softness(hit.point);
        if (soft >= MinSoftness)
            GroundTracks.Instance.AddBoot(hit.point, forward, hit.normal, soft);
    }
}
