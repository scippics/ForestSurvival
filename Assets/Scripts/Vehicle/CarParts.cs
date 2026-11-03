using System.Collections.Generic;
using UnityEngine;

public enum CarSlot
{
    Front,
    Roof,
    Sides,
    Rear,
    Wheels,
    Engine,
    Tank
}

public enum CarPartKind
{
    BullBar,
    RamPlow,
    WinchBumper,
    RoofLights,
    RoofCage,
    SideArmor,
    RearBumper,
    JerryRack,
    MudTyres,
    WideTyres,
    TunedEngine,
    DieselEngine,
    ExtraTank
}

// Что деталь даёт машине. Масса настоящая: лишний вес сам отнимает разгон и садит в грязь.
public class CarPartInfo
{
    public CarSlot Slot;
    public string Name;
    public float Mass;
    public float Torque;
    public float Speed;
    public float MudGrip;
    public float TankLiters;
    public float Burn;
    public float WheelRadius;
    public float Armor;
    public float Seconds = 3f;
}

public static class CarParts
{
    // Больше этого человек не поднимет: сила рук в PlayerHands рассчитана примерно на столько.
    const float MaxCarryMass = 30f;

    public static readonly Dictionary<CarPartKind, CarPartInfo> Info = new Dictionary<CarPartKind, CarPartInfo>
    {
        { CarPartKind.BullBar, new CarPartInfo { Slot = CarSlot.Front, Name = "Кенгурятник", Mass = 35f, Armor = 2f } },
        { CarPartKind.RamPlow, new CarPartInfo { Slot = CarSlot.Front, Name = "Отвал", Mass = 90f, Armor = 5f, Speed = -0.04f, Seconds = 5f } },
        { CarPartKind.WinchBumper, new CarPartInfo { Slot = CarSlot.Front, Name = "Силовой бампер", Mass = 70f, Armor = 3f, MudGrip = 0.08f, Seconds = 4f } },
        { CarPartKind.RoofLights, new CarPartInfo { Slot = CarSlot.Roof, Name = "Люстра на крышу", Mass = 15f, Seconds = 2.5f } },
        { CarPartKind.RoofCage, new CarPartInfo { Slot = CarSlot.Roof, Name = "Каркас на крышу", Mass = 60f, Armor = 3f, Seconds = 4f } },
        { CarPartKind.SideArmor, new CarPartInfo { Slot = CarSlot.Sides, Name = "Бортовые листы", Mass = 120f, Armor = 4f, Speed = -0.05f, Seconds = 5f } },
        { CarPartKind.RearBumper, new CarPartInfo { Slot = CarSlot.Rear, Name = "Задний бампер", Mass = 30f, Armor = 1f } },
        { CarPartKind.JerryRack, new CarPartInfo { Slot = CarSlot.Rear, Name = "Подвесной бачок", Mass = 40f, TankLiters = 20f } },
        { CarPartKind.MudTyres, new CarPartInfo { Slot = CarSlot.Wheels, Name = "Грязевая резина", Mass = 40f, MudGrip = 0.25f, Speed = -0.06f, Seconds = 6f } },
        { CarPartKind.WideTyres, new CarPartInfo { Slot = CarSlot.Wheels, Name = "Большие колёса", Mass = 60f, MudGrip = 0.14f, WheelRadius = 0.05f, Seconds = 6f } },
        { CarPartKind.TunedEngine, new CarPartInfo { Slot = CarSlot.Engine, Name = "Форсированный мотор", Mass = 20f, Torque = 0.3f, Speed = 0.08f, Burn = 0.35f, Seconds = 7f } },
        { CarPartKind.DieselEngine, new CarPartInfo { Slot = CarSlot.Engine, Name = "Дизель", Mass = 130f, Torque = 0.65f, Speed = -0.1f, Burn = -0.3f, Seconds = 8f } },
        { CarPartKind.ExtraTank, new CarPartInfo { Slot = CarSlot.Tank, Name = "Дополнительный бак", Mass = 25f, TankLiters = 30f, Seconds = 4f } }
    };

    public static float CarryMass(CarPartKind kind)
    {
        return Mathf.Min(Info[kind].Mass, MaxCarryMass);
    }

    public static string SlotName(CarSlot slot)
    {
        switch (slot)
        {
            case CarSlot.Front: return "Перед";
            case CarSlot.Roof: return "Крыша";
            case CarSlot.Sides: return "Борта";
            case CarSlot.Rear: return "Зад";
            case CarSlot.Wheels: return "Колёса";
            case CarSlot.Engine: return "Мотор";
            default: return "Бак";
        }
    }
}

// Как деталь выглядит. Одна и та же модель лежит на брошенной машине, таскается в руках и стоит на нашей.
public static class CarPartModels
{
    static readonly Color Steel = new Color(0.2f, 0.2f, 0.2f);
    static readonly Color DarkSteel = new Color(0.07f, 0.07f, 0.075f);
    static readonly Color Paint = new Color(0.16f, 0.17f, 0.12f);
    static readonly Color Rubber = new Color(0.02f, 0.02f, 0.02f);
    static readonly Color Glass = new Color(0.55f, 0.55f, 0.5f);
    static readonly Color TankRed = new Color(0.3f, 0.07f, 0.05f);

    public const int SurfaceRust = 10;

    // Ржавчина считается от самой детали: на брошенной машине и в руках рисунок не плывёт.
    static Material M(Color color, float smoothness, bool mounted)
    {
        return mounted ? ProceduralModels.Lit(color, 0.7f, 0f, smoothness) : ProceduralModels.Lit(color, 0f, 0f, smoothness, SurfaceRust);
    }

    static void Box(Transform parent, Vector3 position, Vector3 size, Material material, Quaternion? rotation = null)
    {
        ProceduralModels.Part(parent, "PartBox", PrimitiveType.Cube, position, size, material, rotation);
    }

    static void Tube(Transform parent, Vector3 from, Vector3 to, float radius, Material material)
    {
        Vector3 along = to - from;
        ProceduralModels.Part(parent, "PartTube", PrimitiveType.Cylinder, (from + to) * 0.5f, new Vector3(radius * 2f, along.magnitude * 0.5f, radius * 2f), material, Quaternion.FromToRotation(Vector3.up, along));
    }

    public static void Build(Transform root, CarPartKind kind, bool mounted)
    {
        Material steel = M(Steel, 0.35f, mounted);
        Material dark = M(DarkSteel, 0.25f, mounted);
        Material paint = M(Paint, 0.2f, mounted);

        switch (kind)
        {
            case CarPartKind.BullBar:
                foreach (float s in new[] { -1f, 1f })
                {
                    Tube(root, new Vector3(s * 0.38f, -0.25f, 0f), new Vector3(s * 0.38f, 0.28f, 0.04f), 0.03f, dark);
                    Tube(root, new Vector3(s * 0.38f, 0.28f, 0.04f), new Vector3(s * 0.82f, 0.12f, -0.08f), 0.024f, dark);
                    Tube(root, new Vector3(s * 0.82f, 0.12f, -0.08f), new Vector3(s * 0.86f, -0.22f, -0.1f), 0.024f, dark);
                }
                Tube(root, new Vector3(-0.38f, 0.28f, 0.04f), new Vector3(0.38f, 0.28f, 0.04f), 0.03f, dark);
                Tube(root, new Vector3(-0.38f, 0.02f, 0.02f), new Vector3(0.38f, 0.02f, 0.02f), 0.024f, dark);
                break;

            case CarPartKind.RamPlow:
                for (int i = 0; i < 5; i++)
                {
                    float y = -0.32f + i * 0.17f;
                    Box(root, new Vector3(0f, y, 0.12f - Mathf.Abs(i - 2) * 0.03f), new Vector3(2f - i * 0.03f, 0.16f, 0.03f), paint, Quaternion.Euler(-14f + i * 7f, 0f, 0f));
                }
                Box(root, new Vector3(0f, -0.42f, 0.2f), new Vector3(2.02f, 0.05f, 0.05f), steel);
                foreach (float s in new[] { -0.5f, 0f, 0.5f })
                    Box(root, new Vector3(s, -0.05f, -0.03f), new Vector3(0.09f, 0.6f, 0.2f), dark);
                break;

            case CarPartKind.WinchBumper:
                Box(root, new Vector3(0f, -0.14f, 0f), new Vector3(1.86f, 0.22f, 0.18f), dark);
                Box(root, new Vector3(0f, -0.04f, 0.04f), new Vector3(0.6f, 0.14f, 0.2f), steel);
                ProceduralModels.Part(root, "Drum", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0.04f), new Vector3(0.17f, 0.24f, 0.17f), M(DarkSteel * 2f, 0.4f, mounted), Quaternion.Euler(0f, 0f, 90f));
                foreach (float s in new[] { -1f, 1f })
                {
                    Box(root, new Vector3(s * 0.28f, 0.08f, 0.04f), new Vector3(0.03f, 0.22f, 0.2f), steel);
                    Tube(root, new Vector3(s * 0.7f, -0.14f, 0.1f), new Vector3(s * 0.7f, -0.14f, 0.2f), 0.024f, steel);
                }
                break;

            case CarPartKind.RoofLights:
                Tube(root, new Vector3(-0.72f, 0.1f, 0f), new Vector3(0.72f, 0.1f, 0f), 0.022f, dark);
                foreach (float s in new[] { -0.74f, 0.74f })
                    Box(root, new Vector3(s, 0f, 0f), new Vector3(0.05f, 0.2f, 0.09f), dark);
                foreach (float s in new[] { -0.54f, -0.18f, 0.18f, 0.54f })
                {
                    ProceduralModels.Part(root, "RoofLamp", PrimitiveType.Cylinder, new Vector3(s, 0.17f, 0.05f), new Vector3(0.17f, 0.05f, 0.17f), dark, Quaternion.Euler(90f, 0f, 0f));
                    ProceduralModels.Part(root, "RoofLampGlass", PrimitiveType.Cylinder, new Vector3(s, 0.17f, 0.1f), new Vector3(0.14f, 0.005f, 0.14f), ProceduralModels.Lit(Glass, 0f, 0f, 0.85f), Quaternion.Euler(90f, 0f, 0f));
                }
                break;

            case CarPartKind.RoofCage:
                foreach (float s in new[] { -1f, 1f })
                {
                    Tube(root, new Vector3(s * 0.8f, 0f, -1f), new Vector3(s * 0.8f, 0f, 0.9f), 0.024f, dark);
                    Tube(root, new Vector3(s * 0.8f, 0f, -1f), new Vector3(s * 0.65f, 0.24f, -0.8f), 0.02f, dark);
                    Tube(root, new Vector3(s * 0.8f, 0f, 0.9f), new Vector3(s * 0.65f, 0.24f, 0.7f), 0.02f, dark);
                    Tube(root, new Vector3(s * 0.65f, 0.24f, -0.8f), new Vector3(s * 0.65f, 0.24f, 0.7f), 0.024f, dark);
                }
                foreach (float z in new[] { -0.8f, -0.05f, 0.7f })
                    Tube(root, new Vector3(-0.65f, 0.24f, z), new Vector3(0.65f, 0.24f, z), 0.02f, dark);
                break;

            case CarPartKind.SideArmor:
                for (int i = 0; i < 3; i++)
                    Box(root, new Vector3(0f, -0.26f + i * 0.26f, 0f), new Vector3(0.025f, 0.24f, 2.1f - i * 0.1f), i == 1 ? paint : steel);
                for (int i = 0; i < 8; i++)
                    ProceduralModels.Part(root, "Rivet", PrimitiveType.Sphere, new Vector3(0.018f, -0.26f, -0.9f + i * 0.26f), Vector3.one * 0.03f, dark);
                break;

            case CarPartKind.RearBumper:
                Box(root, new Vector3(0f, 0f, 0f), new Vector3(1.86f, 0.17f, 0.12f), dark);
                Box(root, new Vector3(0f, -0.11f, -0.03f), new Vector3(0.3f, 0.06f, 0.16f), steel);
                ProceduralModels.Part(root, "TowBall", PrimitiveType.Sphere, new Vector3(0f, -0.07f, -0.14f), Vector3.one * 0.07f, steel);
                break;

            case CarPartKind.JerryRack:
                Box(root, new Vector3(0f, -0.26f, -0.02f), new Vector3(0.8f, 0.04f, 0.2f), dark);
                foreach (float s in new[] { -0.2f, 0.2f })
                {
                    Box(root, new Vector3(s, 0f, -0.02f), new Vector3(0.32f, 0.5f, 0.16f), M(TankRed, 0.3f, mounted));
                    Box(root, new Vector3(s, 0.28f, -0.02f), new Vector3(0.16f, 0.03f, 0.03f), M(TankRed, 0.3f, mounted));
                }
                Box(root, new Vector3(0f, 0.08f, -0.06f), new Vector3(0.84f, 0.03f, 0.16f), dark);
                break;

            case CarPartKind.MudTyres:
            case CarPartKind.WideTyres:
                BuildTyreItem(root, kind == CarPartKind.MudTyres, mounted);
                break;

            case CarPartKind.TunedEngine:
            case CarPartKind.DieselEngine:
                BuildEngine(root, kind == CarPartKind.DieselEngine, mounted);
                break;

            case CarPartKind.ExtraTank:
                Box(root, Vector3.zero, new Vector3(0.62f, 0.2f, 0.72f), steel);
                foreach (float z in new[] { -0.22f, 0.22f })
                    Box(root, new Vector3(0f, 0f, z), new Vector3(0.64f, 0.22f, 0.04f), dark);
                ProceduralModels.Part(root, "TankNeck", PrimitiveType.Cylinder, new Vector3(0.2f, 0.13f, 0f), new Vector3(0.09f, 0.04f, 0.09f), dark);
                break;
        }
    }

    static void BuildTyreItem(Transform root, bool lugs, bool mounted)
    {
        Material rubber = ProceduralModels.Lit(Rubber, mounted ? 0.3f : 0f, 0f, 0.15f);
        var tyre = new GameObject("TyreItem");
        tyre.transform.SetParent(root, false);
        tyre.transform.localScale = new Vector3(0.3f, 0.86f, 0.86f);
        tyre.AddComponent<MeshFilter>().sharedMesh = RuntimeBootstrapper.TyreMesh;
        tyre.AddComponent<MeshRenderer>().sharedMaterial = rubber;
        ProceduralModels.Part(root, "Rim", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.52f, 0.13f, 0.52f), M(Steel, 0.35f, mounted), Quaternion.Euler(0f, 0f, 90f));
        if (!lugs)
            return;
        AddLugs(root, 0.43f, 0.3f, rubber);
    }

    // Грунтозацепы по кругу колеса: и у детали в руках, и на самой машине.
    public static void AddLugs(Transform root, float radius, float width, Material rubber)
    {
        for (int i = 0; i < 14; i++)
        {
            float angle = i * Mathf.PI * 2f / 14f;
            var position = new Vector3(0f, Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
            ProceduralModels.Part(root, "Lug", PrimitiveType.Cube, position, new Vector3(width, 0.03f, 0.05f), rubber, Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, i % 2 == 0 ? 12f : -12f));
        }
    }

    static void BuildEngine(Transform root, bool diesel, bool mounted)
    {
        Material block = M(diesel ? new Color(0.09f, 0.14f, 0.1f) : new Color(0.14f, 0.09f, 0.07f), 0.3f, mounted);
        Material dark = M(DarkSteel, 0.25f, mounted);
        Box(root, new Vector3(0f, -0.06f, 0f), new Vector3(0.42f, 0.34f, 0.62f), block);
        Box(root, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.09f, 0.56f), dark);
        Box(root, new Vector3(0f, -0.26f, 0f), new Vector3(0.36f, 0.08f, 0.58f), dark);
        ProceduralModels.Part(root, "Pulley", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0.33f), new Vector3(0.2f, 0.02f, 0.2f), M(Steel, 0.4f, mounted), Quaternion.Euler(90f, 0f, 0f));
        int cylinders = diesel ? 6 : 4;
        for (int i = 0; i < cylinders; i++)
        {
            float z = -0.22f + i * 0.44f / (cylinders - 1);
            ProceduralModels.Part(root, "HeadBolt", PrimitiveType.Cylinder, new Vector3(0.1f, 0.21f, z), new Vector3(0.05f, 0.02f, 0.05f), M(Steel, 0.4f, mounted));
            Tube(root, new Vector3(0.2f, 0.06f, z), new Vector3(0.28f, -0.02f, z), 0.016f, dark);
        }
        Tube(root, new Vector3(0.28f, -0.02f, -0.24f), new Vector3(0.28f, -0.02f, 0.24f), 0.024f, dark);
    }
}

// Лежит на земле, таскается в руках и ставится на машину зажатой G.
public class CarPartItem : MonoBehaviour
{
    public const string NamePrefix = "Part_";

    public CarPartKind Kind;
    public CarPartInfo Info => CarParts.Info[Kind];

    public static string MakeName(CarPartKind kind, string owner)
    {
        return NamePrefix + (int)kind + "_" + owner;
    }

    public static bool TryParseKind(string name, out CarPartKind kind)
    {
        kind = default;
        string[] parts = name.Split('_');
        if (parts.Length < 2 || parts[0] + "_" != NamePrefix || !int.TryParse(parts[1], out int value))
            return false;
        kind = (CarPartKind)value;
        return true;
    }

    public static GameObject Create(string name, CarPartKind kind, Vector3 position, Quaternion rotation)
    {
        var root = new GameObject(name) { tag = InteractionRay.PickupableTag };
        root.transform.SetPositionAndRotation(position, rotation);
        CarPartModels.Build(root.transform, kind, false);

        var box = root.AddComponent<BoxCollider>();
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
        {
            Bounds local = new Bounds(root.transform.InverseTransformPoint(part.bounds.center), part.bounds.size);
            if (first)
                bounds = local;
            else
                bounds.Encapsulate(local);
            first = false;
        }
        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size, Vector3.one * 0.08f);

        var body = root.AddComponent<Rigidbody>();
        body.mass = CarParts.CarryMass(kind);
        root.AddComponent<ImpactSound>();
        root.AddComponent<CarPartItem>().Kind = kind;
        return root;
    }
}
