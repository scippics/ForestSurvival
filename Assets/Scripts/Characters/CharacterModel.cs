using System.Collections.Generic;
using UnityEngine;

// Модель героя: техник лесхоза в ватнике, ушанке и резиновых сапогах, с рюкзаком за спиной.
// Формы — тела вращения, а руки и ноги сидят на шарнирах: походку считает CharacterAnimator.
public static class CharacterModel
{
    public const string HipLeft = "HipL";
    public const string HipRight = "HipR";
    public const string KneeName = "Knee";
    public const string ShoulderLeft = "ShoulderL";
    public const string ShoulderRight = "ShoulderR";
    public const string ElbowName = "Elbow";

    const int SurfaceQuilt = 8;
    const int SurfaceCloth = 9;
    const int LatheSides = 24;

    public static readonly Color JacketKey = new Color(0.62f, 0.3f, 0.07f);
    public static readonly Color WornJacket = new Color(0.27f, 0.25f, 0.17f);

    static readonly Color SkinColor = new Color(0.62f, 0.44f, 0.34f);
    static readonly Color LipColor = new Color(0.42f, 0.2f, 0.17f);
    static readonly Color TrousersColor = new Color(0.17f, 0.19f, 0.14f);
    static readonly Color BootColor = new Color(0.035f, 0.038f, 0.034f);
    static readonly Color SoleColor = new Color(0.012f, 0.012f, 0.012f);
    static readonly Color FurColor = new Color(0.3f, 0.23f, 0.16f);
    static readonly Color HatColor = new Color(0.15f, 0.16f, 0.12f);
    static readonly Color GloveColor = new Color(0.22f, 0.17f, 0.11f);
    static readonly Color BeltColor = new Color(0.08f, 0.055f, 0.035f);
    static readonly Color BuckleColor = new Color(0.4f, 0.36f, 0.26f);
    static readonly Color CanvasColor = new Color(0.3f, 0.29f, 0.19f);
    static readonly Color BlanketColor = new Color(0.14f, 0.1f, 0.08f);
    static readonly Color BeardColor = new Color(0.2f, 0.15f, 0.11f);
    static readonly Color EyeWhite = new Color(0.62f, 0.6f, 0.56f);
    static readonly Color EyeDark = new Color(0.02f, 0.02f, 0.025f);

    static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

    static Mesh Lathe(string name, Vector2[] profile, float xScale = 1f, float zScale = 1f)
    {
        if (!Meshes.TryGetValue(name, out Mesh mesh))
        {
            mesh = ForestMeshes.BuildLathe(name, Rounded(Rounded(profile)), LatheSides, xScale, zScale);
            Meshes[name] = mesh;
        }
        return mesh;
    }

    // Срезание углов: иначе плечи и локти из нескольких точек выглядят рублеными.
    static Vector2[] Rounded(Vector2[] profile)
    {
        var points = new List<Vector2> { profile[0] };
        for (int i = 0; i < profile.Length - 1; i++)
        {
            points.Add(Vector2.Lerp(profile[i], profile[i + 1], 0.25f));
            points.Add(Vector2.Lerp(profile[i], profile[i + 1], 0.75f));
        }
        points.Add(profile[profile.Length - 1]);
        return points.ToArray();
    }

    static Transform Pivot(Transform parent, string name, Vector3 position)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = position;
        return pivot;
    }

    static Material Cloth(Color color)
    {
        return ProceduralModels.Lit(color, 0f, 0f, 0.05f, SurfaceCloth);
    }

    public static Material NewJacket(Color color)
    {
        Material jacket = ProceduralModels.CreateLit(color);
        jacket.SetFloat("_Surface", SurfaceQuilt);
        jacket.SetFloat("_Smoothness", 0.04f);
        return jacket;
    }

    public static void Build(Transform player, string bodyName, string headName)
    {
        var body = new GameObject(bodyName).transform;
        body.SetParent(player, false);

        Material jacket = NewJacket(JacketKey);
        BuildTorso(body, jacket);
        BuildPack(body);

        var head = Pivot(body, headName, new Vector3(0f, 0.55f, 0f));
        BuildHead(head);

        foreach (float side in new[] { -1f, 1f })
        {
            BuildArm(body, side, jacket);
            BuildLeg(body, side);
        }
    }

    static void BuildTorso(Transform body, Material jacket)
    {
        Mesh torso = Lathe("Torso", new[]
        {
            new Vector2(-0.2f, 0f), new Vector2(-0.2f, 0.19f), new Vector2(-0.08f, 0.2f), new Vector2(0.02f, 0.18f),
            new Vector2(0.12f, 0.185f), new Vector2(0.28f, 0.215f), new Vector2(0.38f, 0.215f), new Vector2(0.45f, 0.17f),
            new Vector2(0.5f, 0.075f), new Vector2(0.51f, 0f)
        }, 1.2f, 0.72f);
        ProceduralModels.MeshPart(body, "Torso", torso, Vector3.zero, Vector3.one, jacket);

        Material skin = ProceduralModels.Lit(SkinColor);
        ProceduralModels.Part(body, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.53f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skin);

        // Стоячий воротник и пояс на животе поверх ватника.
        Mesh collar = Lathe("Collar", new[]
        {
            new Vector2(0.47f, 0.095f), new Vector2(0.53f, 0.1f), new Vector2(0.56f, 0.105f), new Vector2(0.565f, 0.09f), new Vector2(0.5f, 0.07f)
        }, 1.15f, 1f);
        ProceduralModels.MeshPart(body, "Collar", collar, Vector3.zero, Vector3.one, jacket);

        Mesh belt = Lathe("Belt", new[]
        {
            new Vector2(-0.045f, 0.17f), new Vector2(-0.04f, 0.2f), new Vector2(0.01f, 0.195f), new Vector2(0.015f, 0.17f)
        }, 1.2f, 0.72f);
        ProceduralModels.MeshPart(body, "Belt", belt, Vector3.zero, Vector3.one, ProceduralModels.Lit(BeltColor, 0f, 0f, 0.3f));
        ProceduralModels.Part(body, "Buckle", PrimitiveType.Cube, new Vector3(0f, -0.015f, 0.142f), new Vector3(0.055f, 0.045f, 0.012f), ProceduralModels.Lit(BuckleColor, 0f, 0f, 0.5f));

        // Молния и пуговицы: так перед куртки читается сразу.
        Material dark = ProceduralModels.Lit(BeltColor);
        ProceduralModels.Part(body, "Placket", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0.156f), new Vector3(0.022f, 0.46f, 0.008f), dark, Quaternion.Euler(-3f, 0f, 0f));
        for (int i = 0; i < 4; i++)
            ProceduralModels.Part(body, "Button", PrimitiveType.Sphere, new Vector3(0f, 0.07f + i * 0.11f, 0.163f - i * 0.002f), Vector3.one * 0.018f, ProceduralModels.Lit(BuckleColor * 0.5f));

        foreach (float side in new[] { -1f, 1f })
        {
            Material pocket = Cloth(CanvasColor * 0.8f);
            ProceduralModels.Part(body, "Pocket", PrimitiveType.Cube, new Vector3(side * 0.12f, -0.115f, 0.123f), new Vector3(0.1f, 0.095f, 0.014f), pocket, Quaternion.Euler(3f, side * 30f, 0f));
            ProceduralModels.Part(body, "PocketFlap", PrimitiveType.Cube, new Vector3(side * 0.12f, -0.065f, 0.13f), new Vector3(0.108f, 0.028f, 0.014f), Cloth(CanvasColor * 0.6f), Quaternion.Euler(3f, side * 30f, 0f));
            ProceduralModels.Part(body, "ChestPocket", PrimitiveType.Cube, new Vector3(side * 0.07f, 0.27f, 0.152f), new Vector3(0.07f, 0.062f, 0.01f), pocket, Quaternion.Euler(-4f, side * 8f, 0f));

            // Лямки рюкзака идут по груди через плечи, поверх выпуклого ватника.
            Material strap = Cloth(CanvasColor * 0.62f);
            ProceduralModels.Part(body, "Strap", PrimitiveType.Cube, new Vector3(side * 0.165f, 0.27f, 0.122f), new Vector3(0.038f, 0.3f, 0.01f), strap, Quaternion.Euler(-5f, side * 32f, side * -4f));
            ProceduralModels.Part(body, "StrapBuckle", PrimitiveType.Cube, new Vector3(side * 0.169f, 0.2f, 0.122f), new Vector3(0.044f, 0.018f, 0.012f), ProceduralModels.Lit(BuckleColor, 0f, 0f, 0.5f), Quaternion.Euler(-5f, side * 32f, 0f));
            ProceduralModels.Part(body, "StrapTop", PrimitiveType.Cube, new Vector3(side * 0.15f, 0.455f, 0f), new Vector3(0.038f, 0.012f, 0.25f), strap, Quaternion.Euler(0f, 0f, side * -28f));
        }
    }

    static void BuildPack(Transform body)
    {
        Material canvas = Cloth(CanvasColor);
        Mesh pack = Lathe("Pack", new[]
        {
            new Vector2(-0.2f, 0f), new Vector2(-0.19f, 0.13f), new Vector2(-0.1f, 0.17f), new Vector2(0.12f, 0.17f), new Vector2(0.2f, 0.13f), new Vector2(0.235f, 0f)
        }, 1.1f, 0.62f);
        ProceduralModels.MeshPart(body, "Pack", pack, new Vector3(0f, 0.2f, -0.255f), Vector3.one, canvas);
        ProceduralModels.Part(body, "PackPocket", PrimitiveType.Cube, new Vector3(0f, 0.09f, -0.355f), new Vector3(0.24f, 0.15f, 0.05f), Cloth(CanvasColor * 0.8f));
        ProceduralModels.Part(body, "PackFlap", PrimitiveType.Cube, new Vector3(0f, 0.34f, -0.31f), new Vector3(0.26f, 0.09f, 0.05f), Cloth(CanvasColor * 0.8f), Quaternion.Euler(22f, 0f, 0f));

        // Скатка сверху и котелок сбоку.
        ProceduralModels.Part(body, "Bedroll", PrimitiveType.Cylinder, new Vector3(0f, 0.47f, -0.24f), new Vector3(0.15f, 0.21f, 0.15f), Cloth(BlanketColor), Quaternion.Euler(0f, 0f, 90f));
        foreach (float x in new[] { -0.11f, 0.11f })
            ProceduralModels.Part(body, "BedrollTie", PrimitiveType.Cylinder, new Vector3(x, 0.47f, -0.24f), new Vector3(0.16f, 0.012f, 0.16f), Cloth(BeltColor), Quaternion.Euler(0f, 0f, 90f));
        ProceduralModels.Part(body, "Kettle", PrimitiveType.Cylinder, new Vector3(0.2f, 0.12f, -0.27f), new Vector3(0.1f, 0.06f, 0.1f), ProceduralModels.Lit(new Color(0.12f, 0.12f, 0.11f), 0f, 0f, 0.4f));
    }

    static void BuildHead(Transform head)
    {
        Material skin = ProceduralModels.Lit(SkinColor);
        Material beard = Cloth(BeardColor);

        ProceduralModels.Part(head, "Skull", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0.005f), new Vector3(0.205f, 0.25f, 0.225f), skin);
        ProceduralModels.Part(head, "Jaw", PrimitiveType.Sphere, new Vector3(0f, 0.065f, 0.025f), new Vector3(0.165f, 0.13f, 0.17f), skin);
        ProceduralModels.Part(head, "Beard", PrimitiveType.Sphere, new Vector3(0f, 0.042f, 0.05f), new Vector3(0.168f, 0.135f, 0.15f), beard);
        ProceduralModels.Part(head, "BeardChin", PrimitiveType.Sphere, new Vector3(0f, 0.004f, 0.078f), new Vector3(0.09f, 0.085f, 0.085f), beard);
        ProceduralModels.Part(head, "Lip", PrimitiveType.Sphere, new Vector3(0f, 0.082f, 0.104f), new Vector3(0.036f, 0.012f, 0.016f), ProceduralModels.Lit(LipColor));
        ProceduralModels.Part(head, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0.135f, 0.122f), new Vector3(0.034f, 0.058f, 0.05f), skin);
        ProceduralModels.Part(head, "NoseTip", PrimitiveType.Sphere, new Vector3(0f, 0.116f, 0.134f), new Vector3(0.036f, 0.028f, 0.03f), skin);

        foreach (float side in new[] { -1f, 1f })
        {
            ProceduralModels.Part(head, "Moustache", PrimitiveType.Sphere, new Vector3(side * 0.024f, 0.099f, 0.108f), new Vector3(0.052f, 0.018f, 0.024f), beard, Quaternion.Euler(0f, 0f, side * -16f));
            ProceduralModels.Part(head, "Brow", PrimitiveType.Sphere, new Vector3(side * 0.043f, 0.176f, 0.1f), new Vector3(0.058f, 0.015f, 0.022f), beard, Quaternion.Euler(0f, 0f, side * 9f));
            ProceduralModels.Part(head, "Cheek", PrimitiveType.Sphere, new Vector3(side * 0.058f, 0.118f, 0.078f), new Vector3(0.05f, 0.045f, 0.045f), skin);
            ProceduralModels.Part(head, "EyeWhite", PrimitiveType.Sphere, new Vector3(side * 0.041f, 0.152f, 0.098f), new Vector3(0.03f, 0.018f, 0.018f), ProceduralModels.Lit(EyeWhite, 0f, 0f, 0.6f));
            ProceduralModels.Part(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.041f, 0.152f, 0.106f), new Vector3(0.014f, 0.014f, 0.008f), ProceduralModels.Lit(EyeDark, 0f, 0f, 0.8f));
        }

        BuildHat(head);
    }

    // Ушанка: купол, мех спереди и завязанные наверху клапаны.
    static void BuildHat(Transform head)
    {
        Material hat = Cloth(HatColor);
        Material fur = Cloth(FurColor);

        Mesh dome = Lathe("HatDome", new[]
        {
            new Vector2(0f, 0.122f), new Vector2(0.05f, 0.122f), new Vector2(0.09f, 0.1f), new Vector2(0.115f, 0.05f), new Vector2(0.12f, 0f)
        });
        ProceduralModels.MeshPart(head, "HatDome", dome, new Vector3(0f, 0.19f, -0.003f), Vector3.one, hat);

        Mesh band = Lathe("HatBand", new[]
        {
            new Vector2(-0.02f, 0.11f), new Vector2(-0.015f, 0.135f), new Vector2(0.045f, 0.135f), new Vector2(0.055f, 0.11f)
        });
        ProceduralModels.MeshPart(head, "HatBand", band, new Vector3(0f, 0.19f, -0.003f), Vector3.one, fur);

        ProceduralModels.Part(head, "HatFront", PrimitiveType.Sphere, new Vector3(0f, 0.245f, 0.1f), new Vector3(0.21f, 0.085f, 0.07f), fur);
        foreach (float side in new[] { -1f, 1f })
        {
            ProceduralModels.Part(head, "EarFlap", PrimitiveType.Sphere, new Vector3(side * 0.112f, 0.135f, -0.01f), new Vector3(0.05f, 0.16f, 0.13f), fur);
            ProceduralModels.Part(head, "Ear", PrimitiveType.Sphere, new Vector3(side * 0.103f, 0.135f, 0.012f), new Vector3(0.02f, 0.05f, 0.03f), ProceduralModels.Lit(SkinColor));
        }
        ProceduralModels.Part(head, "HatBack", PrimitiveType.Sphere, new Vector3(0f, 0.14f, -0.105f), new Vector3(0.21f, 0.17f, 0.06f), fur);
    }

    static void BuildArm(Transform body, float side, Material jacket)
    {
        Transform shoulder = Pivot(body, side < 0f ? ShoulderLeft : ShoulderRight, new Vector3(side * 0.232f, 0.385f, 0f));
        Mesh upper = Lathe("UpperArm", new[]
        {
            new Vector2(-0.3f, 0f), new Vector2(-0.3f, 0.062f), new Vector2(-0.14f, 0.068f), new Vector2(-0.02f, 0.076f), new Vector2(0.05f, 0.07f), new Vector2(0.085f, 0f)
        });
        ProceduralModels.MeshPart(shoulder, "UpperArm", upper, Vector3.zero, Vector3.one, jacket);

        Transform elbow = Pivot(shoulder, ElbowName, new Vector3(0f, -0.29f, 0f));
        Mesh fore = Lathe("Forearm", new[]
        {
            new Vector2(-0.26f, 0f), new Vector2(-0.26f, 0.052f), new Vector2(-0.1f, 0.062f), new Vector2(-0.01f, 0.068f), new Vector2(0.04f, 0.04f), new Vector2(0.05f, 0f)
        });
        ProceduralModels.MeshPart(elbow, "Forearm", fore, Vector3.zero, Vector3.one, jacket);

        Material glove = Cloth(GloveColor);
        ProceduralModels.Part(elbow, "Cuff", PrimitiveType.Cylinder, new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.018f, 0.11f), ProceduralModels.Lit(BeltColor));
        ProceduralModels.Part(elbow, "Mitt", PrimitiveType.Sphere, new Vector3(0f, -0.315f, 0.005f), new Vector3(0.075f, 0.115f, 0.1f), glove);
        ProceduralModels.Part(elbow, "Thumb", PrimitiveType.Sphere, new Vector3(-side * 0.02f, -0.3f, 0.05f), new Vector3(0.03f, 0.06f, 0.03f), glove, Quaternion.Euler(30f, 0f, 0f));
    }

    static void BuildLeg(Transform body, float side)
    {
        Material trousers = Cloth(TrousersColor);
        Material rubber = ProceduralModels.Lit(BootColor, 0f, 0f, 0.42f);

        Transform hip = Pivot(body, side < 0f ? HipLeft : HipRight, new Vector3(side * 0.105f, -0.06f, 0f));
        Mesh thigh = Lathe("Thigh", new[]
        {
            new Vector2(-0.44f, 0f), new Vector2(-0.44f, 0.07f), new Vector2(-0.3f, 0.078f), new Vector2(-0.1f, 0.098f), new Vector2(0.04f, 0.09f), new Vector2(0.08f, 0f)
        });
        ProceduralModels.MeshPart(hip, "Thigh", thigh, Vector3.zero, Vector3.one, trousers);
        ProceduralModels.Part(hip, "CargoPocket", PrimitiveType.Cube, new Vector3(side * 0.085f, -0.24f, 0f), new Vector3(0.018f, 0.12f, 0.1f), trousers);

        Transform knee = Pivot(hip, KneeName, new Vector3(0f, -0.42f, 0f));

        // Сапог до колена: штанина заправлена, и голенище расширяется кверху.
        Mesh shaft = Lathe("BootShaft", new[]
        {
            new Vector2(-0.4f, 0f), new Vector2(-0.4f, 0.058f), new Vector2(-0.3f, 0.056f), new Vector2(-0.14f, 0.066f), new Vector2(-0.04f, 0.078f), new Vector2(-0.025f, 0.082f), new Vector2(-0.02f, 0.05f)
        });
        ProceduralModels.MeshPart(knee, "BootShaft", shaft, Vector3.zero, Vector3.one, rubber);
        ProceduralModels.Part(knee, "ShinCloth", PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.14f, 0.15f), trousers);

        ProceduralModels.Part(knee, "BootFoot", PrimitiveType.Sphere, new Vector3(0f, -0.405f, 0.055f), new Vector3(0.115f, 0.09f, 0.26f), rubber);
        ProceduralModels.Part(knee, "Sole", PrimitiveType.Cube, new Vector3(0f, -0.455f, 0.05f), new Vector3(0.115f, 0.022f, 0.27f), ProceduralModels.Lit(SoleColor));
        ProceduralModels.Part(knee, "Heel", PrimitiveType.Cube, new Vector3(0f, -0.468f, -0.05f), new Vector3(0.1f, 0.02f, 0.08f), ProceduralModels.Lit(SoleColor));
    }
}

// Походка считается по настоящему смещению тела, поэтому одинаково работает и у своего, и у чужого.
public class CharacterAnimator : MonoBehaviour
{
    const float PhasePerMeter = 4.1f;
    const float FullSwingSpeed = 4.2f;
    const float LegSwing = 34f;
    const float ArmSwing = 26f;
    const float KneeBend = 52f;
    const float ElbowRest = 16f;
    const float ElbowSwing = 16f;
    const float Response = 12f;
    const float BodyDip = 0.022f;

    public float ShownSwing => swing;

    Transform hipLeft, hipRight, kneeLeft, kneeRight, shoulderLeft, shoulderRight, elbowLeft, elbowRight;
    Transform model;
    PlayerAvatar avatar;
    Vector3 lastPosition;
    float phase;
    float swing;
    float seated;

    void Awake()
    {
        model = transform.Find(ProceduralModels.MannequinName);
        hipLeft = model.Find(CharacterModel.HipLeft);
        hipRight = model.Find(CharacterModel.HipRight);
        kneeLeft = hipLeft.Find(CharacterModel.KneeName);
        kneeRight = hipRight.Find(CharacterModel.KneeName);
        shoulderLeft = model.Find(CharacterModel.ShoulderLeft);
        shoulderRight = model.Find(CharacterModel.ShoulderRight);
        elbowLeft = shoulderLeft.Find(CharacterModel.ElbowName);
        elbowRight = shoulderRight.Find(CharacterModel.ElbowName);
        lastPosition = transform.position;
        avatar = GetComponent<PlayerAvatar>();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;

        // В машине игрок становится дочерним объектом кузова: ноги сгибаются, а шаг замирает.
        bool inVehicle = transform.parent != null || avatar.IsSeatedNetwork;
        float speed = inVehicle ? 0f : delta.magnitude / dt;
        float blend = 1f - Mathf.Exp(-Response * dt);
        swing = Mathf.Lerp(swing, Mathf.Clamp01(speed / FullSwingSpeed), blend);
        seated = Mathf.Lerp(seated, inVehicle ? 1f : 0f, blend);
        phase = Mathf.Repeat(phase + speed * PhasePerMeter * dt, Mathf.PI * 2f);

        float leg = Mathf.Sin(phase) * swing * LegSwing;
        float arm = Mathf.Sin(phase) * swing * ArmSwing;

        // Колено сгибается, только когда нога идёт вперёд над землёй.
        float kneeLeftBend = Mathf.Max(0f, Mathf.Cos(phase)) * swing * KneeBend;
        float kneeRightBend = Mathf.Max(0f, -Mathf.Cos(phase)) * swing * KneeBend;

        hipLeft.localRotation = Quaternion.Euler(Mathf.Lerp(-leg, -82f, seated), 0f, 0f);
        hipRight.localRotation = Quaternion.Euler(Mathf.Lerp(leg, -82f, seated), 0f, 0f);
        kneeLeft.localRotation = Quaternion.Euler(Mathf.Lerp(kneeLeftBend, 78f, seated), 0f, 0f);
        kneeRight.localRotation = Quaternion.Euler(Mathf.Lerp(kneeRightBend, 78f, seated), 0f, 0f);

        shoulderLeft.localRotation = Quaternion.Euler(Mathf.Lerp(arm, -48f, seated), 0f, -5f);
        shoulderRight.localRotation = Quaternion.Euler(Mathf.Lerp(-arm, -48f, seated), 0f, 5f);
        elbowLeft.localRotation = Quaternion.Euler(Mathf.Lerp(-(ElbowRest + Mathf.Max(0f, Mathf.Sin(phase)) * swing * ElbowSwing), -40f, seated), 0f, 0f);
        elbowRight.localRotation = Quaternion.Euler(Mathf.Lerp(-(ElbowRest + Mathf.Max(0f, -Mathf.Sin(phase)) * swing * ElbowSwing), -40f, seated), 0f, 0f);

        model.localPosition = new Vector3(0f, -Mathf.Abs(Mathf.Cos(phase)) * swing * BodyDip, 0f);
    }
}
