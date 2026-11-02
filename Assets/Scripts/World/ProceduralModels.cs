using System.Collections.Generic;
using UnityEngine;

// Модели из примитивов. Предмет — это корень с тегом и Rigidbody, а под ним детали: коллайдер
// остаётся только там, где нужен лучу и физике, остальное — чистая графика.
public static class ProceduralModels
{
    public const string LootPrefix = "Loot_";
    public const string JerrycanKind = "Jerrycan";
    public const string BatteryKind = "Battery";
    public const string MannequinName = "Mannequin";
    public const string MannequinHeadName = "Head";

    const float JerrycanMass = 18f;
    const float BatteryMass = 14f;
    const float CrateMass = 30f;
    const float HoseMass = 2f;
    const float BlueprintMass = 0.1f;

    static readonly Color PlankColor = new Color(0.27f, 0.22f, 0.17f);
    static readonly Color OldPlankColor = new Color(0.25f, 0.2f, 0.15f);
    static readonly Color StumpColor = new Color(0.27f, 0.19f, 0.12f);
    static readonly Color CharColor = new Color(0.035f, 0.03f, 0.028f);
    static readonly Color StoneColor = new Color(0.3f, 0.3f, 0.29f);
    static readonly Color JerrycanColor = new Color(0.55f, 0.11f, 0.09f);
    static readonly Color CapColor = new Color(0.08f, 0.08f, 0.08f);
    static readonly Color BatteryCaseColor = new Color(0.1f, 0.11f, 0.12f);
    static readonly Color BatteryLidColor = new Color(0.2f, 0.21f, 0.22f);
    static readonly Color TerminalPlusColor = new Color(0.65f, 0.12f, 0.08f);
    static readonly Color TerminalMinusColor = new Color(0.4f, 0.4f, 0.42f);
    static readonly Color WoodColor = new Color(0.5f, 0.35f, 0.2f);
    static readonly Color WoodFrameColor = new Color(0.32f, 0.22f, 0.13f);
    static readonly Color HoseColor = new Color(0.09f, 0.09f, 0.09f);
    static readonly Color BrassColor = new Color(0.6f, 0.47f, 0.2f);
    static readonly Color PaperColor = new Color(0.8f, 0.78f, 0.65f);
    static readonly Color InkColor = new Color(0.14f, 0.25f, 0.5f);
    static readonly Color DrumColor = new Color(0.3f, 0.25f, 0.08f);
    static readonly Color SkinColor = new Color(0.62f, 0.47f, 0.38f);
    static readonly Color TrousersColor = new Color(0.1f, 0.11f, 0.13f);
    static readonly Color BootColor = new Color(0.05f, 0.04f, 0.035f);
    static readonly Color BotJacketColor = new Color(0.8f, 0.36f, 0.06f);
    static readonly Color[] JacketColors =
    {
        new Color(0.2f, 0.27f, 0.17f),
        new Color(0.45f, 0.13f, 0.1f),
        new Color(0.13f, 0.25f, 0.42f),
        new Color(0.42f, 0.34f, 0.12f)
    };

    static readonly Dictionary<(Color, float, float, float), Material> Materials = new Dictionary<(Color, float, float, float), Material>();
    static Shader litShader;

    // Общие материалы: одинаковые детали стоянок должны рисоваться одним пакетом.
    public static Material Lit(Color color, float grime = 0f, float detail = 0f, float smoothness = 0.15f)
    {
        var key = (color, grime, detail, smoothness);
        if (Materials.TryGetValue(key, out Material material))
            return material;

        material = CreateLit(color);
        material.SetFloat("_Grime", grime);
        material.SetFloat("_Detail", detail);
        material.SetFloat("_Smoothness", smoothness);
        Materials[key] = material;
        return material;
    }

    public static Material CreateLit(Color color)
    {
        if (litShader == null)
            litShader = Resources.Load<Shader>("ForestLit");
        return new Material(litShader) { color = color };
    }

    public static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Material material, Quaternion? localRotation = null, bool keepCollider = false)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        if (!keepCollider)
            Object.DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.GetComponent<Renderer>().sharedMaterial = material;
        part.transform.SetParent(parent, false);
        part.transform.SetLocalPositionAndRotation(localPosition, localRotation ?? Quaternion.identity);
        part.transform.localScale = scale;
        return part;
    }

    static GameObject CreateItemRoot(string name, Vector3 position, Quaternion rotation, float mass)
    {
        var root = new GameObject(name) { tag = InteractionRay.PickupableTag };
        root.transform.SetPositionAndRotation(position, rotation);
        root.AddComponent<Rigidbody>().mass = mass;
        return root;
    }

    // Два тега на один объект Unity не позволяет: Pickupable на корне, Jerrycan на коллайдере.
    public static GameObject CreateJerrycan(string name, Vector3 position, Quaternion rotation)
    {
        GameObject root = CreateItemRoot(name, position, rotation, JerrycanMass);
        Transform t = root.transform;
        Material paint = Lit(JerrycanColor, 0f, 0f, 0.35f);

        GameObject shell = Part(t, "Shell", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.4f, 0.14f), paint, null, true);
        shell.tag = InteractionRay.JerrycanTag;

        Part(t, "Rib", PrimitiveType.Cube, Vector3.zero, new Vector3(0.22f, 0.3f, 0.16f), Lit(JerrycanColor * 0.75f, 0f, 0f, 0.3f));
        Part(t, "Shoulder", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.26f, 0.03f, 0.12f), paint);
        Part(t, "Handle", PrimitiveType.Cylinder, new Vector3(-0.04f, 0.27f, 0f), new Vector3(0.025f, 0.09f, 0.025f), paint, Quaternion.Euler(0f, 0f, 90f));
        Part(t, "HandleLeg", PrimitiveType.Cube, new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.02f, 0.06f, 0.02f), paint);
        Part(t, "HandleLeg", PrimitiveType.Cube, new Vector3(0.04f, 0.24f, 0f), new Vector3(0.02f, 0.06f, 0.02f), paint);
        Part(t, "Cap", PrimitiveType.Cylinder, new Vector3(0.1f, 0.24f, 0f), new Vector3(0.05f, 0.025f, 0.05f), Lit(CapColor));
        return root;
    }

    public static GameObject CreateBattery(string name, Vector3 position, Quaternion rotation)
    {
        GameObject root = CreateItemRoot(name, position, rotation, BatteryMass);
        Transform t = root.transform;

        Part(t, "Case", PrimitiveType.Cube, Vector3.zero, new Vector3(0.28f, 0.19f, 0.18f), Lit(BatteryCaseColor, 0f, 0f, 0.25f), null, true);
        Part(t, "Lid", PrimitiveType.Cube, new Vector3(0f, 0.105f, 0f), new Vector3(0.29f, 0.025f, 0.19f), Lit(BatteryLidColor));
        Part(t, "TerminalPlus", PrimitiveType.Cylinder, new Vector3(-0.09f, 0.135f, 0f), new Vector3(0.035f, 0.02f, 0.035f), Lit(TerminalPlusColor, 0f, 0f, 0.5f));
        Part(t, "TerminalMinus", PrimitiveType.Cylinder, new Vector3(0.09f, 0.135f, 0f), new Vector3(0.035f, 0.02f, 0.035f), Lit(TerminalMinusColor, 0f, 0f, 0.6f));
        return root;
    }

    public static GameObject CreateCrate(string name, Vector3 position)
    {
        GameObject root = CreateItemRoot(name, position, Quaternion.identity, CrateMass);
        Transform t = root.transform;
        Material frame = Lit(WoodFrameColor);

        Part(t, "Box", PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.46f, Lit(WoodColor), null, true);
        foreach (float x in new[] { -0.23f, 0.23f })
        {
            foreach (float z in new[] { -0.23f, 0.23f })
                Part(t, "Post", PrimitiveType.Cube, new Vector3(x, 0f, z), new Vector3(0.05f, 0.5f, 0.05f), frame);
            foreach (float y in new[] { -0.23f, 0.23f })
                Part(t, "Rail", PrimitiveType.Cube, new Vector3(x, y, 0f), new Vector3(0.05f, 0.05f, 0.5f), frame);
        }
        foreach (float z in new[] { -0.23f, 0.23f })
        {
            foreach (float y in new[] { -0.23f, 0.23f })
                Part(t, "Rail", PrimitiveType.Cube, new Vector3(0f, y, z), new Vector3(0.5f, 0.05f, 0.05f), frame);
        }
        return root;
    }

    public static GameObject CreateHose(string name, Vector3 position)
    {
        GameObject root = CreateItemRoot(name, position, Quaternion.identity, HoseMass);
        Transform t = root.transform;

        Part(t, "Tube", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.45f, 0.07f), Lit(HoseColor, 0f, 0f, 0.3f), null, true);
        Part(t, "Nozzle", PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0f), new Vector3(0.04f, 0.05f, 0.04f), Lit(BrassColor, 0f, 0f, 0.6f));
        Part(t, "Coupling", PrimitiveType.Cylinder, new Vector3(0f, -0.46f, 0f), new Vector3(0.09f, 0.03f, 0.09f), Lit(BrassColor, 0f, 0f, 0.6f));
        return root;
    }

    public static GameObject CreateBlueprint(string name, Vector3 position)
    {
        GameObject root = CreateItemRoot(name, position, Quaternion.identity, BlueprintMass);
        Transform t = root.transform;

        Part(t, "Sheet", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.02f, 0.4f), Lit(PaperColor), null, true);
        Part(t, "Drawing", PrimitiveType.Cube, new Vector3(0f, 0.011f, 0.02f), new Vector3(0.22f, 0.002f, 0.26f), Lit(InkColor));
        return root;
    }

    // Сам объект — только коллайдер с тегом: луч и сифон проверяют именно его.
    public static GameObject CreateFuelSource(string name, Vector3 position)
    {
        var root = new GameObject(name) { tag = InteractionRay.FuelSourceTag };
        root.transform.position = position;
        Transform t = root.transform;
        root.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);

        Part(t, "Pallet", PrimitiveType.Cube, new Vector3(0f, -0.93f, 0f), new Vector3(2f, 0.14f, 2f), Lit(OldPlankColor, 0f, 1f));
        Material drum = Lit(DrumColor, 0f, 0.5f, 0.25f);
        foreach (Vector2 spot in new[] { new Vector2(-0.48f, -0.48f), new Vector2(0.48f, -0.48f), new Vector2(0f, 0.48f) })
        {
            Part(t, "Drum", PrimitiveType.Cylinder, new Vector3(spot.x, -0.08f, spot.y), new Vector3(0.9f, 0.78f, 0.9f), drum);
            Part(t, "DrumRim", PrimitiveType.Cylinder, new Vector3(spot.x, 0.72f, spot.y), new Vector3(0.93f, 0.03f, 0.93f), Lit(CapColor));
        }
        return root;
    }

    // Манекен игрока. Деталь вращается вместе с Mannequin (поворот тела), а голова — ещё и вокруг шеи.
    // Центр капсулы игрока на высоте 0.9 м над землёй, поэтому ноги уходят вниз от корня.
    public static void BuildMannequin(Transform player)
    {
        var body = new GameObject(MannequinName).transform;
        body.SetParent(player, false);

        var head = new GameObject(MannequinHeadName).transform;
        head.SetParent(body, false);
        head.localPosition = new Vector3(0f, 0.55f, 0f);

        Material skin = Lit(SkinColor);
        Material trousers = Lit(TrousersColor);
        Material boots = Lit(BootColor);
        Material jacket = CreateLit(BotJacketColor);

        Part(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, 0.17f, 0f), new Vector3(0.46f, 0.36f, 0.28f), jacket);
        Part(body, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.52f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skin);
        Part(head, "Skull", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0f), new Vector3(0.24f, 0.27f, 0.25f), skin);
        Part(head, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.2f, -0.01f), new Vector3(0.26f, 0.16f, 0.27f), jacket);
        Part(head, "Visor", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.14f), new Vector3(0.2f, 0.02f, 0.11f), jacket);
        foreach (float side in new[] { -1f, 1f })
        {
            Part(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.05f, 0.13f, 0.115f), Vector3.one * 0.03f, boots);

            // Руки чуть вынесены вперёд: сразу видно, куда развёрнут игрок.
            Part(body, "Shoulder", PrimitiveType.Sphere, new Vector3(side * 0.27f, 0.36f, 0f), Vector3.one * 0.14f, jacket);
            Part(body, "UpperArm", PrimitiveType.Cylinder, new Vector3(side * 0.29f, 0.17f, 0.03f), new Vector3(0.1f, 0.19f, 0.1f), jacket, Quaternion.Euler(-9f, 0f, side * 4f));
            Part(body, "Forearm", PrimitiveType.Cylinder, new Vector3(side * 0.3f, -0.08f, 0.14f), new Vector3(0.085f, 0.16f, 0.085f), jacket, Quaternion.Euler(-40f, 0f, 0f));
            Part(body, "Hand", PrimitiveType.Sphere, new Vector3(side * 0.3f, -0.21f, 0.25f), Vector3.one * 0.09f, skin);

            Part(body, "Thigh", PrimitiveType.Cylinder, new Vector3(side * 0.12f, -0.33f, 0f), new Vector3(0.15f, 0.21f, 0.15f), trousers);
            Part(body, "Shin", PrimitiveType.Cylinder, new Vector3(side * 0.12f, -0.67f, 0f), new Vector3(0.12f, 0.17f, 0.12f), trousers);
            Part(body, "Boot", PrimitiveType.Cube, new Vector3(side * 0.12f, -0.85f, 0.05f), new Vector3(0.14f, 0.1f, 0.28f), boots);
        }
    }

    // Куртка у каждого игрока своя, у бота сигнальная: так их проще различить в лесу.
    public static void PaintMannequin(Transform mannequin, ulong clientId)
    {
        Color color = clientId == ulong.MaxValue ? BotJacketColor : JacketColors[(int)(clientId % (ulong)JacketColors.Length)];
        Material jacket = null;
        foreach (Renderer part in mannequin.GetComponentsInChildren<Renderer>())
        {
            if (part.sharedMaterial.color != BotJacketColor)
                continue;
            if (jacket == null)
                jacket = CreateLit(color);
            part.sharedMaterial = jacket;
        }
    }

    public static GameObject CreateLoot(string name, Vector3 position, Quaternion rotation)
    {
        return name.StartsWith(LootPrefix + BatteryKind)
            ? CreateBattery(name, position, rotation)
            : CreateJerrycan(name, position, rotation);
    }

    // Заброшенная стоянка: навес из досок, пеньки и кострище. Предметы лута создаёт генератор мира,
    // потому что они живут дольше самой стоянки: их можно унести.
    public static GameObject BuildCamp(Vector3 center, float groundHeight, System.Random random)
    {
        var root = new GameObject("Camp");
        root.transform.position = new Vector3(center.x, groundHeight, center.z);
        root.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

        BuildShelter(root.transform, new Vector3(0f, 0f, 3.4f));

        for (int i = 0; i < 4; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(3f, 7f, (float)random.NextDouble());
            float radius = Mathf.Lerp(0.5f, 0.8f, (float)random.NextDouble());
            float height = Mathf.Lerp(0.22f, 0.4f, (float)random.NextDouble());
            var spot = new Vector3(Mathf.Cos(angle) * distance, height - 0.05f, Mathf.Sin(angle) * distance - 1f);
            Part(root.transform, "Stump", PrimitiveType.Cylinder, spot, new Vector3(radius, height, radius), Lit(StumpColor, 0f, 1f), null, true);
        }

        BuildFirePit(root.transform, new Vector3(0f, 0f, -0.8f), random);
        return root;
    }

    static void BuildShelter(Transform camp, Vector3 position)
    {
        var shelter = new GameObject("Shelter").transform;
        shelter.SetParent(camp, false);
        shelter.localPosition = position;

        const float halfWidth = 2.4f;
        const float halfDepth = 1.5f;
        const float frontHeight = 2.5f;
        const float rearHeight = 2f;
        Material post = Lit(OldPlankColor, 0f, 1f);
        Material plank = Lit(PlankColor, 0f, 1f);

        foreach (float x in new[] { -halfWidth, halfWidth })
        {
            AddPost(shelter, new Vector3(x, 0f, -halfDepth), frontHeight, post);
            AddPost(shelter, new Vector3(x, 0f, halfDepth), rearHeight, post);
        }

        Part(shelter, "BeamFront", PrimitiveType.Cube, new Vector3(0f, frontHeight, -halfDepth), new Vector3(halfWidth * 2f + 0.3f, 0.12f, 0.12f), post);
        Part(shelter, "BeamRear", PrimitiveType.Cube, new Vector3(0f, rearHeight, halfDepth), new Vector3(halfWidth * 2f + 0.3f, 0.12f, 0.12f), post);

        // Доски крыши лежат на балках с общим уклоном, а одна сорвана и оставляет дыру.
        float pitch = Mathf.Atan2(frontHeight - rearHeight, halfDepth * 2f) * Mathf.Rad2Deg;
        float roofY = (frontHeight + rearHeight) * 0.5f + 0.1f;
        for (int i = 0; i < 11; i++)
        {
            if (i == 7)
                continue;
            float x = Mathf.Lerp(-halfWidth - 0.1f, halfWidth + 0.1f, i / 10f);
            Part(shelter, "RoofPlank", PrimitiveType.Cube, new Vector3(x, roofY, 0f), new Vector3(0.44f, 0.04f, halfDepth * 2.25f), plank, Quaternion.Euler(pitch, 0f, 0f), true);
        }

        for (int i = 0; i < 4; i++)
            Part(shelter, "WallPlank", PrimitiveType.Cube, new Vector3(0f, 0.3f + i * 0.34f, halfDepth), new Vector3(halfWidth * 2f, 0.28f, 0.04f), plank, null, true);
    }

    static void AddPost(Transform shelter, Vector3 foot, float height, Material material)
    {
        Part(shelter, "Post", PrimitiveType.Cube, foot + Vector3.up * (height * 0.5f), new Vector3(0.16f, height + 0.1f, 0.16f), material, null, true);
    }

    static void BuildFirePit(Transform camp, Vector3 position, System.Random random)
    {
        Material stone = Lit(StoneColor);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float size = Mathf.Lerp(0.22f, 0.34f, (float)random.NextDouble());
            Part(camp, "FireStone", PrimitiveType.Sphere, position + new Vector3(Mathf.Cos(angle) * 0.62f, 0.05f, Mathf.Sin(angle) * 0.62f), new Vector3(size, size * 0.7f, size), stone);
        }

        Material charred = Lit(CharColor);
        for (int i = 0; i < 3; i++)
        {
            float yaw = (float)random.NextDouble() * 180f;
            Part(camp, "Charred", PrimitiveType.Cylinder, position + Vector3.up * 0.06f, new Vector3(0.09f, 0.42f, 0.09f), charred, Quaternion.Euler(0f, yaw, 88f));
        }
    }
}
