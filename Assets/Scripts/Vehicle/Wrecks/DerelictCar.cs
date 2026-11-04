using System.Collections.Generic;
using UnityEngine;

// Брошенные такси и полиция: редкие находки у дороги, а не примета конца света. На них нельзя сесть,
// но из бака можно слить бензин. Модели лежат вне репозитория, поэтому без них на этом месте стоит обычная развалина.
public static class DerelictCar
{
    public const int PoseNoWheel = 0;
    public const int PoseOnRoof = 1;
    public const int PoseOnSide = 2;
    public const int PoseDitched = 3;
    const int PoseCount = 4;

    const string PolicePath = "Sedans/Police_Prefab";
    const string TaxiPath = "Sedans/Taxi_Prefab";
    const double PoliceChance = 0.35;
    const double DoorOpenChance = 0.45;
    const double DoorGoneChance = 0.15;
    const double HoodOpenChance = 0.4;
    const double TrunkOpenChance = 0.3;
    const double EmptyTankChance = 0.3;
    const float MinLiters = 6f;
    const float MaxLiters = 45f;
    static readonly Vector3 FillerSpot = new Vector3(0.95f, 0.85f, -1.95f);

    const float RoofHeight = 1.5f;
    const float HalfWidth = 0.965f;
    static readonly Vector3 BodyCenter = new Vector3(0f, 0.78f, 0f);
    static readonly Vector3 BodySize = new Vector3(1.9f, 1.42f, 5.3f);

    static readonly Color GlassColor = new Color(0.03f, 0.04f, 0.045f);

    // Чистая белая краска светилась бы в сумрачном лесу, как новая.
    static readonly Color WornTint = new Color(0.46f, 0.45f, 0.42f);
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    public static bool Available => Resources.Load<GameObject>(TaxiPath) != null;

    public static GameObject Spawn(string id, Vector3 ground, float yaw, int seed)
    {
        var random = new System.Random(seed);
        bool police = random.NextDouble() < PoliceChance;
        return Spawn(id, police, random.Next(PoseCount), ground, yaw, random);
    }

    public static GameObject Spawn(string id, bool police, int pose, Vector3 ground, float yaw, System.Random random)
    {
        var root = new GameObject("Derelict_" + id).transform;
        GameObject model = Object.Instantiate(Resources.Load<GameObject>(police ? PolicePath : TaxiPath), root, false);
        model.name = "Model";

        foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
            part.sharedMaterials = Reskin(part.sharedMaterials);

        foreach (Transform part in model.GetComponentsInChildren<Transform>())
            Damage(part, pose, random);

        var body = root.gameObject.AddComponent<BoxCollider>();
        body.center = BodyCenter;
        body.size = BodySize;

        float liters = random.NextDouble() < EmptyTankChance ? 0f : Range(random, MinLiters, MaxLiters);
        WreckBuilder.AddTank(root, id, default, FillerSpot, liters);

        Place(root, pose, ground, yaw, random);
        return root.gameObject;
    }

    // Модель различает части по именам, поэтому повреждения не зависят от того, такси это или полиция.
    static void Damage(Transform part, int pose, System.Random random)
    {
        string name = part.name;
        if (name.Contains("Door_"))
        {
            double roll = random.NextDouble();
            if (roll < DoorGoneChance)
                part.gameObject.SetActive(false);
            else if (roll < DoorGoneChance + DoorOpenChance)
                part.localRotation = Quaternion.Euler(0f, (name.EndsWith("L") ? 1f : -1f) * Range(random, 18f, 62f), 0f);
        }
        else if (name.EndsWith("_hood") || name.EndsWith("_Hood"))
        {
            if (random.NextDouble() < HoodOpenChance)
                part.localRotation = Quaternion.Euler(-Range(random, 14f, 52f), 0f, 0f);
        }
        else if (name.EndsWith("_trunk"))
        {
            if (random.NextDouble() < TrunkOpenChance)
                part.localRotation = Quaternion.Euler(Range(random, 12f, 50f), 0f, 0f);
        }
        else if (name.EndsWith("_WheelFR") && pose == PoseNoWheel)
        {
            part.gameObject.SetActive(false);
        }
    }

    static void Place(Transform root, int pose, Vector3 ground, float yaw, System.Random random)
    {
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        switch (pose)
        {
            case PoseOnRoof:
                root.SetPositionAndRotation(ground + Vector3.up * (RoofHeight - 0.05f), heading * Quaternion.Euler(Range(random, -3f, 3f), 0f, 180f + Range(random, -4f, 4f)));
                break;
            case PoseOnSide:
                root.SetPositionAndRotation(ground + Vector3.up * (HalfWidth - 0.04f), heading * Quaternion.Euler(Range(random, -3f, 3f), 0f, random.NextDouble() < 0.5 ? 88f : -88f));
                break;
            case PoseDitched:
                root.SetPositionAndRotation(ground + Vector3.down * 0.22f, heading * Quaternion.Euler(Range(random, 7f, 13f), 0f, Range(random, -6f, 6f)));
                break;
            default:
                // Нет переднего правого колеса: машина лежит на этом углу.
                root.SetPositionAndRotation(ground + Vector3.down * 0.06f, heading * Quaternion.Euler(3.2f, 0f, -5f));
                break;
        }
    }

    static float Range(System.Random random, float from, float to)
    {
        return from + (float)random.NextDouble() * (to - from);
    }

    static Material[] Reskin(Material[] source)
    {
        var skinned = new Material[source.Length];
        for (int i = 0; i < source.Length; i++)
            skinned[i] = source[i] != null ? Worn(source[i]) : ProceduralModels.Lit(Color.black);
        return skinned;
    }

    // Краска ржавеет своим шейдером, стёкла потемнели от грязи, а лампы давно не горят.
    static Material Worn(Material source)
    {
        if (Materials.TryGetValue(source.name, out Material worn))
            return worn;

        string name = source.name;
        if (name.StartsWith("Car_Glass"))
        {
            worn = ProceduralModels.CreateLit(GlassColor);
            worn.SetFloat("_Smoothness", 0.65f);
        }
        else if (name.Contains("Light") || name.Contains("light") || name.Contains("Signal") || name.Contains("signal"))
        {
            worn = ProceduralModels.CreateLit(source.color * 0.3f);
            worn.SetFloat("_Smoothness", 0.35f);
        }
        else
        {
            worn = ProceduralModels.CreateLit(WornTint);
            worn.mainTexture = source.mainTexture;
            worn.SetFloat("_Smoothness", 0.16f);
            worn.SetFloat("_Surface", name == "Taxi_Mat" || name == "Police_Mat" || name == "Car_Color_Mat" ? CarPartModels.SurfaceRust : 0f);
        }

        Materials[source.name] = worn;
        return worn;
    }
}
