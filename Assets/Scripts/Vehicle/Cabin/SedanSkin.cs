using System.Collections.Generic;
using UnityEngine;

// Внешний вид основной машины: готовая модель седана поверх той же физики. Колёса, коллайдеры и масса
// остаются нашими, а модель только подогнана по колёсной базе. Модель лежит вне репозитория,
// поэтому без неё машина собирается из примитивов, как раньше.
public static class SedanSkin
{
    const string ModelPath = "Sedans/Car_Prefab";
    public const float Scale = 0.952f;
    public const float GroundY = -0.843f;
    public const float CenterZ = 0.12f;
    public const float Track = 0.8f;

    public const float GlassBottom = 0.09f;
    public const float GlassSlopeScale = 1.55f;

    static readonly Color Paint = new Color(0.13f, 0.145f, 0.115f);
    static readonly Color TrimTint = new Color(0.86f, 0.72f, 0.56f);
    static readonly Color DeadLamp = new Color(0.16f, 0.1f, 0.08f);

    public static bool Available => Resources.Load<GameObject>(ModelPath) != null;
    public static Material HeadlightMaterial { get; private set; }
    public static Material TaillightMaterial { get; private set; }

    // Точка модели в координатах машины.
    public static Vector3 Point(float x, float y, float z)
    {
        return new Vector3(x * Scale, GroundY + y * Scale, (z - CenterZ) * Scale);
    }

    public static void Build(Transform vehicle, Material glass, Color headlightOff, Color taillightOff)
    {
        var root = Object.Instantiate(Resources.Load<GameObject>(ModelPath), vehicle, false).transform;
        root.name = "SedanSkin";
        root.localPosition = new Vector3(0f, GroundY, -CenterZ * Scale);
        root.localScale = Vector3.one * Scale;

        HeadlightMaterial = ProceduralModels.CreateLit(headlightOff);
        TaillightMaterial = ProceduralModels.CreateLit(taillightOff);

        var mapped = new Dictionary<string, Material>();
        foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
        {
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Material[] slots = part.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Mapped(slots[i], glass, mapped);
            part.sharedMaterials = slots;
        }

        var doors = vehicle.gameObject.AddComponent<CarDoors>();
        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
        {
            string name = part.name;
            if (name == "Car_Wheels" || name == "Car_Radio")
                part.gameObject.SetActive(false);
            else if (name == "CDoor_FL")
                doors.FrontLeft = part;
            else if (name == "CDoor_FR")
                doors.FrontRight = part;
            else if (name == "CDoor_BL")
                doors.RearLeft = part;
            else if (name == "CDoor_BR")
                doors.RearRight = part;
        }

        // Подсказка и ремонт ищут капот прямо на машине.
        Transform hood = root.Find("Car_hood");
        hood.SetParent(vehicle, true);
        hood.name = "Hood";
    }

    public static Transform SteeringWheelModel(Transform vehicle)
    {
        return vehicle.Find("SedanSkin/Car_Steering_Wheel");
    }

    static Material Mapped(Material source, Material glass, Dictionary<string, Material> mapped)
    {
        if (source == null)
            return ProceduralModels.Lit(Color.black);
        if (mapped.TryGetValue(source.name, out Material material))
            return material;

        string name = source.name;
        if (name.StartsWith("Car_Glass"))
        {
            material = glass;
        }
        else if (name == "Headlights_Mat")
        {
            material = HeadlightMaterial;
        }
        else if (name == "Rear_lights_Mat")
        {
            material = TaillightMaterial;
        }
        else if (name.Contains("signal") || name.Contains("Signal"))
        {
            material = ProceduralModels.CreateLit(DeadLamp);
        }
        else if (name == "Car_Color_Mat")
        {
            // Та же краска с грязью, что была у машины: мёртвой её не назвать, но и новой тоже.
            material = ProceduralModels.Lit(Paint, 1f, 0f, 0.26f);
        }
        else
        {
            material = ProceduralModels.CreateLit(name == "Car_Number_Mat" ? Color.white * 0.7f : TrimTint);
            material.mainTexture = source.mainTexture;
            material.SetFloat("_Smoothness", 0.14f);
        }

        mapped[source.name] = material;
        return material;
    }
}

// Туман впереди светится от фар: включается вместе с ними, а днём почти незаметен.
public class HeadlightGlow : MonoBehaviour
{
    const float Distance = 11f;
    const float Height = 0.75f;
    const float Width = 22f;
    const float Tall = 11f;
    const float NightStrength = 0.4f;
    const float DayStrength = 0.05f;

    Light source;
    Renderer card;
    Material material;

    public static void Attach(Transform vehicle, Light source)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        // Сразу, ещё до машины: вогнутый коллайдер на теле с Rigidbody сам по себе ошибка.
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.name = "HeadlightGlow";
        quad.transform.SetParent(vehicle, false);
        quad.transform.localPosition = new Vector3(0f, Height, Distance);
        quad.transform.localScale = new Vector3(Width, Tall, 1f);
        var renderer = quad.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = new Material(Resources.Load<Shader>("ForestHeadGlow"));

        var glow = quad.AddComponent<HeadlightGlow>();
        glow.source = source;
        glow.card = renderer;
        glow.material = renderer.sharedMaterial;
    }

    void LateUpdate()
    {
        card.enabled = source.enabled;
        if (source.enabled)
            material.SetColor("_Color", source.color * (DayNightCycle.IsDark ? NightStrength : DayStrength));
    }
}

// Двери открываются, пока человек садится или выходит, и закрываются за ним.
public class CarDoors : MonoBehaviour
{
    const float OpenDegrees = 58f;
    const float SwingSeconds = 0.32f;
    const float HoldSeconds = 0.55f;

    public Transform FrontLeft, FrontRight, RearLeft, RearRight;

    readonly float[] timers = new float[4];

    // Номера мест те же, что у сидений: водитель, пассажир и двое сзади.
    public void Swing(int seat)
    {
        if (seat >= 0 && seat < timers.Length)
            timers[seat] = SwingSeconds * 2f + HoldSeconds;
    }

    void Update()
    {
        Animate(0, FrontLeft, 1f);
        Animate(1, FrontRight, -1f);
        Animate(2, RearLeft, 1f);
        Animate(3, RearRight, -1f);
    }

    void Animate(int seat, Transform door, float side)
    {
        if (door == null || timers[seat] <= 0f)
            return;
        timers[seat] -= Time.deltaTime;

        float total = SwingSeconds * 2f + HoldSeconds;
        float elapsed = total - timers[seat];
        float open = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed, Mathf.Max(timers[seat], 0f)) / SwingSeconds);
        door.localRotation = Quaternion.Euler(0f, side * OpenDegrees * open, 0f);
    }
}
