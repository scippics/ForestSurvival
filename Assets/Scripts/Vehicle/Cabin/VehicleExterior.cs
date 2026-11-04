using UnityEngine;

// Всё, что делает из двух ящиков старый экспедиционный внедорожник: арки, уплотнители, кенгурятник,
// шноркель, багажник с поклажей. Коллайдеров здесь нет: форма и масса машины от этого не зависят.
public static class VehicleExterior
{
    const float WheelTrack = 0.95f;
    const float WheelBase = 1.4f;
    const float WheelHeight = -0.35f;

    static readonly Color Paint = new Color(0.17f, 0.19f, 0.12f);
    static readonly Color Dark = new Color(0.04f, 0.04f, 0.042f);
    static readonly Color Rubber = new Color(0.018f, 0.018f, 0.018f);
    static readonly Color Steel = new Color(0.26f, 0.26f, 0.26f);
    static readonly Color Chrome = new Color(0.55f, 0.55f, 0.53f);
    static readonly Color Amber = new Color(0.55f, 0.28f, 0.03f);
    static readonly Color Tarp = new Color(0.17f, 0.17f, 0.11f);
    static readonly Color Strap = new Color(0.05f, 0.04f, 0.03f);
    static readonly Color Wood = new Color(0.25f, 0.17f, 0.1f);
    static readonly Color CanColor = new Color(0.17f, 0.05f, 0.04f);

    public const float WiperRestAngle = 66f;
    public const float BladeInner = 0.08f;
    public const float BladeOuter = 0.58f;

    public static readonly System.Collections.Generic.List<Transform> Wipers = new System.Collections.Generic.List<Transform>();

    static Mesh arch;
    static Mesh ring;
    static Mesh bundle;

    static Material Body(Color color)
    {
        return ProceduralModels.Lit(color, 1f, 0f, 0.28f);
    }

    static Material Clean(Color color, float smoothness = 0.3f)
    {
        return ProceduralModels.Lit(color, 0.15f, 0f, smoothness);
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, Quaternion? rotation = null)
    {
        ProceduralModels.Part(parent, name, PrimitiveType.Cube, position, size, material, rotation);
    }

    static void Tube(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
    {
        Vector3 along = to - from;
        ProceduralModels.Part(parent, name, PrimitiveType.Cylinder, (from + to) * 0.5f, new Vector3(radius * 2f, along.magnitude * 0.5f, radius * 2f), material, Quaternion.FromToRotation(Vector3.up, along));
    }

    public static void Build(Transform vehicle)
    {
        if (arch == null)
        {
            arch = ForestMeshes.BuildArc(0.53f, 0.034f, 22f, 158f);
            ring = ForestMeshes.BuildTorus(0.135f, 0.016f);
            bundle = ForestMeshes.BuildLathe("Bundle", new[]
            {
                new Vector2(-0.46f, 0f), new Vector2(-0.45f, 0.13f), new Vector2(-0.3f, 0.19f), new Vector2(0.28f, 0.2f), new Vector2(0.44f, 0.14f), new Vector2(0.46f, 0f)
            }, 12, 1f, 0.72f);
        }

        foreach (float side in new[] { -1f, 1f })
        {
            BuildSide(vehicle, side);
            foreach (float z in new[] { WheelBase, -WheelBase })
                ProceduralModels.MeshPart(vehicle, "Arch", arch, new Vector3(side * (WheelTrack - 0.02f), WheelHeight + 0.02f, z), new Vector3(1f, 0.92f, 4.2f), Body(Paint * 0.75f), Quaternion.Euler(0f, 90f, 0f));
        }

        BuildGlassSeals(vehicle);
        BuildFront(vehicle);
        BuildHood(vehicle);
        BuildRear(vehicle);
        BuildRoof(vehicle);
        BuildUnderbody(vehicle);
    }

    // Высокая подвеска открывает низ: мосты, рессоры, карданы и раздатка должны там быть.
    static void BuildUnderbody(Transform vehicle)
    {
        Material iron = Clean(Dark, 0.2f);
        const float axleY = -0.6f;
        foreach (float z in new[] { WheelBase, -WheelBase })
        {
            Tube(vehicle, "Axle", new Vector3(-0.82f, axleY, z), new Vector3(0.82f, axleY, z), 0.042f, iron);
            ProceduralModels.Part(vehicle, "Differential", PrimitiveType.Sphere, new Vector3(0.05f, axleY, z), new Vector3(0.26f, 0.22f, 0.24f), iron);
            foreach (float side in new[] { -1f, 1f })
            {
                // Рессора и амортизатор между мостом и рамой.
                Box(vehicle, "LeafSpring", new Vector3(side * 0.56f, axleY + 0.08f, z), new Vector3(0.07f, 0.035f, 0.82f), iron);
                Box(vehicle, "LeafSpring", new Vector3(side * 0.56f, axleY + 0.045f, z), new Vector3(0.07f, 0.03f, 0.55f), iron);
                Tube(vehicle, "Shock", new Vector3(side * 0.66f, axleY + 0.02f, z + 0.1f), new Vector3(side * 0.62f, -0.44f, z + 0.16f), 0.022f, Clean(Steel, 0.4f));
            }
        }
        Tube(vehicle, "DriveShaft", new Vector3(0.05f, axleY + 0.02f, WheelBase - 0.1f), new Vector3(0.05f, -0.53f, 0.1f), 0.028f, iron);
        Tube(vehicle, "DriveShaft", new Vector3(0.05f, -0.53f, -0.2f), new Vector3(0.05f, axleY + 0.02f, -WheelBase + 0.1f), 0.028f, iron);
        Box(vehicle, "TransferCase", new Vector3(0.05f, -0.53f, -0.05f), new Vector3(0.34f, 0.2f, 0.42f), iron);
        Box(vehicle, "FuelTank", new Vector3(-0.32f, -0.56f, -0.9f), new Vector3(0.62f, 0.2f, 0.76f), Clean(Steel * 0.5f, 0.25f));
        foreach (float side in new[] { -1f, 1f })
            Box(vehicle, "FrameRail", new Vector3(side * 0.56f, -0.51f, 0f), new Vector3(0.07f, 0.1f, 3.9f), iron);
    }

    static void BuildSide(Transform vehicle, float side)
    {
        // Швы дверей, ручки, петли: на ровном борту взгляду не за что зацепиться.
        float x = side * 0.898f;
        Material seam = Clean(Dark);
        foreach (float z in new[] { 0.9f, -0.08f, -1.08f })
            Box(vehicle, "DoorSeam", new Vector3(x, 0.08f, z), new Vector3(0.006f, 0.64f, 0.012f), seam);
        Box(vehicle, "SillSeam", new Vector3(x, -0.24f, -0.08f), new Vector3(0.006f, 0.012f, 1.98f), seam);

        foreach (float z in new[] { 0.14f, -0.86f })
        {
            Box(vehicle, "DoorHandle", new Vector3(side * 0.905f, 0.28f, z), new Vector3(0.022f, 0.022f, 0.12f), Clean(Chrome, 0.55f));
            Box(vehicle, "HandlePlate", new Vector3(side * 0.9f, 0.28f, z), new Vector3(0.006f, 0.05f, 0.16f), Clean(Steel));
        }
        foreach (float y in new[] { 0.2f, -0.1f })
            Box(vehicle, "Hinge", new Vector3(side * 0.905f, y, 0.82f), new Vector3(0.02f, 0.05f, 0.07f), Body(Paint * 0.7f));

        // Молдинг вдоль борта с заклёпками и заплата на пороге.
        Box(vehicle, "Rubbing", new Vector3(side * 0.908f, -0.02f, -0.08f), new Vector3(0.018f, 0.045f, 1.96f), Clean(Rubber, 0.2f));
        for (int i = 0; i < 9; i++)
            ProceduralModels.Part(vehicle, "Rivet", PrimitiveType.Sphere, new Vector3(side * 0.9f, -0.17f, 0.82f - i * 0.23f), Vector3.one * 0.016f, Body(Paint * 0.85f));

        // Брызговики за колёсами.
        foreach (float z in new[] { WheelBase - 0.55f, -WheelBase - 0.55f })
            Box(vehicle, "MudFlap", new Vector3(side * 0.94f, -0.56f, z), new Vector3(0.26f, 0.3f, 0.015f), Clean(Rubber, 0.1f));

        // Поворотники на крыльях.
        Box(vehicle, "SideMarker", new Vector3(side * 0.84f, 0.09f, 1.86f), new Vector3(0.015f, 0.035f, 0.09f), Clean(Amber, 0.6f));

        // Подножка на кронштейнах.
        foreach (float z in new[] { 0.45f, -0.65f })
            Box(vehicle, "StepBracket", new Vector3(side * 0.9f, -0.34f, z), new Vector3(0.14f, 0.03f, 0.04f), Clean(Dark));
        for (int i = 0; i < 6; i++)
            Box(vehicle, "StepTread", new Vector3(side * 0.96f, -0.296f, 0.5f - i * 0.24f), new Vector3(0.12f, 0.008f, 0.03f), Clean(Steel));
    }

    static void BuildGlassSeals(Transform vehicle)
    {
        Material rubber = Clean(Rubber, 0.25f);

        // Лобовое и заднее стекло в резинке.
        foreach (float z in new[] { 0.985f, -1.185f })
        {
            foreach (float y in new[] { 0.2f, 0.86f })
                Box(vehicle, "GlassSeal", new Vector3(0f, y, z), new Vector3(1.72f, 0.03f, 0.03f), rubber);
            foreach (float x in new[] { -0.84f, 0.84f })
                Box(vehicle, "GlassSeal", new Vector3(x, 0.53f, z), new Vector3(0.03f, 0.68f, 0.03f), rubber);
        }

        // Дворники на осях у нижней кромки: в покое лежат наклонно и видны над торпедой.
        Wipers.Clear();
        foreach (float x in new[] { -0.28f, 0.48f })
        {
            var pivot = new GameObject("WiperPivot").transform;
            pivot.SetParent(vehicle, false);
            pivot.localPosition = new Vector3(x, 0.215f, 0.995f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, WiperRestAngle);
            Tube(pivot, "WiperArm", Vector3.zero, new Vector3(0f, 0.33f, 0f), 0.006f, Clean(Dark));
            Box(pivot, "WiperBlade", new Vector3(0f, 0.33f, 0.006f), new Vector3(0.02f, 0.5f, 0.016f), rubber);
            Wipers.Add(pivot);
        }

        // Боковые стёкла, разделённые стойкой между дверями.
        foreach (float side in new[] { -1f, 1f })
        {
            foreach (float y in new[] { 0.36f, 0.86f })
                Box(vehicle, "SideSeal", new Vector3(side * 0.878f, y, -0.1f), new Vector3(0.02f, 0.025f, 2.12f), rubber);
            Box(vehicle, "MidPillar", new Vector3(side * 0.878f, 0.61f, -0.08f), new Vector3(0.022f, 0.5f, 0.06f), Body(Paint));

            // Форточка у передней стойки.
            Box(vehicle, "VentWindow", new Vector3(side * 0.878f, 0.61f, 0.68f), new Vector3(0.02f, 0.5f, 0.022f), rubber);
        }
    }

    // Щётки седана лежат на наклонном стекле: узел наклонён вместе с ним, а сами щётки качаются как обычно.
    public static void BuildWipers(Transform vehicle)
    {
        Material rubber = Clean(Rubber, 0.25f);
        var mount = new GameObject("WiperMount").transform;
        mount.SetParent(vehicle, false);
        mount.localPosition = SedanSkin.Point(0f, 0.985f, 1.36f);
        mount.localRotation = Quaternion.Euler(-49f, 0f, 0f);

        Wipers.Clear();
        foreach (float x in new[] { -0.28f, 0.48f })
        {
            var pivot = new GameObject("WiperPivot").transform;
            pivot.SetParent(mount, false);
            pivot.localPosition = new Vector3(x, 0f, 0.01f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, WiperRestAngle);
            Tube(pivot, "WiperArm", Vector3.zero, new Vector3(0f, 0.33f, 0f), 0.006f, Clean(Dark));
            Box(pivot, "WiperBlade", new Vector3(0f, 0.33f, 0.006f), new Vector3(0.02f, 0.5f, 0.016f), rubber);
            Wipers.Add(pivot);
        }
    }

    static void BuildFront(Transform vehicle)
    {
        Material tube = Clean(Dark, 0.25f);

        // Кольца фар и подфарники.
        foreach (float side in new[] { -1f, 1f })
        {
            ProceduralModels.MeshPart(vehicle, "LampRing", ring, new Vector3(side * 0.62f, -0.02f, 2.066f), Vector3.one, Clean(Chrome, 0.7f));
            Box(vehicle, "TurnLamp", new Vector3(side * 0.62f, -0.17f, 2.062f), new Vector3(0.09f, 0.045f, 0.012f), Clean(Amber, 0.65f));
        }

        // Шильдик.
        Box(vehicle, "Badge", new Vector3(0f, 0.1f, 2.058f), new Vector3(0.16f, 0.03f, 0.008f), Clean(Chrome, 0.7f));

        // Кенгурятник из труб с креплением к бамперу.
        foreach (float side in new[] { -1f, 1f })
        {
            Tube(vehicle, "BullBarPost", new Vector3(side * 0.38f, -0.27f, 2.2f), new Vector3(side * 0.38f, 0.2f, 2.25f), 0.024f, tube);
            Tube(vehicle, "BullBarSide", new Vector3(side * 0.38f, 0.2f, 2.25f), new Vector3(side * 0.84f, 0.08f, 2.14f), 0.02f, tube);
            Tube(vehicle, "BullBarLow", new Vector3(side * 0.84f, 0.08f, 2.14f), new Vector3(side * 0.88f, -0.25f, 2.13f), 0.02f, tube);
            Tube(vehicle, "TowHook", new Vector3(side * 0.56f, -0.33f, 2.13f), new Vector3(side * 0.56f, -0.33f, 2.27f), 0.018f, tube);

            // Пара дополнительных фар на дуге.
            ProceduralModels.Part(vehicle, "SpotLamp", PrimitiveType.Cylinder, new Vector3(side * 0.2f, 0.21f, 2.26f), new Vector3(0.13f, 0.03f, 0.13f), Clean(Dark, 0.4f), Quaternion.Euler(90f, 0f, 0f));
            ProceduralModels.Part(vehicle, "SpotGlass", PrimitiveType.Cylinder, new Vector3(side * 0.2f, 0.21f, 2.292f), new Vector3(0.105f, 0.003f, 0.105f), Clean(Chrome, 0.85f), Quaternion.Euler(90f, 0f, 0f));
        }
        Tube(vehicle, "BullBarTop", new Vector3(-0.38f, 0.2f, 2.25f), new Vector3(0.38f, 0.2f, 2.25f), 0.024f, tube);
        Tube(vehicle, "BullBarMid", new Vector3(-0.38f, -0.03f, 2.227f), new Vector3(0.38f, -0.03f, 2.227f), 0.018f, tube);
    }

    static void BuildHood(Transform vehicle)
    {
        Material panel = Body(Paint * 1.08f);
        foreach (float side in new[] { -1f, 1f })
        {
            // Выштамповки и защёлки капота.
            Box(vehicle, "HoodRib", new Vector3(side * 0.42f, 0.2f, 1.44f), new Vector3(0.05f, 0.012f, 0.88f), panel);
            Box(vehicle, "HoodSeam", new Vector3(side * 0.682f, 0.178f, 1.44f), new Vector3(0.008f, 0.012f, 0.92f), Clean(Dark));
            Box(vehicle, "HoodLatch", new Vector3(side * 0.67f, 0.11f, 1.95f), new Vector3(0.018f, 0.12f, 0.035f), Clean(Dark));
            ProceduralModels.Part(vehicle, "Washer", PrimitiveType.Sphere, new Vector3(side * 0.22f, 0.205f, 1.02f), Vector3.one * 0.018f, Clean(Dark));

            // Жалюзи по бокам капота.
            for (int i = 0; i < 5; i++)
                Box(vehicle, "HoodVent", new Vector3(side * 0.834f, 0.03f, 1.72f - i * 0.05f), new Vector3(0.006f, 0.13f, 0.018f), Clean(Dark));
        }
        Box(vehicle, "HoodCenter", new Vector3(0f, 0.2f, 1.44f), new Vector3(0.24f, 0.012f, 0.88f), panel);

        // Шноркель справа по стойке, с колпаком над крышей.
        Material pipe = Clean(Dark, 0.2f);
        Tube(vehicle, "SnorkelLow", new Vector3(0.84f, 0.14f, 1.1f), new Vector3(0.93f, 0.2f, 1.03f), 0.038f, pipe);
        Tube(vehicle, "Snorkel", new Vector3(0.93f, 0.2f, 1.03f), new Vector3(0.93f, 1.08f, 1.03f), 0.038f, pipe);
        ProceduralModels.Part(vehicle, "SnorkelCap", PrimitiveType.Cylinder, new Vector3(0.93f, 1.12f, 1.05f), new Vector3(0.105f, 0.05f, 0.105f), pipe, Quaternion.Euler(30f, 0f, 0f));
    }

    static void BuildRear(Transform vehicle)
    {
        Material pipe = Clean(Dark, 0.2f);

        // Выхлопная труба и лист рессоры, видные снизу.
        Tube(vehicle, "Exhaust", new Vector3(0.52f, -0.42f, -1.1f), new Vector3(0.56f, -0.42f, -2.2f), 0.03f, pipe);

        // Крышка бака: сюда герой и льёт бензин.
        ProceduralModels.Part(vehicle, "FuelCap", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.2f, -2.042f), new Vector3(0.11f, 0.012f, 0.11f), Clean(Steel, 0.45f), Quaternion.Euler(90f, 0f, 0f));

        // Петли и ручка задней двери.
        foreach (float y in new[] { 0.16f, -0.08f })
            Box(vehicle, "RearHinge", new Vector3(-0.74f, y, -2.04f), new Vector3(0.09f, 0.05f, 0.02f), Clean(Dark));
        Box(vehicle, "RearHandle", new Vector3(0.68f, 0.06f, -2.04f), new Vector3(0.1f, 0.022f, 0.022f), Clean(Chrome, 0.55f));

        // Кронштейн запаски.
        Box(vehicle, "SpareMount", new Vector3(0f, 0.3f, -2.07f), new Vector3(0.5f, 0.06f, 0.04f), Clean(Dark));
    }

    static void BuildRoof(Transform vehicle)
    {
        Material frame = Clean(Dark, 0.25f);

        // Экспедиционный багажник: рама на стойках и поклажа под ремнями.
        const float y = 1.02f;
        foreach (float side in new[] { -1f, 1f })
        {
            Tube(vehicle, "RackRail", new Vector3(side * 0.72f, y, -1.1f), new Vector3(side * 0.72f, y, 0.9f), 0.016f, frame);
            foreach (float z in new[] { -0.95f, -0.1f, 0.75f })
                Tube(vehicle, "RackLeg", new Vector3(side * 0.74f, 0.92f, z), new Vector3(side * 0.72f, y, z), 0.012f, frame);
        }
        for (int i = 0; i < 6; i++)
        {
            float z = -1.05f + i * 0.38f;
            Tube(vehicle, "RackBar", new Vector3(-0.72f, y, z), new Vector3(0.72f, y, z), 0.012f, frame);
        }

        // Скатанный брезент.
        ProceduralModels.MeshPart(vehicle, "Tarp", bundle, new Vector3(-0.22f, y + 0.13f, -0.55f), Vector3.one, Clean(Tarp, 0.05f), Quaternion.Euler(0f, 0f, 90f));
        foreach (float x in new[] { -0.45f, 0.02f })
            Box(vehicle, "TarpStrap", new Vector3(x, y + 0.13f, -0.55f), new Vector3(0.03f, 0.3f, 0.41f), Clean(Strap, 0.05f));

        // Лопата и две канистры, которые нельзя снять: они пустые и приварены к раме ржавчиной.
        foreach (float x in new[] { 0.3f, 0.56f })
        {
            Box(vehicle, "RoofCan", new Vector3(x, y + 0.18f, -0.5f), new Vector3(0.22f, 0.34f, 0.11f), Body(CanColor));
            Box(vehicle, "RoofCanHandle", new Vector3(x, y + 0.37f, -0.5f), new Vector3(0.1f, 0.02f, 0.02f), Body(CanColor));
        }
        Box(vehicle, "CanStrap", new Vector3(0.43f, y + 0.18f, -0.5f), new Vector3(0.52f, 0.03f, 0.125f), Clean(Strap, 0.05f));

        Tube(vehicle, "ShovelHandle", new Vector3(-0.4f, y + 0.04f, 0.72f), new Vector3(-0.4f, y + 0.04f, -0.05f), 0.016f, Clean(Wood, 0.1f));
        Box(vehicle, "ShovelBlade", new Vector3(-0.4f, y + 0.04f, -0.14f), new Vector3(0.2f, 0.012f, 0.24f), Clean(Steel, 0.2f));

        // Антенна на заднем углу.
        Tube(vehicle, "RadioAntenna", new Vector3(-0.8f, 0.9f, -1.1f), new Vector3(-0.84f, 2.1f, -1.18f), 0.005f, frame);
        ProceduralModels.Part(vehicle, "RoofBeacon", PrimitiveType.Cylinder, new Vector3(0.2f, 0.93f, 0.68f), new Vector3(0.06f, 0.02f, 0.06f), Clean(Dark));
    }
}
