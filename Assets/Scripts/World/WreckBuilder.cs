using UnityEngine;

public enum WreckStyle
{
    Sedan,
    Wagon,
    Hatch,
    Van,
    Pickup,
    Jeep,
    CanvasJeep,
    FlatTruck,
    DumpTruck,
    Logger,
    Bus,
    Ambulance,
    Tractor,
    Crawler,
    Tanker
}

public class WreckSpec
{
    public WreckStyle Style;
    public string Name;
    public float Length, Width, RoofY, FloorY, HoodLength, TailLength, WheelRadius;
    public int Axles = 2;
    public Color Paint;
    public float TankMax;
    public CarPartKind[] Parts;
}

// Пятнадцать брошенных машин одним строителем: размеры и хвост разные, а ржавчина, колёса и горловина бака общие.
public static class WreckBuilder
{
    const int Surface = CarPartModels.SurfaceRust;
    const double EmptyTankChance = 0.22;
    const double PartChance = 0.72;
    const double FlatTyreChance = 0.4;
    const double HoodOpenChance = 0.3;

    static readonly Color Dark = new Color(0.035f, 0.035f, 0.038f);
    static readonly Color Metal = new Color(0.16f, 0.16f, 0.16f);
    static readonly Color Chrome = new Color(0.36f, 0.36f, 0.34f);
    static readonly Color GlassColor = new Color(0.09f, 0.11f, 0.12f);
    static readonly Color RubberColor = new Color(0.03f, 0.03f, 0.03f);
    static readonly Color Canvas = new Color(0.17f, 0.18f, 0.11f);
    static readonly Color LogColor = new Color(0.2f, 0.14f, 0.09f);

    static WreckSpec S(WreckStyle style, string name, float length, float width, float roof, float floor, float hood, float tail, float wheel, Color paint, float tank, params CarPartKind[] parts)
    {
        return new WreckSpec { Style = style, Name = name, Length = length, Width = width, RoofY = roof, FloorY = floor, HoodLength = hood, TailLength = tail, WheelRadius = wheel, Paint = paint, TankMax = tank, Parts = parts };
    }

    public static readonly WreckSpec[] Catalog =
    {
        S(WreckStyle.Sedan, "седан", 4.3f, 1.65f, 1.42f, 0.34f, 1.15f, 1f, 0.32f, new Color(0.22f, 0.26f, 0.3f), 40f, CarPartKind.RearBumper, CarPartKind.TunedEngine),
        S(WreckStyle.Wagon, "универсал", 4.5f, 1.68f, 1.5f, 0.34f, 1.1f, 0.2f, 0.32f, new Color(0.34f, 0.28f, 0.16f), 45f, CarPartKind.RoofCage, CarPartKind.RearBumper),
        S(WreckStyle.Hatch, "хэтчбек", 3.7f, 1.55f, 1.4f, 0.3f, 0.95f, 0.15f, 0.3f, new Color(0.33f, 0.09f, 0.07f), 35f, CarPartKind.TunedEngine),
        S(WreckStyle.Van, "фургон", 4.6f, 1.9f, 2.1f, 0.42f, 0.5f, 0f, 0.36f, new Color(0.3f, 0.32f, 0.26f), 55f, CarPartKind.RoofCage, CarPartKind.ExtraTank, CarPartKind.RearBumper),
        S(WreckStyle.Pickup, "пикап", 4.9f, 1.75f, 1.75f, 0.46f, 1.2f, 1.65f, 0.38f, new Color(0.15f, 0.21f, 0.28f), 50f, CarPartKind.BullBar, CarPartKind.RoofLights, CarPartKind.JerryRack),
        S(WreckStyle.Jeep, "джип", 4f, 1.75f, 1.95f, 0.5f, 1.1f, 0.2f, 0.42f, new Color(0.2f, 0.22f, 0.13f), 60f, CarPartKind.BullBar, CarPartKind.WideTyres, CarPartKind.RoofLights),
        S(WreckStyle.CanvasJeep, "открытый вездеход", 3.8f, 1.7f, 1.85f, 0.52f, 1.05f, 0.1f, 0.42f, new Color(0.17f, 0.2f, 0.11f), 50f, CarPartKind.MudTyres, CarPartKind.SideArmor),
        S(WreckStyle.FlatTruck, "бортовой грузовик", 6.2f, 2.15f, 2.3f, 0.72f, 1.15f, 3f, 0.5f, new Color(0.22f, 0.3f, 0.2f), 110f, CarPartKind.DieselEngine, CarPartKind.ExtraTank, CarPartKind.WinchBumper),
        S(WreckStyle.DumpTruck, "самосвал", 6f, 2.2f, 2.4f, 0.78f, 1.1f, 2.9f, 0.52f, new Color(0.45f, 0.26f, 0.05f), 120f, CarPartKind.RamPlow, CarPartKind.DieselEngine),
        S(WreckStyle.Logger, "лесовоз", 7.2f, 2.25f, 2.45f, 0.8f, 1.25f, 4f, 0.54f, new Color(0.14f, 0.22f, 0.3f), 130f, CarPartKind.WinchBumper, CarPartKind.MudTyres, CarPartKind.SideArmor),
        S(WreckStyle.Bus, "автобус", 7f, 2.25f, 2.75f, 0.6f, 0.45f, 0f, 0.46f, new Color(0.45f, 0.36f, 0.14f), 90f, CarPartKind.DieselEngine, CarPartKind.ExtraTank, CarPartKind.RoofLights),
        S(WreckStyle.Ambulance, "санитарный фургон", 4.7f, 1.9f, 2.2f, 0.44f, 0.55f, 0f, 0.36f, new Color(0.55f, 0.55f, 0.5f), 55f, CarPartKind.RoofLights, CarPartKind.ExtraTank),
        S(WreckStyle.Tractor, "трактор", 3.9f, 1.6f, 2.5f, 0.75f, 1.6f, 0.5f, 0.45f, new Color(0.36f, 0.12f, 0.07f), 70f, CarPartKind.RamPlow, CarPartKind.WideTyres),
        S(WreckStyle.Crawler, "гусеничный вездеход", 5.2f, 2.3f, 2.1f, 0.62f, 1.3f, 1.2f, 0.36f, new Color(0.2f, 0.24f, 0.15f), 140f, CarPartKind.SideArmor, CarPartKind.DieselEngine, CarPartKind.RoofCage),
        S(WreckStyle.Tanker, "бензовоз", 6.6f, 2.2f, 2.35f, 0.74f, 1.15f, 3.4f, 0.5f, new Color(0.32f, 0.33f, 0.31f), 400f, CarPartKind.ExtraTank, CarPartKind.JerryRack, CarPartKind.RearBumper)
    };

    static Material M(Color color, float smoothness = 0.18f)
    {
        return ProceduralModels.Lit(color, 0f, 0f, smoothness, Surface);
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, Quaternion? rotation = null)
    {
        return ProceduralModels.Part(parent, name, PrimitiveType.Cube, position, size, material, rotation);
    }

    static void Solid(Transform parent, Vector3 center, Vector3 size)
    {
        var part = new GameObject("WreckCollider");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = center;
        part.AddComponent<BoxCollider>().size = size;
    }

    public static GameObject Spawn(string id, int specIndex, Vector3 ground, float yaw, int seed, float forcedLiters = -1f, bool forceHood = false)
    {
        WreckSpec spec = Catalog[Mathf.Abs(specIndex) % Catalog.Length];
        var random = new System.Random(seed);

        var root = new GameObject("Wreck_" + id).transform;
        Vector3 normal = TerrainNormal(ground, spec);
        Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(R(random, -2.5f, 2.5f), yaw, R(random, -3.5f, 3.5f));
        root.SetPositionAndRotation(ground + Vector3.down * R(random, 0.06f, 0.26f), tilt);

        bool hoodOpen = forceHood || random.NextDouble() < HoodOpenChance;
        foreach (CarPartKind kind in spec.Parts)
        {
            if (CarParts.Info[kind].Slot == CarSlot.Engine)
                hoodOpen = true;
        }

        BuildBody(root, spec, random, hoodOpen);
        BuildWheels(root, spec, random);

        // Горловина бака: сюда вставляется шланг.
        float belt = BeltY(spec);
        Vector3 fillerSpot = new Vector3(spec.Width * 0.5f + 0.02f, belt - 0.14f, -spec.Length * 0.27f);
        var filler = new GameObject("Filler");
        filler.tag = InteractionRay.FuelSourceTag;
        filler.transform.SetParent(root, false);
        filler.transform.localPosition = fillerSpot;
        filler.AddComponent<SphereCollider>().radius = 0.2f;
        ProceduralModels.Part(filler.transform, "FillerNeck", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.03f, 0.1f), M(Dark, 0.3f), Quaternion.Euler(0f, 0f, 90f));
        var capHinge = new GameObject("CapHinge").transform;
        capHinge.SetParent(filler.transform, false);
        capHinge.localPosition = new Vector3(0.035f, 0.05f, 0f);
        capHinge.localRotation = Quaternion.Euler(0f, 0f, 90f);
        ProceduralModels.Part(capHinge, "FillerCap", PrimitiveType.Cylinder, new Vector3(-0.05f, 0f, 0f), new Vector3(0.11f, 0.012f, 0.11f), M(Metal, 0.4f));

        float liters = forcedLiters >= 0f ? forcedLiters : random.NextDouble() < EmptyTankChance ? 0f : Mathf.Min(spec.TankMax, R(random, 6f, 60f)) * (spec.Style == WreckStyle.Tanker ? 4f : 1f);
        var car = root.gameObject.AddComponent<AbandonedCar>();
        car.Setup(id, spec, liters, filler.transform, capHinge);

        for (int i = 0; i < spec.Parts.Length; i++)
        {
            if (random.NextDouble() < PartChance || i == 0)
                AddSalvage(car, root, spec, spec.Parts[i]);
        }
        return root.gameObject;
    }

    static float R(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    static float BeltY(WreckSpec spec)
    {
        return spec.FloorY + (spec.RoofY - spec.FloorY) * 0.46f;
    }

    static Vector3 TerrainNormal(Vector3 ground, WreckSpec spec)
    {
        float step = spec.Length * 0.4f;
        float dx = OptimizedForestGenerator.TerrainHeight(ground.x + step, ground.z) - OptimizedForestGenerator.TerrainHeight(ground.x - step, ground.z);
        float dz = OptimizedForestGenerator.TerrainHeight(ground.x, ground.z + step) - OptimizedForestGenerator.TerrainHeight(ground.x, ground.z - step);
        return new Vector3(-dx, step * 2f, -dz).normalized;
    }

    static void BuildBody(Transform root, WreckSpec spec, System.Random random, bool hoodOpen)
    {
        Material paint = M(spec.Paint);
        Material paintDim = M(spec.Paint * 0.82f);
        Material dark = M(Dark);
        Material chrome = M(Chrome, 0.45f);
        Material glass = ProceduralModels.Lit(GlassColor, 0f, 0f, 0.8f);

        float L = spec.Length, W = spec.Width, F = spec.FloorY, H = spec.RoofY;
        float front = L * 0.5f, back = -L * 0.5f;
        float belt = BeltY(spec);
        float cabinFront = front - spec.HoodLength;
        float cabinBack = back + spec.TailLength;
        float cabinMid = (cabinFront + cabinBack) * 0.5f;
        float cabinLength = cabinFront - cabinBack;

        Box(root, "Chassis", new Vector3(0f, F - 0.04f, 0f), new Vector3(W * 0.72f, 0.16f, L * 0.96f), dark);
        Solid(root, new Vector3(0f, (F + belt) * 0.5f, 0f), new Vector3(W, belt - F + 0.08f, L));
        Solid(root, new Vector3(0f, (belt + H) * 0.5f, cabinMid), new Vector3(W * 0.96f, H - belt, cabinLength));

        // Перед: капот на петлях, решётка и фары.
        if (spec.HoodLength > 0.7f)
        {
            Box(root, "HoodBase", new Vector3(0f, (F + belt) * 0.5f - 0.04f, front - spec.HoodLength * 0.5f), new Vector3(W, belt - F, spec.HoodLength), paint);
            var hood = new GameObject("WreckHood").transform;
            hood.SetParent(root, false);
            hood.localPosition = new Vector3(0f, belt - 0.02f, cabinFront);
            hood.localRotation = Quaternion.Euler(hoodOpen ? -58f : 0f, 0f, 0f);
            Box(hood, "HoodLid", new Vector3(0f, 0.02f, spec.HoodLength * 0.5f), new Vector3(W * 0.92f, 0.05f, spec.HoodLength * 0.98f), paintDim);
        }
        Box(root, "Grille", new Vector3(0f, (F + belt) * 0.5f, front + 0.012f), new Vector3(W * 0.56f, (belt - F) * 0.62f, 0.02f), dark);
        for (int i = 0; i < 6; i++)
            Box(root, "GrilleBar", new Vector3(-W * 0.24f + i * W * 0.096f, (F + belt) * 0.5f, front + 0.024f), new Vector3(0.022f, (belt - F) * 0.62f, 0.02f), chrome);
        Box(root, "FrontBumper", new Vector3(0f, F + 0.04f, front + 0.04f), new Vector3(W * 1.02f, 0.11f, 0.08f), chrome);
        foreach (float side in new[] { -1f, 1f })
        {
            ProceduralModels.Part(root, "Headlamp", PrimitiveType.Cylinder, new Vector3(side * W * 0.37f, (F + belt) * 0.5f + 0.03f, front + 0.012f), new Vector3(0.2f, 0.016f, 0.2f), random.NextDouble() < 0.3 ? dark : chrome, Quaternion.Euler(90f, 0f, 0f));
            Box(root, "TailLamp", new Vector3(side * W * 0.38f, belt - 0.14f, back - 0.012f), new Vector3(0.2f, 0.1f, 0.02f), M(new Color(0.2f, 0.03f, 0.02f), 0.5f));
        }

        // Кабина: низ, стёкла с тёмным нутром, стойки и крыша с мхом.
        Box(root, "CabinLow", new Vector3(0f, (F + belt) * 0.5f, cabinMid), new Vector3(W, belt - F, cabinLength), paint);
        Box(root, "CabinGlass", new Vector3(0f, (belt + H) * 0.5f, cabinMid), new Vector3(W * 0.93f, H - belt, cabinLength * 0.97f), glass);
        Box(root, "CabinRoof", new Vector3(0f, H, cabinMid), new Vector3(W * 0.98f, 0.07f, cabinLength * 1.02f), paintDim);
        foreach (float side in new[] { -1f, 1f })
        {
            int pillars = Mathf.Max(2, Mathf.RoundToInt(cabinLength / 1.15f) + 1);
            for (int i = 0; i < pillars; i++)
            {
                float z = Mathf.Lerp(cabinBack + 0.04f, cabinFront - 0.04f, i / (pillars - 1f));
                Box(root, "Pillar", new Vector3(side * W * 0.47f, (belt + H) * 0.5f, z), new Vector3(0.07f, H - belt, 0.09f), paint);
            }
            Box(root, "DoorSeam", new Vector3(side * (W * 0.5f + 0.003f), (F + belt) * 0.5f, cabinMid), new Vector3(0.006f, belt - F, 0.015f), dark);
            Box(root, "DoorHandle", new Vector3(side * (W * 0.5f + 0.01f), belt - 0.08f, cabinMid - 0.3f), new Vector3(0.02f, 0.025f, 0.14f), chrome);
        }
        foreach (float z in new[] { cabinFront + 0.012f, cabinBack - 0.012f })
            Box(root, "GlassEnd", new Vector3(0f, (belt + H) * 0.5f, z), new Vector3(W * 0.9f, (H - belt) * 0.9f, 0.015f), glass);

        BuildTail(root, spec, random, paint, paintDim, dark, cabinBack, belt);
    }

    static void BuildTail(Transform root, WreckSpec spec, System.Random random, Material paint, Material paintDim, Material dark, float cabinBack, float belt)
    {
        float L = spec.Length, W = spec.Width, F = spec.FloorY, H = spec.RoofY, T = spec.TailLength;
        float back = -L * 0.5f;
        float tailMid = back + T * 0.5f;
        Material wood = ProceduralModels.Lit(LogColor, 0f, 0f, 0.08f, 7);

        switch (spec.Style)
        {
            case WreckStyle.Sedan:
                Box(root, "Trunk", new Vector3(0f, (F + belt) * 0.5f, tailMid), new Vector3(W, belt - F, T), paint);
                Box(root, "TrunkLid", new Vector3(0f, belt + 0.01f, tailMid), new Vector3(W * 0.9f, 0.04f, T * 0.92f), paintDim);
                break;
            case WreckStyle.Wagon:
            case WreckStyle.Hatch:
                Box(root, "Tail", new Vector3(0f, (F + belt) * 0.5f, tailMid), new Vector3(W, belt - F, T), paint);
                break;
            case WreckStyle.Van:
            case WreckStyle.Ambulance:
                if (spec.Style == WreckStyle.Ambulance)
                {
                    foreach (float side in new[] { -1f, 1f })
                        Box(root, "RedStripe", new Vector3(side * (W * 0.5f + 0.004f), belt + 0.02f, 0.1f), new Vector3(0.008f, 0.2f, L * 0.84f), M(new Color(0.4f, 0.05f, 0.04f)));
                    Box(root, "Beacon", new Vector3(0f, H + 0.09f, L * 0.3f), new Vector3(0.3f, 0.12f, 0.16f), M(new Color(0.08f, 0.14f, 0.32f), 0.6f));
                }
                break;
            case WreckStyle.Pickup:
                Box(root, "BedFloor", new Vector3(0f, F + 0.1f, tailMid), new Vector3(W, 0.1f, T), paintDim);
                foreach (float side in new[] { -1f, 1f })
                    Box(root, "BedSide", new Vector3(side * (W * 0.5f - 0.03f), (F + belt) * 0.5f + 0.05f, tailMid), new Vector3(0.05f, belt - F, T), paint);
                Box(root, "Tailgate", new Vector3(0f, (F + belt) * 0.5f + 0.05f, back + 0.02f), new Vector3(W, belt - F, 0.05f), paint);
                break;
            case WreckStyle.Jeep:
                ProceduralModels.Part(root, "SpareTyre", PrimitiveType.Cylinder, new Vector3(0f, belt + 0.12f, back - 0.14f), new Vector3(0.78f, 0.1f, 0.78f), ProceduralModels.Lit(RubberColor, 0f, 0f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
                break;
            case WreckStyle.CanvasJeep:
                Box(root, "Tarpaulin", new Vector3(0f, H + 0.04f, -0.1f), new Vector3(W * 1.02f, 0.12f, L * 0.62f), ProceduralModels.Lit(Canvas, 0f, 0f, 0.05f, 9), Quaternion.Euler(1.5f, 0f, 2f));
                break;
            case WreckStyle.FlatTruck:
                Box(root, "Bed", new Vector3(0f, F + 0.22f, tailMid), new Vector3(W, 0.12f, T), wood);
                foreach (float side in new[] { -1f, 1f })
                    Box(root, "BedBoard", new Vector3(side * (W * 0.5f - 0.03f), F + 0.52f, tailMid), new Vector3(0.05f, 0.5f, T), wood);
                Box(root, "BedBoard", new Vector3(0f, F + 0.52f, back + 0.03f), new Vector3(W, 0.5f, 0.05f), wood);
                break;
            case WreckStyle.DumpTruck:
                var bed = new GameObject("DumpBed").transform;
                bed.SetParent(root, false);
                bed.localPosition = new Vector3(0f, F + 0.3f, back + 0.2f);
                bed.localRotation = Quaternion.Euler(-(float)random.NextDouble() * 34f, 0f, 0f);
                Box(bed, "DumpFloor", new Vector3(0f, 0f, T * 0.5f), new Vector3(W, 0.1f, T), paintDim);
                foreach (float side in new[] { -1f, 1f })
                    Box(bed, "DumpSide", new Vector3(side * (W * 0.5f - 0.04f), 0.42f, T * 0.5f), new Vector3(0.08f, 0.85f, T), paint);
                Box(bed, "DumpFront", new Vector3(0f, 0.5f, T), new Vector3(W, 1f, 0.08f), paint);
                break;
            case WreckStyle.Logger:
                Box(root, "LoggerFrame", new Vector3(0f, F + 0.2f, tailMid), new Vector3(W * 0.7f, 0.14f, T), dark);
                foreach (float z in new[] { back + 0.5f, back + T - 0.6f })
                    foreach (float side in new[] { -1f, 1f })
                        Box(root, "Stake", new Vector3(side * (W * 0.5f - 0.05f), F + 0.9f, z), new Vector3(0.09f, 1.4f, 0.09f), dark);
                for (int i = 0; i < 7; i++)
                {
                    float x = (i % 4 - 1.5f) * W * 0.22f + (i >= 4 ? W * 0.11f : 0f);
                    float y = F + 0.48f + (i >= 4 ? 0.36f : 0f);
                    ProceduralModels.Part(root, "CarriedLog", PrimitiveType.Cylinder, new Vector3(x, y, tailMid), new Vector3(0.38f, T * 0.55f, 0.38f), wood, Quaternion.Euler(90f + R(random, -1f, 1f), 0f, 0f));
                }
                break;
            case WreckStyle.Bus:
                foreach (float side in new[] { -1f, 1f })
                    Box(root, "BusStripe", new Vector3(side * (W * 0.5f + 0.004f), belt - 0.12f, 0f), new Vector3(0.008f, 0.14f, L * 0.92f), M(new Color(0.6f, 0.58f, 0.5f)));
                break;
            case WreckStyle.Tractor:
                Box(root, "RearFender", new Vector3(0f, belt + 0.12f, back + 0.5f), new Vector3(W * 1.1f, 0.06f, 1.2f), paintDim);
                Box(root, "Exhaust", new Vector3(W * 0.3f, H * 0.82f, 0.9f), new Vector3(0.08f, H * 0.5f, 0.08f), dark);
                break;
            case WreckStyle.Crawler:
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(root, "Track", new Vector3(side * (W * 0.5f + 0.13f), 0.42f, 0f), new Vector3(0.34f, 0.72f, L * 0.92f), ProceduralModels.Lit(RubberColor, 0f, 0f, 0.1f));
                    for (int i = 0; i < 18; i++)
                        Box(root, "TrackLink", new Vector3(side * (W * 0.5f + 0.13f), 0.8f, -L * 0.44f + i * L * 0.052f), new Vector3(0.36f, 0.03f, 0.1f), M(Metal));
                }
                Box(root, "HullTail", new Vector3(0f, (F + belt) * 0.5f, tailMid), new Vector3(W, belt - F, T), paint);
                break;
            case WreckStyle.Tanker:
                Box(root, "TankFrame", new Vector3(0f, F + 0.2f, tailMid), new Vector3(W * 0.7f, 0.14f, T), dark);
                ProceduralModels.Part(root, "TankShell", PrimitiveType.Cylinder, new Vector3(0f, F + 0.95f, tailMid), new Vector3(1.6f, T * 0.5f, 1.25f), paint, Quaternion.Euler(90f, 0f, 0f));
                foreach (float z in new[] { back + 0.5f, back + T - 0.5f })
                    ProceduralModels.Part(root, "TankBand", PrimitiveType.Cylinder, new Vector3(0f, F + 0.95f, z), new Vector3(1.64f, 0.03f, 1.29f), dark, Quaternion.Euler(90f, 0f, 0f));
                ProceduralModels.Part(root, "TankHatch", PrimitiveType.Cylinder, new Vector3(0f, F + 1.6f, tailMid), new Vector3(0.45f, 0.05f, 0.45f), dark);
                break;
        }

        // Высокому хвосту нужен свой коллайдер: иначе сквозь него можно пройти.
        if (spec.Style == WreckStyle.Tanker || spec.Style == WreckStyle.Logger || spec.Style == WreckStyle.DumpTruck || spec.Style == WreckStyle.FlatTruck)
            Solid(root, new Vector3(0f, F + 1f, tailMid), new Vector3(W, 1.5f, T));
    }

    static void BuildWheels(Transform root, WreckSpec spec, System.Random random)
    {
        if (spec.Style == WreckStyle.Crawler)
            return;

        Material rubber = ProceduralModels.Lit(RubberColor, 0f, 0f, 0.1f);
        Material rim = ProceduralModels.Lit(Dark * 2.5f, 0f, 0f, 0.3f);
        int axles = spec.Length > 6.1f ? 3 : 2;
        float wheelbase = spec.Length * 0.33f;
        for (int axle = 0; axle < axles; axle++)
        {
            float z = axle == 0 ? wheelbase : axle == 1 ? -wheelbase : -wheelbase * 0.58f;
            float radius = spec.WheelRadius;
            if (spec.Style == WreckStyle.Tractor)
                radius = axle == 0 ? spec.WheelRadius * 0.8f : spec.WheelRadius * 1.9f;

            foreach (float side in new[] { -1f, 1f })
            {
                bool flat = random.NextDouble() < FlatTyreChance;
                var tyre = new GameObject("WreckTyre");
                tyre.transform.SetParent(root, false);
                tyre.transform.localPosition = new Vector3(side * (spec.Width * 0.5f - 0.1f), radius * (flat ? 0.78f : 1f), z);
                tyre.transform.localRotation = Quaternion.Euler(0f, R(random, -6f, 6f), 0f);
                tyre.transform.localScale = new Vector3(0.28f * (radius / 0.42f), radius * (flat ? 1.68f : 2f), radius * 2f);
                tyre.AddComponent<MeshFilter>().sharedMesh = RuntimeBootstrapper.TyreMesh;
                tyre.AddComponent<MeshRenderer>().sharedMaterial = rubber;
                ProceduralModels.Part(root, "WreckRim", PrimitiveType.Cylinder, tyre.transform.localPosition, new Vector3(radius * 1.2f, 0.15f * (radius / 0.42f), radius * 1.2f), rim, Quaternion.Euler(0f, 0f, 90f));
            }
        }
    }

    static void AddSalvage(AbandonedCar car, Transform root, WreckSpec spec, CarPartKind kind)
    {
        CarSlot slot = CarParts.Info[kind].Slot;
        float belt = BeltY(spec);
        Vector3 spot;
        Quaternion turn = Quaternion.identity;
        switch (slot)
        {
            case CarSlot.Front: spot = new Vector3(0f, spec.FloorY + 0.22f, spec.Length * 0.5f + 0.2f); break;
            case CarSlot.Roof: spot = new Vector3(0f, spec.RoofY + 0.05f, spec.Length * 0.5f - spec.HoodLength - (spec.Length - spec.HoodLength - spec.TailLength) * 0.5f); break;
            case CarSlot.Sides: spot = new Vector3(-spec.Width * 0.5f - 0.05f, belt - 0.05f, 0f); turn = Quaternion.Euler(0f, 180f, 0f); break;
            case CarSlot.Rear: spot = new Vector3(0f, spec.FloorY + 0.2f, -spec.Length * 0.5f - 0.16f); break;
            case CarSlot.Wheels: spot = new Vector3(-spec.Width * 0.5f - 0.42f, 0.5f, -spec.Length * 0.2f); turn = Quaternion.Euler(0f, 90f, 12f); break;
            case CarSlot.Engine: spot = new Vector3(0f, belt + 0.18f, spec.Length * 0.5f - spec.HoodLength * 0.5f); break;
            default: spot = new Vector3(-spec.Width * 0.5f - 0.32f, spec.FloorY + 0.1f, -spec.Length * 0.12f); break;
        }

        var holder = new GameObject("Salvage_" + kind);
        holder.transform.SetParent(root, false);
        holder.transform.SetLocalPositionAndRotation(spot, turn);
        CarPartModels.Build(holder.transform, kind, false);

        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (Renderer part in holder.GetComponentsInChildren<Renderer>())
        {
            Bounds local = new Bounds(holder.transform.InverseTransformPoint(part.bounds.center), part.bounds.size);
            if (first)
                bounds = local;
            else
                bounds.Encapsulate(local);
            first = false;
        }
        var box = holder.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size + Vector3.one * 0.04f, Vector3.one * 0.15f);

        var salvage = holder.AddComponent<SalvagePart>();
        salvage.Kind = kind;
        car.AddPart(salvage);
    }
}
