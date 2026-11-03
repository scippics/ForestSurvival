using UnityEngine;

// Завал на просеке: дерево или крупные ветки лежат неподвижно и останавливают машину. Сдвинуть можно руками:
// как только за него взялись, он становится обычным физическим предметом. Отвал и кенгурятник сносят его с разгона.
public class RoadBlock : MonoBehaviour
{
    public const string NamePrefix = "Block_";
    public const int KindTree = 0;
    public const int KindBranch = 1;

    const float TreeLength = 9.5f;
    const float TreeRadius = 0.17f;
    const float TreeMass = 30f;
    const float BranchLength = 4.5f;
    const float BranchRadius = 0.09f;
    const float BranchMass = 16f;
    const float ColliderHeight = 0.95f;
    const float RamSpeed = 3f;
    const float BullBarSpeed = 6f;
    const float FreeLift = 0.2f;

    static readonly Color BarkColor = new Color(0.17f, 0.12f, 0.08f);
    static readonly Color NeedleColor = new Color(0.07f, 0.13f, 0.07f);

    static Mesh bough;
    static Mesh Bough => bough != null ? bough : bough = ForestMeshes.BuildLooseBough();

    public bool IsLodged { get; private set; } = true;

    Rigidbody body;

    public static string MakeName(string site, int index, int kind)
    {
        return NamePrefix + site + "_" + index + "_" + kind;
    }

    public static bool TryParseKind(string name, out int kind)
    {
        kind = 0;
        if (!name.StartsWith(NamePrefix))
            return false;
        string[] parts = name.Split('_');
        return parts.Length == 4 && int.TryParse(parts[3], out kind);
    }

    public static GameObject Create(string name, int kind, Vector3 position, Quaternion rotation)
    {
        bool tree = kind == KindTree;
        float length = tree ? TreeLength : BranchLength;
        float radius = tree ? TreeRadius : BranchRadius;

        var root = new GameObject(name) { tag = InteractionRay.PickupableTag };
        root.transform.SetPositionAndRotation(position, rotation);

        // Детерминировано по имени: у всех игроков одно и то же дерево.
        int seed = 17;
        foreach (char letter in name)
            seed = unchecked(seed * 31 + letter);
        var random = new System.Random(seed);
        Material bark = ProceduralModels.Lit(BarkColor, 0f, 0f, 0.08f, 7);
        Material needles = ProceduralModels.Lit(NeedleColor, 0f, 0f, 0.05f, 9);

        ProceduralModels.Part(root.transform, "FallenTrunk", PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, length * 0.5f, radius * 2f), bark, Quaternion.Euler(0f, 0f, 90f));

        int boughs = tree ? 12 : 6;
        for (int i = 0; i < boughs; i++)
        {
            float x = Mathf.Lerp(-length * 0.46f, length * 0.48f, (i + (float)random.NextDouble() * 0.6f) / boughs);
            float angle = (float)random.NextDouble() * 170f + 5f;
            float reach = Mathf.Lerp(0.55f, 1.15f, (float)random.NextDouble()) * (tree ? 1f : 0.7f);
            Quaternion turn = Quaternion.Euler(angle, 0f, (float)random.NextDouble() * 50f - 25f);
            Vector3 end = turn * Vector3.up * reach;
            ProceduralModels.Part(root.transform, "FallenBranch", PrimitiveType.Cylinder, new Vector3(x, 0f, 0f) + end * 0.5f, new Vector3(radius * 0.5f, reach * 0.5f, radius * 0.5f), bark, turn);

            // Лапы хвои расходятся от ветки в разные стороны.
            for (int j = 0; j < 4; j++)
            {
                Vector3 at = new Vector3(x, 0f, 0f) + end * (0.35f + j * 0.2f);
                Quaternion lay = Quaternion.Euler((float)random.NextDouble() * 50f - 25f, j * 90f + (float)random.NextDouble() * 60f, (float)random.NextDouble() * 40f - 20f);
                ProceduralModels.MeshPart(root.transform, "FallenNeedles", Bough, at, Vector3.one * (0.75f - j * 0.08f), needles, lay);
            }
        }

        // Коллайдер выше самого ствола: кузов не должен переезжать завал поверху.
        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(length, ColliderHeight, tree ? 0.8f : 0.5f);
        box.center = new Vector3(0f, ColliderHeight * 0.32f, 0f);

        var rigid = root.AddComponent<Rigidbody>();
        rigid.mass = tree ? TreeMass : BranchMass;
        rigid.isKinematic = true;
        rigid.angularDamping = 1.5f;
        root.AddComponent<ImpactSound>();
        root.AddComponent<RoadBlock>();
        return root;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Free()
    {
        if (!IsLodged)
            return;
        IsLodged = false;
        body.isKinematic = false;

        // Завал лежит вплотную к неровной земле: чуть приподнять, иначе он сидит в кочках и не сдвинется.
        body.position += Vector3.up * FreeLift;
        body.WakeUp();

        OptimizedForestGenerator forest = FindAnyObjectByType<OptimizedForestGenerator>();
        if (forest != null)
            forest.Track(body);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsLodged || !collision.rigidbody || !collision.rigidbody.TryGetComponent(out CarUpgrades upgrades))
            return;
        if (!upgrades.Mounted.TryGetValue(CarSlot.Front, out CarPartKind front))
            return;

        float needed = front == CarPartKind.RamPlow ? RamSpeed : front == CarPartKind.BullBar || front == CarPartKind.WinchBumper ? BullBarSpeed : float.MaxValue;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < needed)
            return;

        // Завал лопается от удара, а машина почти не теряет ход.
        Free();
        body.AddForce(collision.rigidbody.linearVelocity * body.mass * 0.7f + Vector3.up * body.mass * 2f, ForceMode.Impulse);
        body.AddTorque(Vector3.up * body.mass * 2f, ForceMode.Impulse);

        // Удар о неподвижное гасит скорость целиком, поэтому её возвращают в ту сторону, куда ехали.
        Transform car = collision.rigidbody.transform;
        float forward = Vector3.Dot(collision.relativeVelocity, car.forward);
        collision.rigidbody.linearVelocity = car.forward * (Mathf.Abs(forward) * 0.85f) * (Vector3.Dot(car.forward, transform.position - car.position) >= 0f ? 1f : -1f);
        ForestAudio.Play(ForestSound.Crack, 0.8f);
        if (WorldSync.Instance != null)
            WorldSync.Instance.SendItem(body);
    }

}
