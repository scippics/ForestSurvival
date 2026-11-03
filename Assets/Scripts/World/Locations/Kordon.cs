using UnityEngine;

// Лесной кордон на стартовой поляне: дом лесника, дровяник и пустая будка. Больше тут никого и ничего нет.
// Всё стоит на плоской поляне, а привязка идёт к просеке: дорога ложится от шума, и дом всегда у неё.
public static class Kordon
{
    const float CarZ = -2f;
    const float HouseOffset = 12f;
    const float HouseZ = 4f;
    const float HalfWidth = 3.5f;
    const float HalfDepth = 2.75f;
    const float WallHeight = 2.4f;
    const float RidgeHeight = 3.7f;
    const float LogSize = 0.26f;
    const float LogStep = 0.235f;
    const float CornerOverhang = 0.22f;
    const float FloorHeight = 0.12f;
    const float DoorHalf = 0.5f;
    const float DoorHeight = 2f;
    const float DoorAjar = -24f;
    const float TableHeight = 0.78f;
    const float KennelZ = -5f;
    const float KennelOffset = 10f;
    const string StoryWreckId = "story";
    const float StoryWreckZ = -16f;
    const float StoryWreckOffset = 6.8f;
    const float StoryWreckLiters = 40f;
    const int SurfaceWood = 7;
    const int SurfaceCloth = 9;

    static readonly Color LogColor = new Color(0.2f, 0.135f, 0.085f);
    static readonly Color LogEndColor = new Color(0.26f, 0.2f, 0.13f);
    static readonly Color PlankColor = new Color(0.17f, 0.125f, 0.085f);
    static readonly Color RoofColor = new Color(0.09f, 0.095f, 0.08f);
    static readonly Color IronColor = new Color(0.05f, 0.05f, 0.055f);
    static readonly Color BlanketColor = new Color(0.17f, 0.2f, 0.13f);
    static readonly Color RadioColor = new Color(0.1f, 0.13f, 0.1f);
    static readonly Color RedColor = new Color(0.5f, 0.06f, 0.04f);
    static readonly Color WhiteColor = new Color(0.62f, 0.62f, 0.58f);
    static readonly Color WindowGlow = new Color(1f, 0.62f, 0.26f);
    static readonly Color LampColor = new Color(1f, 0.66f, 0.34f);

    // Дорога на поляне сильно виляет, поэтому каждая постройка отмеряется от неё на своей широте.
    static float Road(float z) => OptimizedForestGenerator.RoadCenterX(0, z);
    static float Side => Road(0f) > 0f ? -1f : 1f;
    static Vector3 Beside(float z, float distance) => new Vector3(Road(z) + Side * distance, 0f, z);

    public static Vector3 CarSpot => new Vector3(Road(CarZ), 1.2f, CarZ);

    // Машина встала вдоль дороги, носом туда, куда та уходит.
    public static Quaternion CarRotation => Quaternion.LookRotation(new Vector3(Road(CarZ + 6f) - Road(CarZ), 0f, 6f));
    public static Vector3 PlayerSpot => CarSpot + new Vector3(Side * 2.4f, -0.2f, 0f);
    public static Vector3 BotSpot => Beside(10f, 7.5f) + Vector3.up * 0.9f;
    public static Vector3 RadioPosition { get; private set; }

    public static void Build()
    {
        var root = new GameObject("Kordon").transform;

        BuildHouse(root);
        BuildWoodpile(root, Beside(13f, 5.5f));
        BuildKennel(root, Beside(KennelZ, KennelOffset));

        // Шланг и канистра у крыльца, а бензин только в брошенной машине дальше по просеке.
        Vector3 center = Beside(HouseZ, HouseOffset);
        Quaternion facing = Quaternion.LookRotation(new Vector3(Side, 0f, 0f));
        ProceduralModels.CreateHose("Item_Hose", center + facing * new Vector3(-1.6f, 0.15f, -HalfDepth - 0.9f)).AddComponent<FuelSiphon>();
        ProceduralModels.CreateJerrycan("Item_Jerrycan", center + facing * new Vector3(-2.4f, 0.23f, -HalfDepth - 0.7f), facing);
        WreckBuilder.Spawn(StoryWreckId, 0, Beside(StoryWreckZ, StoryWreckOffset), Side * 12f, 77, StoryWreckLiters, true);

        ProceduralModels.CreateCrate("Item_Cube_1", Beside(11f, 6.7f) + Vector3.up * 0.25f);
        ProceduralModels.CreateCrate("Item_Cube_2", Beside(12f, 7.5f) + Vector3.up * 0.25f);
        ProceduralModels.CreateCrate("Item_Cube_3", Beside(12f, 7.5f) + Vector3.up * 0.75f);
    }

    static Material Wood(Color color)
    {
        return ProceduralModels.Lit(color, 0f, 0f, 0.08f, SurfaceWood);
    }

    // Голая земля: дорога, дом, навес и тропы между ними. Дальше двор зарастает травой.
    public static float YardBare(float x, float z)
    {
        float distance = Mathf.Abs(x - OptimizedForestGenerator.RoadCenterX(0, z)) - 3.2f;
        distance = Mathf.Min(distance, RectDistance(x, z, Beside(HouseZ, HouseOffset).x, HouseZ, HalfWidth + 2.2f, HalfDepth + 2.2f));
        distance = Mathf.Min(distance, RectDistance(x, z, Beside(StoryWreckZ, StoryWreckOffset).x, StoryWreckZ, 2f, 3f));
        distance = Mathf.Min(distance, RectDistance(x, z, Beside(12.5f, 6.5f).x, 12.5f, 3.2f, 2.4f));
        distance = Mathf.Min(distance, RectDistance(x, z, Beside(KennelZ, KennelOffset).x, KennelZ, 1.3f, 1.5f));
        distance = Mathf.Min(distance, RectDistance(x, z, Beside(2f, 5.5f).x, 2f, 3f, 0.7f));

        float wobble = (Mathf.PerlinNoise(x * 0.33f + 40f, z * 0.33f + 17f) - 0.5f) * 4f;
        return 1f - Mathf.SmoothStep(0f, 3.5f, distance + wobble);
    }

    static float RectDistance(float x, float z, float centerX, float centerZ, float halfX, float halfZ)
    {
        return Mathf.Max(Mathf.Abs(x - centerX) - halfX, Mathf.Abs(z - centerZ) - halfZ);
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, Quaternion? rotation = null, bool solid = true)
    {
        return ProceduralModels.Part(parent, name, PrimitiveType.Cube, position, size, material, rotation, solid);
    }

    static void Solid(Transform parent, Vector3 center, Vector3 size)
    {
        var part = new GameObject("WallCollider");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = center;
        part.AddComponent<BoxCollider>().size = size;
    }

    // Венцы вдоль локальной X и общий коллайдер на всю стену: лишние неровности только мешали бы ходить.
    static void LogsAlongX(Transform parent, float z, float x0, float x1, float y0, float y1)
    {
        Material log = Wood(LogColor);
        float length = x1 - x0;
        float x = (x0 + x1) * 0.5f;
        for (float y = y0 + LogSize * 0.5f; y < y1; y += LogStep)
            ProceduralModels.Part(parent, "Log", PrimitiveType.Cylinder, new Vector3(x, y, z), new Vector3(LogSize, length * 0.5f, LogSize), log, Quaternion.Euler(0f, 0f, 90f));
        Solid(parent, new Vector3(x, (y0 + y1) * 0.5f, z), new Vector3(length, y1 - y0, LogSize));
    }

    static void LogsAlongZ(Transform parent, float x, float z0, float z1, float y0, float y1)
    {
        Material log = Wood(LogColor);
        float length = z1 - z0;
        float z = (z0 + z1) * 0.5f;
        for (float y = y0 + LogSize * 0.5f + LogStep * 0.5f; y < y1; y += LogStep)
            ProceduralModels.Part(parent, "Log", PrimitiveType.Cylinder, new Vector3(x, y, z), new Vector3(LogSize, length * 0.5f, LogSize), log, Quaternion.Euler(90f, 0f, 0f));
        Solid(parent, new Vector3(x, (y0 + y1) * 0.5f, z), new Vector3(LogSize, y1 - y0, length));
    }

    // Локальная -Z смотрит на дорогу: дверь и окно видно из машины.
    static void BuildHouse(Transform root)
    {
        var house = new GameObject("House").transform;
        house.SetParent(root, false);
        house.SetPositionAndRotation(Beside(HouseZ, HouseOffset), Quaternion.LookRotation(new Vector3(Side, 0f, 0f)));

        float overX = HalfWidth + CornerOverhang;
        float overZ = HalfDepth + CornerOverhang;

        // Передняя стена: дверной проём и окно.
        const float windowLeft = 1.3f;
        const float windowRight = 2.5f;
        const float sill = 0.92f;
        const float top = 1.8f;
        LogsAlongX(house, -HalfDepth, -overX, -DoorHalf, 0f, WallHeight);
        LogsAlongX(house, -HalfDepth, DoorHalf, windowLeft, 0f, WallHeight);
        LogsAlongX(house, -HalfDepth, windowRight, overX, 0f, WallHeight);
        LogsAlongX(house, -HalfDepth, -DoorHalf, DoorHalf, DoorHeight, WallHeight);
        LogsAlongX(house, -HalfDepth, windowLeft, windowRight, 0f, sill);
        LogsAlongX(house, -HalfDepth, windowLeft, windowRight, top, WallHeight);

        LogsAlongX(house, HalfDepth, -overX, overX, 0f, WallHeight);
        LogsAlongZ(house, -HalfWidth, -overZ, overZ, 0f, WallHeight);
        LogsAlongZ(house, HalfWidth, -overZ, overZ, 0f, WallHeight);

        // Фронтоны зашиты вертикальной доской до самого конька, сверху их закрывает скат.
        const float boardWidth = 0.24f;
        Material gable = Wood(PlankColor * 1.15f);
        float roofRise = RidgeHeight - WallHeight + 0.2f;
        float roofRun = HalfWidth + 0.45f;
        foreach (float z in new[] { -HalfDepth, HalfDepth })
        {
            for (float x = -HalfWidth - 0.2f; x <= HalfWidth + 0.2f; x += boardWidth)
            {
                float boardTop = WallHeight + 0.14f + roofRise * (1f - Mathf.Abs(x) / roofRun) + 0.05f;
                float bottom = WallHeight - 0.08f;
                ProceduralModels.Part(house, "GableBoard", PrimitiveType.Cube, new Vector3(x, (boardTop + bottom) * 0.5f, z), new Vector3(boardWidth - 0.012f, boardTop - bottom, 0.05f), gable);
            }
            ProceduralModels.Part(house, "GableVent", PrimitiveType.Cube, new Vector3(0f, WallHeight + 0.72f, z - 0.03f * Mathf.Sign(-z)), new Vector3(0.36f, 0.26f, 0.02f), ProceduralModels.Lit(IronColor));
        }

        // Боковые стены прикрыты карнизной доской: под скатом не должно быть щели на улицу.
        foreach (float side in new[] { -1f, 1f })
            ProceduralModels.Part(house, "Soffit", PrimitiveType.Cube, new Vector3(side * HalfWidth, WallHeight + 0.15f, 0f), new Vector3(0.28f, 0.42f, HalfDepth * 2f + 0.5f), gable);

        BuildRoof(house);
        BuildWindow(house, (windowLeft + windowRight) * 0.5f, (sill + top) * 0.5f, windowRight - windowLeft, top - sill, out Material glass);
        HouseDoor door = BuildDoor(house);

        // Пол и пологий порог: ступень капсула игрока не перешагнула бы.
        Box(house, "Floor", new Vector3(0f, FloorHeight * 0.5f, 0f), new Vector3(HalfWidth * 2f, FloorHeight, HalfDepth * 2f), Wood(PlankColor));
        for (int i = 0; i < 12; i++)
            Box(house, "FloorSeam", new Vector3(0f, FloorHeight + 0.001f, -HalfDepth + 0.46f * i), new Vector3(HalfWidth * 2f, 0.002f, 0.012f), ProceduralModels.Lit(IronColor), null, false);
        Box(house, "Porch", new Vector3(0f, 0.08f, -HalfDepth - 0.72f), new Vector3(1.6f, 0.06f, 1.2f), Wood(PlankColor), Quaternion.Euler(-4f, 0f, 0f));

        BuildFurniture(house, glass);
        door.IgnoreWalls(house);
    }

    static void BuildRoof(Transform house)
    {
        float rise = RidgeHeight - WallHeight + 0.2f;
        float run = HalfWidth + 0.45f;
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(rise * rise + run * run);
        Material boards = ProceduralModels.Lit(RoofColor, 0f, 1f, 0.1f);

        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 center = new Vector3(side * run * 0.5f, WallHeight + 0.14f + rise * 0.5f, 0f);
            Quaternion tilt = Quaternion.Euler(0f, 0f, -side * angle);
            Box(house, "RoofSlope", center, new Vector3(length, 0.09f, HalfDepth * 2f + 1.1f), boards, tilt);

            // Рейки поверх досок ловят свет и ломают ровную плоскость.
            for (int i = 0; i < 8; i++)
            {
                float z = -HalfDepth - 0.3f + i * (HalfDepth * 2f + 0.6f) / 7f;
                Box(house, "RoofBatten", center + tilt * new Vector3(0f, 0.06f, 0f) + new Vector3(0f, 0f, z), new Vector3(length, 0.035f, 0.07f), Wood(PlankColor * 0.7f), tilt, false);
            }
        }
        Box(house, "Ridge", new Vector3(0f, WallHeight + rise + 0.2f, 0f), new Vector3(0.3f, 0.07f, HalfDepth * 2f + 1.15f), ProceduralModels.Lit(RoofColor * 0.7f), null, false);

        // Труба от печки.
        ProceduralModels.Part(house, "Chimney", PrimitiveType.Cylinder, new Vector3(2.5f, 2.9f, 1.6f), new Vector3(0.16f, 1.5f, 0.16f), ProceduralModels.Lit(IronColor, 0f, 0.3f, 0.3f));
        ProceduralModels.Part(house, "ChimneyCap", PrimitiveType.Cylinder, new Vector3(2.5f, 4.45f, 1.6f), new Vector3(0.3f, 0.03f, 0.3f), ProceduralModels.Lit(IronColor));
    }

    static void BuildWindow(Transform house, float x, float y, float width, float height, out Material glass)
    {
        Material frame = Wood(PlankColor * 0.8f);
        const float z = -HalfDepth;
        foreach (float side in new[] { -1f, 1f })
        {
            Box(house, "WindowFrame", new Vector3(x + side * width * 0.5f, y, z - 0.1f), new Vector3(0.08f, height + 0.1f, 0.1f), frame, null, false);
            Box(house, "WindowFrame", new Vector3(x, y + side * height * 0.5f, z - 0.1f), new Vector3(width + 0.1f, 0.08f, 0.1f), frame, null, false);

            // Ставни распахнуты и висят на стене.
            Box(house, "Shutter", new Vector3(x + side * (width * 0.5f + 0.32f), y, z - 0.16f), new Vector3(0.56f, height + 0.05f, 0.03f), Wood(PlankColor * 1.2f), null, false);
        }
        Box(house, "WindowBar", new Vector3(x, y, z - 0.1f), new Vector3(0.04f, height, 0.04f), frame, null, false);
        Box(house, "WindowBar", new Vector3(x, y, z - 0.1f), new Vector3(width, 0.04f, 0.04f), frame, null, false);

        // Снаружи окно светится лампой и шторами, а изнутри за ним просто серое утро.
        Solid(house, new Vector3(x, y, z), new Vector3(width, height, 0.02f));
        GameObject outside = ProceduralModels.Part(house, "WindowGlass", PrimitiveType.Quad, new Vector3(x, y, z - 0.012f), new Vector3(width, height, 1f), ProceduralModels.CreateLit(new Color(0.03f, 0.035f, 0.035f)));
        glass = outside.GetComponent<Renderer>().sharedMaterial;
        glass.SetTexture("_EmissionMap", ForestTextures.LitWindow());
        GameObject inside = ProceduralModels.Part(house, "WindowInside", PrimitiveType.Quad, new Vector3(x, y, z + 0.012f), new Vector3(width, height, 1f), ProceduralModels.CreateLit(new Color(0.03f, 0.035f, 0.035f)), Quaternion.Euler(0f, 180f, 0f));
        Material grey = inside.GetComponent<Renderer>().sharedMaterial;
        grey.SetColor("_EmissionColor", new Color(0.16f, 0.2f, 0.22f));
        foreach (float side in new[] { -1f, 1f })
            Box(house, "Curtain", new Vector3(x + side * (width * 0.5f - 0.16f), y, z + 0.09f), new Vector3(0.3f, height + 0.08f, 0.03f), ProceduralModels.Lit(new Color(0.17f, 0.1f, 0.07f), 0f, 0f, 0f, SurfaceCloth), null, false);
    }

    // Настоящая дверь на петле: приоткрыта, её можно толкнуть плечом или открыть рукой.
    static HouseDoor BuildDoor(Transform house)
    {
        var door = new GameObject("Door").transform;
        door.SetParent(house, false);
        door.localPosition = new Vector3(-DoorHalf, FloorHeight, -HalfDepth);

        Material plank = Wood(PlankColor * 1.25f);
        Box(door, "DoorBacking", new Vector3(0.5f, (DoorHeight - FloorHeight) * 0.5f, 0.006f), new Vector3(0.98f, DoorHeight - FloorHeight - 0.02f, 0.03f), Wood(PlankColor * 0.7f), null, false);
        for (int i = 0; i < 5; i++)
            Box(door, "DoorPlank", new Vector3(0.1f + i * 0.2f, (DoorHeight - FloorHeight) * 0.5f, 0f), new Vector3(0.19f, DoorHeight - FloorHeight - 0.02f, 0.05f), plank, null, false);
        foreach (float y in new[] { 0.35f, 1.5f })
            Box(door, "DoorBrace", new Vector3(0.5f, y, -0.035f), new Vector3(0.96f, 0.1f, 0.025f), plank, null, false);
        ProceduralModels.Part(door, "DoorHandle", PrimitiveType.Cylinder, new Vector3(0.86f, 1f, -0.07f), new Vector3(0.03f, 0.07f, 0.03f), ProceduralModels.Lit(IronColor, 0f, 0f, 0.4f));

        var box = door.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0.5f, (DoorHeight - FloorHeight) * 0.5f, 0f);
        box.size = new Vector3(0.98f, DoorHeight - FloorHeight, 0.06f);

        var body = door.gameObject.AddComponent<Rigidbody>();
        body.mass = 22f;
        body.angularDamping = 2.5f;
        body.useGravity = false;

        var hinge = door.gameObject.AddComponent<HingeJoint>();
        hinge.axis = Vector3.up;
        hinge.anchor = Vector3.zero;
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -95f, max = 95f };

        door.rotation *= Quaternion.Euler(0f, DoorAjar, 0f);

        var control = door.gameObject.AddComponent<HouseDoor>();
        control.Setup(hinge, DoorAjar);
        return control;
    }

    static void BuildFurniture(Transform house, Material windowGlass)
    {
        Material wood = Wood(PlankColor * 1.3f);
        float floor = FloorHeight;

        // Стол у окна: на нём лампа, рация и записка.
        Vector3 table = new Vector3(1.6f, floor, -1.5f);
        Box(house, "TableTop", table + new Vector3(0f, TableHeight, 0f), new Vector3(1.5f, 0.05f, 0.8f), wood);
        foreach (float x in new[] { -0.65f, 0.65f })
        {
            foreach (float z in new[] { -0.32f, 0.32f })
                Box(house, "TableLeg", table + new Vector3(x, TableHeight * 0.5f, z), new Vector3(0.07f, TableHeight, 0.07f), wood, null, false);
        }
        Box(house, "Stool", table + new Vector3(0f, 0.25f, 0.8f), new Vector3(0.36f, 0.5f, 0.36f), wood);

        Vector3 surface = table + new Vector3(0f, TableHeight + 0.025f, 0f);

        Transform radio = Box(house, "Radio", surface + new Vector3(0.4f, 0.13f, 0.1f), new Vector3(0.44f, 0.26f, 0.28f), ProceduralModels.Lit(RadioColor, 0f, 0f, 0.25f)).transform;
        RadioPosition = radio.position;
        Box(house, "RadioPanel", surface + new Vector3(0.4f, 0.14f, -0.045f), new Vector3(0.38f, 0.2f, 0.01f), ProceduralModels.Lit(IronColor), null, false);
        foreach (float x in new[] { -0.12f, 0f, 0.12f })
            ProceduralModels.Part(house, "RadioKnob", PrimitiveType.Cylinder, surface + new Vector3(0.4f + x, 0.08f, -0.058f), new Vector3(0.04f, 0.01f, 0.04f), ProceduralModels.Lit(WhiteColor, 0f, 0f, 0.4f), Quaternion.Euler(90f, 0f, 0f));
        Box(house, "RadioScale", surface + new Vector3(0.4f, 0.19f, -0.052f), new Vector3(0.3f, 0.05f, 0.002f), ProceduralModels.Lit(LogEndColor, 0f, 0f, 0.5f), null, false);
        ProceduralModels.Part(house, "Antenna", PrimitiveType.Cylinder, surface + new Vector3(0.58f, 0.5f, 0.2f), new Vector3(0.008f, 0.25f, 0.008f), ProceduralModels.Lit(WhiteColor));

        // Керосиновая лампа: основание, колба и настоящий свет.
        Vector3 lampSpot = surface + new Vector3(-0.42f, 0f, 0.1f);
        ProceduralModels.Part(house, "LampBase", PrimitiveType.Cylinder, lampSpot + new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.04f, 0.12f), ProceduralModels.Lit(IronColor, 0f, 0f, 0.5f));
        GameObject flame = ProceduralModels.Part(house, "LampGlass", PrimitiveType.Sphere, lampSpot + new Vector3(0f, 0.17f, 0f), new Vector3(0.09f, 0.14f, 0.09f), ProceduralModels.CreateLit(new Color(0.1f, 0.07f, 0.03f)));

        var lampObject = new GameObject("LampLight");
        lampObject.transform.SetParent(house, false);
        lampObject.transform.localPosition = lampSpot + new Vector3(0f, 0.22f, 0f);
        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 8f;
        lamp.intensity = 1.3f;
        lamp.color = LampColor;
        lamp.shadows = LightShadows.Soft;
        lamp.renderMode = LightRenderMode.ForcePixel;

        // Пятно света из окна на земле: фонарь сквозь бревно не светит, а это ложится на траву.
        var spillObject = new GameObject("WindowSpill");
        spillObject.transform.SetParent(house, false);
        spillObject.transform.SetLocalPositionAndRotation(new Vector3(1.9f, 1.5f, -HalfDepth - 0.1f), Quaternion.Euler(28f, 180f, 0f));
        Light spill = spillObject.AddComponent<Light>();
        spill.type = LightType.Spot;
        spill.range = 11f;
        spill.spotAngle = 70f;
        spill.intensity = 0.9f;
        spill.color = WindowGlow;
        spill.renderMode = LightRenderMode.ForcePixel;
        ForestLights.Register(spill);

        var flicker = lampObject.AddComponent<LampFlicker>();
        flicker.Setup(lamp, spill, flame.GetComponent<Renderer>().sharedMaterial, windowGlass);

        GameObject note = ProceduralModels.CreateNote(ProceduralModels.KordonNoteName, house.TransformPoint(surface + new Vector3(-0.05f, 0.03f, -0.12f)), StoryText.KordonNoteId, StoryText.KordonNote);
        note.transform.rotation = house.rotation * Quaternion.Euler(0f, 14f, 0f);

        ProceduralModels.CreateBlueprint("Item_Blueprint", house.TransformPoint(new Vector3(-2.65f, 1.02f, 1.9f))).AddComponent<BlueprintItem>().recipeID = "fuel_filter";
        ProceduralModels.CreateBattery("Item_Battery", house.TransformPoint(new Vector3(2.9f, 0.25f, -1.9f)), house.rotation);
        ProceduralModels.CreateCells(ProceduralModels.LootPrefix + ProceduralModels.CellsKind + "_house", house.TransformPoint(surface + new Vector3(0.05f, 0.07f, 0.22f)), house.rotation);

        // Нары с одеялом у задней стены.
        Box(house, "Bunk", new Vector3(-1.9f, floor + 0.28f, 1.75f), new Vector3(2f, 0.1f, 0.85f), wood);
        foreach (float x in new[] { -0.9f, 0.9f })
            Box(house, "BunkLeg", new Vector3(-1.9f + x, floor + 0.12f, 1.75f), new Vector3(0.1f, 0.24f, 0.8f), wood, null, false);
        Box(house, "Mattress", new Vector3(-1.9f, floor + 0.38f, 1.75f), new Vector3(1.9f, 0.1f, 0.78f), ProceduralModels.Lit(BlanketColor * 1.5f, 0f, 0f, 0f, SurfaceCloth), null, false);
        Box(house, "Blanket", new Vector3(-1.6f, floor + 0.45f, 1.72f), new Vector3(1.2f, 0.05f, 0.84f), ProceduralModels.Lit(BlanketColor, 0f, 0f, 0f, SurfaceCloth), Quaternion.Euler(0f, 0f, 1.5f), false);

        // Полка над нарами с банками: одна пустая полка смотрится декорацией.
        Box(house, "Shelf", new Vector3(-2.2f, 1.45f, 2.45f), new Vector3(1.7f, 0.04f, 0.3f), wood, null, false);
        for (int i = 0; i < 5; i++)
        {
            float height = 0.1f + i % 3 * 0.03f;
            ProceduralModels.Part(house, "Jar", PrimitiveType.Cylinder, new Vector3(-2.85f + i * 0.3f, 1.47f + height, 2.45f), new Vector3(0.1f, height, 0.1f), ProceduralModels.Lit(new Color(0.16f + i * 0.03f, 0.14f, 0.08f), 0f, 0f, 0.55f));
        }

        // Печка, чайник и дрова у неё.
        Box(house, "Stove", new Vector3(2.5f, floor + 0.4f, 1.6f), new Vector3(0.75f, 0.8f, 0.75f), ProceduralModels.Lit(IronColor, 0f, 0.4f, 0.25f));
        Box(house, "StoveDoor", new Vector3(2.5f, floor + 0.32f, 1.22f), new Vector3(0.36f, 0.3f, 0.015f), ProceduralModels.Lit(IronColor * 2f, 0f, 0f, 0.4f), null, false);
        ProceduralModels.Part(house, "Kettle", PrimitiveType.Sphere, new Vector3(2.35f, floor + 0.9f, 1.5f), new Vector3(0.22f, 0.16f, 0.22f), ProceduralModels.Lit(WhiteColor * 0.5f, 0f, 0f, 0.45f));
        for (int i = 0; i < 4; i++)
            ProceduralModels.Part(house, "StoveLog", PrimitiveType.Cylinder, new Vector3(3f - i * 0.11f, floor + 0.05f, 0.75f), new Vector3(0.1f, 0.26f, 0.1f), Wood(LogEndColor), Quaternion.Euler(90f, 0f, 0f));

        // Ватник и ружейная вешалка на стене: хозяин уходил налегке.
        Box(house, "CoatHook", new Vector3(-3.3f, 1.65f, -1.4f), new Vector3(0.1f, 0.03f, 0.9f), wood, null, false);
        Box(house, "Coat", new Vector3(-3.27f, 1.2f, -1.2f), new Vector3(0.1f, 0.85f, 0.46f), ProceduralModels.Lit(BlanketColor * 0.8f, 0f, 0f, 0f, SurfaceCloth), null, false);
    }

    static void BuildWoodpile(Transform root, Vector3 center)
    {
        var pile = new GameObject("Woodpile").transform;
        pile.SetParent(root, false);
        pile.position = center;

        Material wood = Wood(LogEndColor);
        for (int row = 0; row < 5; row++)
        {
            int count = 11 - row;
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * 0.25f;
                float wobble = Mathf.Sin(i * 12.9f + row * 4.1f) * 0.05f;
                ProceduralModels.Part(pile, "Firewood", PrimitiveType.Cylinder, new Vector3(x, 0.12f + row * 0.21f, wobble), new Vector3(0.23f, 0.42f, 0.23f), wood, Quaternion.Euler(90f, 0f, 0f));
            }
        }
        Solid(pile, new Vector3(0f, 0.55f, 0f), new Vector3(2.7f, 1.1f, 0.9f));

        // Колода с топором.
        Vector3 block = new Vector3(-Side * 1.6f + 1.2f, 0.28f, -1.3f);
        GameObject stump = ProceduralModels.Part(pile, "ChopBlock", PrimitiveType.Cylinder, block, new Vector3(0.55f, 0.28f, 0.55f), wood);
        var capsule = stump.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        capsule.radius = 0.5f;
        ProceduralModels.Part(pile, "AxeHandle", PrimitiveType.Cylinder, block + new Vector3(0.13f, 0.48f, 0f), new Vector3(0.04f, 0.28f, 0.04f), Wood(LogColor), Quaternion.Euler(0f, 0f, -24f));
        Box(pile, "AxeHead", block + new Vector3(0.015f, 0.29f, 0f), new Vector3(0.2f, 0.06f, 0.03f), ProceduralModels.Lit(IronColor * 3f, 0f, 0f, 0.5f), Quaternion.Euler(0f, 0f, -24f), false);
    }

    // Будка пустая, цепь лежит на земле и уходит под неё: записка про пса начинается отсюда.
    static void BuildKennel(Transform root, Vector3 center)
    {
        var kennel = new GameObject("Kennel").transform;
        kennel.SetParent(root, false);
        kennel.SetPositionAndRotation(center, Quaternion.Euler(0f, Side * 120f, 0f));

        Material wood = Wood(PlankColor * 1.2f);
        Solid(kennel, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 1.1f));
        Box(kennel, "KennelBack", new Vector3(0f, 0.32f, 0.55f), new Vector3(0.9f, 0.64f, 0.04f), wood, null, false);
        foreach (float side in new[] { -1f, 1f })
        {
            Box(kennel, "KennelSide", new Vector3(side * 0.43f, 0.32f, 0f), new Vector3(0.04f, 0.64f, 1.1f), wood, null, false);
            Box(kennel, "KennelRoof", new Vector3(side * 0.26f, 0.77f, 0f), new Vector3(0.62f, 0.04f, 1.25f), ProceduralModels.Lit(RoofColor), Quaternion.Euler(0f, 0f, -side * 32f), false);
            Box(kennel, "KennelFront", new Vector3(side * 0.33f, 0.32f, -0.55f), new Vector3(0.24f, 0.64f, 0.04f), wood, null, false);
        }
        Box(kennel, "KennelLintel", new Vector3(0f, 0.6f, -0.55f), new Vector3(0.45f, 0.14f, 0.04f), wood, null, false);

        Material iron = ProceduralModels.Lit(IronColor * 2.5f, 0f, 0f, 0.5f);
        for (int i = 0; i < 12; i++)
        {
            Vector3 link = new Vector3(Mathf.Sin(i * 0.8f) * 0.14f, 0.02f, -0.6f - i * 0.09f);
            Box(kennel, "ChainLink", link, new Vector3(0.025f, 0.018f, 0.075f), iron, Quaternion.Euler(0f, i * 31f, 0f), false);
        }
    }

}

// Пламя лампы дрожит, а тени от неё считаются, только пока игрок рядом.
public class LampFlicker : MonoBehaviour
{
    const float ShadowDistance = 22f;
    const float FlickerSpeed = 7f;
    const float DayFactor = 0.3f;

    Light lamp;
    Light spill;
    Material flame;
    Material window;
    float lampIntensity;
    float spillIntensity;

    public void Setup(Light lampLight, Light spillLight, Material flameMaterial, Material windowMaterial)
    {
        lamp = lampLight;
        spill = spillLight;
        flame = flameMaterial;
        window = windowMaterial;
        lampIntensity = lamp.intensity;
        spillIntensity = spill.intensity;
    }

    void Update()
    {
        float t = Time.time * FlickerSpeed;
        float wave = 0.82f + 0.18f * Mathf.PerlinNoise(t, 1.7f) + 0.06f * Mathf.Sin(t * 3.1f);
        float outside = DayNightCycle.IsDark ? 1f : DayFactor;

        lamp.intensity = lampIntensity * wave;
        spill.intensity = spillIntensity * wave * outside;
        flame.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * (2.4f * wave));
        window.SetColor("_EmissionColor", new Color(1f, 0.58f, 0.24f) * (1.1f * wave * (0.6f + 0.4f * outside)));

        Camera view = Camera.main;
        bool near = view != null && (view.transform.position - transform.position).sqrMagnitude < ShadowDistance * ShadowDistance;
        lamp.shadows = near ? LightShadows.Soft : LightShadows.None;
        lamp.enabled = near;
    }
}
