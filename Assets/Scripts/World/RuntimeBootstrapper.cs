using UnityEngine;

// Сцена в проекте пустая: весь тестовый полигон собирается кодом при старте.
public static class RuntimeBootstrapper
{
    const float WheelRadius = 0.42f;
    const float WheelWidth = 0.28f;
    const float WheelTrack = 0.95f;
    const float WheelBase = 1.4f;
    const float WheelHeight = -0.35f;
    const float FogDensity = 0.05f;
    const float VisibleDistance = 45f;
    const float CameraFarClip = 90f;

    static readonly Color ConcreteColor = new Color(0.45f, 0.45f, 0.43f);
    static readonly Color WoodColor = new Color(0.5f, 0.35f, 0.2f);
    static readonly Color HoseColor = new Color(0.1f, 0.1f, 0.1f);
    static readonly Color JerrycanColor = new Color(0.6f, 0.12f, 0.1f);
    static readonly Color PaperColor = new Color(0.8f, 0.78f, 0.65f);
    static readonly Color FuelSourceColor = new Color(0.55f, 0.45f, 0.1f);
    static readonly Color CarColor =new Color(0.25f, 0.32f, 0.38f);
    static readonly Color WheelColor = new Color(0.08f, 0.08f, 0.08f);
    static readonly Color InteriorDark = new Color(0.07f, 0.07f, 0.08f);
    static readonly Color InteriorBlack = new Color(0.02f, 0.02f, 0.02f);
    static readonly Color HeadlightColor = new Color(1f, 0.8f, 0.5f);
    static readonly Color LampOff = new Color(0.55f, 0.55f, 0.5f);
    static readonly Color TailOff = new Color(0.3f, 0.04f, 0.03f);
    static readonly Color TailEmission = new Color(1f, 0.08f, 0.05f);

    static Shader litShader;

    // Общий материал растительности: один на весь лес, чтобы деревья рисовались инстансингом.
    public static Material FoliageMaterial { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Build()
    {
        BuildEnvironment();
        BuildTestGround();
        BuildItems();
        BuildVehicle(new Vector3(5f, 1.2f, 0f));
        BuildPlayer(new Vector3(0f, 1f, -4f));

        // Последним: рельеф создаётся сразу и запоминает всё, что нужно удерживать на земле.
        new GameObject("Forest").AddComponent<OptimizedForestGenerator>();
        new GameObject("MainMenu").AddComponent<MainMenu>();
    }

    static void BuildEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = FogDensity;

        // Дальше тумана ничего не видно: не тратим на это ни разрешение теней, ни отрисовку.
        QualitySettings.shadowDistance = VisibleDistance;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.gameObject.AddComponent<DayNightCycle>();

        FoliageMaterial = new Material(Resources.Load<Shader>("CustomWindFoliage")) { enableInstancing = true };
    }

    static void BuildTestGround()
    {
        CreatePrimitive("Wall_Test", PrimitiveType.Cube, new Vector3(0f, 1.5f, 6f), new Vector3(8f, 3f, 0.5f), ConcreteColor);

        GameObject ramp = CreatePrimitive("Ramp_Test", PrimitiveType.Cube, new Vector3(-6f, 0.8f, 0f), new Vector3(3f, 0.3f, 6f), ConcreteColor);
        ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
    }

    static void BuildItems()
    {
        for (int i = 0; i < 3; i++)
            CreateItem("Item_Cube_" + (i + 1), PrimitiveType.Cube, new Vector3(-1.5f + i, 0.25f, 2f), Vector3.one * 0.5f, 30f, WoodColor);

        GameObject hose = CreateItem("Item_Hose", PrimitiveType.Cylinder, new Vector3(-0.6f, 0.5f, -1.2f), new Vector3(0.08f, 0.45f, 0.08f), 2f, HoseColor);
        hose.AddComponent<FuelSiphon>();

        GameObject blueprint = CreateItem("Item_Blueprint", PrimitiveType.Cube, new Vector3(1f, 0.02f, -2f), new Vector3(0.3f, 0.02f, 0.4f), 0.1f, PaperColor);
        blueprint.AddComponent<BlueprintItem>().recipeID = "fuel_filter";

        CreateJerrycan(new Vector3(-1.2f, 0.225f, -1.5f));

        GameObject source = CreatePrimitive("FuelSource", PrimitiveType.Cube, new Vector3(-3f, 1f, -1f), Vector3.one * 2f, FuelSourceColor);
        source.tag = InteractionRay.FuelSourceTag;
    }

    // Два тега на один объект Unity не позволяет: Pickupable на корне, Jerrycan на коллайдере.
    static void CreateJerrycan(Vector3 position)
    {
        var root = new GameObject("Item_Jerrycan");
        root.tag = InteractionRay.PickupableTag;
        root.transform.position = position;

        GameObject shell = CreatePrimitive("Shell", PrimitiveType.Cube, position, new Vector3(0.3f, 0.45f, 0.18f), JerrycanColor);
        shell.tag = InteractionRay.JerrycanTag;
        shell.transform.SetParent(root.transform, true);

        root.AddComponent<Rigidbody>().mass = 18f;
    }

    static void BuildVehicle(Vector3 position)
    {
        var vehicle = new GameObject("Vehicle");
        vehicle.transform.position = position;

        var body = vehicle.AddComponent<Rigidbody>();
        body.mass = 1500f;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        BuildVehicleBody(vehicle.transform);

        var controller = vehicle.AddComponent<VehicleController>();
        BuildInterior(controller);
        AddWheel(controller, "Wheel_FL", new Vector3(-WheelTrack, WheelHeight, WheelBase), true);
        AddWheel(controller, "Wheel_FR", new Vector3(WheelTrack, WheelHeight, WheelBase), true);
        AddWheel(controller, "Wheel_RL", new Vector3(-WheelTrack, WheelHeight, -WheelBase), false);
        AddWheel(controller, "Wheel_RR", new Vector3(WheelTrack, WheelHeight, -WheelBase), false);

        AddVehicleLights(controller);

        // Низкий центр масс: иначе высокий кузов опрокидывается в поворотах.
        body.centerOfMass = new Vector3(0f, -0.45f, 0f);

        var pushZone = new GameObject("PushZone");
        pushZone.tag = InteractionRay.PushTriggerTag;
        pushZone.transform.SetParent(vehicle.transform, false);
        var zone = pushZone.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(0f, 0f, -2.7f);
        zone.size = new Vector3(2f, 2.4f, 1.6f);

        vehicle.AddComponent<PushableVehicle>();
    }

    // Кузов — дочерние объекты: масштаб на корне исказил бы WheelCollider. Кабина собрана из крыши
    // и стоек, чтобы из салона было видно окна; физически это два сплошных ящика.
    static void BuildVehicleBody(Transform vehicle)
    {
        GameObject hull = CreatePrimitive("Hull", PrimitiveType.Cube, Vector3.zero, new Vector3(1.8f, 0.6f, 4f), CarColor);
        hull.transform.SetParent(vehicle, false);
        hull.transform.localPosition = new Vector3(0f, -0.2f, 0f);

        var cabin = new GameObject("CabinCollider");
        cabin.transform.SetParent(vehicle, false);
        var cabinCollider = cabin.AddComponent<BoxCollider>();
        cabinCollider.center = new Vector3(0f, 0.5f, -0.1f);
        cabinCollider.size = new Vector3(1.8f, 0.8f, 2.2f);

        AddVisual(vehicle, "Roof", PrimitiveType.Cube, new Vector3(0f, 0.88f, -0.1f), new Vector3(1.8f, 0.06f, 2.2f), Quaternion.identity, CarColor);
        foreach (float side in new[] { -1f, 1f })
        {
            AddVisual(vehicle, "PillarFront", PrimitiveType.Cube, new Vector3(side * 0.87f, 0.49f, 0.97f), new Vector3(0.06f, 0.78f, 0.06f), Quaternion.identity, CarColor);
            AddVisual(vehicle, "PillarRear", PrimitiveType.Cube, new Vector3(side * 0.87f, 0.49f, -1.17f), new Vector3(0.06f, 0.78f, 0.06f), Quaternion.identity, CarColor);
        }
    }

    static void BuildInterior(VehicleController controller)
    {
        Transform vehicle = controller.transform;

        // Торпеда и руль ниже линии взгляда: дорога должна оставаться видна поверх них.
        AddVisual(vehicle, "Dashboard", PrimitiveType.Cube, new Vector3(0f, 0.19f, 0.85f), new Vector3(1.66f, 0.18f, 0.24f), Quaternion.identity, InteriorDark);
        AddVisual(vehicle, "SteeringWheel", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.27f, 0.6f), new Vector3(0.28f, 0.012f, 0.28f), Quaternion.Euler(55f, 0f, 0f), InteriorBlack);

        controller.DriverSeat = BuildSeat(vehicle, "DriverSeatPos", -0.42f);
        controller.PassengerSeat = BuildSeat(vehicle, "PassengerSeatPos", 0.42f);
    }

    static Transform BuildSeat(Transform vehicle, string pointName, float x)
    {
        AddVisual(vehicle, "SeatCushion", PrimitiveType.Cube, new Vector3(x, 0.2f, -0.15f), new Vector3(0.55f, 0.18f, 0.55f), Quaternion.identity, InteriorDark);
        AddVisual(vehicle, "SeatBack", PrimitiveType.Cube, new Vector3(x, 0.53f, -0.45f), new Vector3(0.55f, 0.6f, 0.12f), Quaternion.Euler(-8f, 0f, 0f), InteriorDark);

        var point = new GameObject(pointName).transform;
        point.SetParent(vehicle, false);
        point.localPosition = new Vector3(x, 0.3f, -0.15f);
        return point;
    }

    // Только вид: коллайдер на дочернем объекте стал бы частью формы и массы машины.
    static void AddVisual(Transform parent, string name, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Quaternion localRotation, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        Paint(part, color);
        part.transform.SetParent(parent, false);
        part.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        part.transform.localScale = scale;
    }

    static void AddVehicleLights(VehicleController controller)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            var headlight = CreateVehicleLight(controller, "Headlight", new Vector3(side * 0.6f, -0.1f, 2.05f), Quaternion.Euler(5f, 0f, 0f));
            headlight.type = LightType.Spot;
            headlight.range = 70f;
            headlight.spotAngle = 65f;
            headlight.intensity = 8f;
            headlight.color = HeadlightColor;
            controller.AddLight(headlight);

            AddLamp(controller, "HeadLamp", new Vector3(side * 0.6f, -0.1f, 2.01f), LampOff, HeadlightColor * 1.5f);
            AddLamp(controller, "TailLamp", new Vector3(side * 0.7f, -0.1f, -2.01f), TailOff, TailEmission);
        }

        // Один габаритный свет на оба фонаря: подсветке за машиной больше не нужно.
        // Только назад: лампы не отбрасывают теней, и точечный свет прошёл бы сквозь кузов в салон.
        var tail = CreateVehicleLight(controller, "TailLight", new Vector3(0f, -0.1f, -2.1f), Quaternion.Euler(0f, 180f, 0f));
        tail.type = LightType.Spot;
        tail.spotAngle = 130f;
        tail.range = 7f;
        tail.intensity = 1.5f;
        tail.color = TailEmission;
        controller.AddLight(tail);
    }

    static Light CreateVehicleLight(VehicleController controller, string name, Vector3 localPosition, Quaternion localRotation)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(controller.transform, false);
        lightObject.transform.SetLocalPositionAndRotation(localPosition, localRotation);

        var vehicleLight = lightObject.AddComponent<Light>();
        vehicleLight.renderMode = LightRenderMode.ForcePixel;
        return vehicleLight;
    }

    static void AddLamp(VehicleController controller, string name, Vector3 localPosition, Color color, Color emission)
    {
        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(lamp.GetComponent<Collider>());
        lamp.name = name;
        Paint(lamp, color);
        lamp.transform.SetParent(controller.transform, false);
        lamp.transform.localPosition = localPosition;
        lamp.transform.localScale = new Vector3(0.3f, 0.15f, 0.04f);
        controller.AddLamp(lamp.GetComponent<Renderer>(), emission);
    }

    static void AddWheel(VehicleController controller, string name, Vector3 localPosition, bool isFront)
    {
        var wheelObject = new GameObject(name);
        wheelObject.transform.SetParent(controller.transform, false);
        wheelObject.transform.localPosition = localPosition;

        var wheel = wheelObject.AddComponent<WheelCollider>();
        wheel.mass = 40f;
        wheel.radius = WheelRadius;
        // Длинный ход и мягкая пружина: нейтраль сдвинута вниз, чтобы под весом машины
        // запас хода оставался и на сжатие, и на отбой.
        wheel.suspensionDistance = 0.45f;
        wheel.wheelDampingRate = 0.5f;
        wheel.suspensionSpring = new JointSpring { spring = 24000f, damper = 3200f, targetPosition = 0.2f };

        // Визуальный узел вращает колесо целиком, а цилиндр внутри лежит осью вдоль оси колеса.
        var visual = new GameObject(name + "_Visual").transform;
        visual.SetParent(controller.transform, false);

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        cylinder.name = "Tyre";
        Paint(cylinder, WheelColor);
        cylinder.transform.SetParent(visual, false);
        cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        cylinder.transform.localScale = new Vector3(WheelRadius * 2f, WheelWidth * 0.5f, WheelRadius * 2f);

        controller.AddWheel(wheel, visual, isFront);
    }

    static void BuildPlayer(Vector3 position)
    {
        var player = new GameObject("Player");
        player.layer = InteractionRay.IgnoreRaycastLayer;
        player.transform.position = position;

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;

        var body = player.AddComponent<Rigidbody>();
        body.mass = 80f;

        player.AddComponent<PlayerInputReader>();

        var cameraObject = new GameObject("PlayerCamera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = CameraFarClip;
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<PlayerCamera>();

        var hold = new GameObject("HoldPosition");
        hold.transform.SetParent(cameraObject.transform, false);
        hold.transform.localPosition = new Vector3(0f, -0.25f, 1.2f);

        player.AddComponent<PlayerMovement>();
        player.AddComponent<BlueprintNotebook>();
        player.AddComponent<PlayerHands>();
        player.AddComponent<PlayerSeat>();
        cameraObject.AddComponent<InteractionUI>();
    }

    static GameObject CreateItem(string name, PrimitiveType shape, Vector3 position, Vector3 scale, float mass, Color color)
    {
        GameObject item = CreatePrimitive(name, shape, position, scale, color);
        item.tag = InteractionRay.PickupableTag;
        item.AddComponent<Rigidbody>().mass = mass;
        return item;
    }

    static GameObject CreatePrimitive(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(shape);
        primitive.name = name;
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        Paint(primitive, color);
        return primitive;
    }

    static void Paint(GameObject target, Color color)
    {
        if (litShader == null)
            litShader = Resources.Load<Shader>("ForestLit");
        target.GetComponent<Renderer>().sharedMaterial = new Material(litShader) { color = color };
    }
}
