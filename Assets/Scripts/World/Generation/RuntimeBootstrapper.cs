using UnityEngine;

// Сцена в проекте пустая: весь тестовый полигон собирается кодом при старте.
public static class RuntimeBootstrapper
{
    const float WheelRadius = 0.42f;
    const float WheelWidth = 0.28f;
    const float WheelTrack = 0.95f;
    const float WheelBase = 1.4f;
    const float WheelHeight = -0.35f;
    const float FogDensity = 0.042f;
    const float CameraFarClip = 90f;
    const float HeadlightBeamLength = 22f;
    const float HeadlightBeamStrength = 0.1f;

    static readonly Color CarColor = new Color(0.17f, 0.19f, 0.12f);
    static readonly Color CarDarkColor = new Color(0.04f, 0.04f, 0.042f);
    static readonly Color MetalColor = new Color(0.13f, 0.13f, 0.135f);
    static readonly Color RimColor = new Color(0.13f, 0.13f, 0.12f);
    static readonly Color WheelColor = new Color(0.05f, 0.05f, 0.05f);
    static readonly Color HeadlightColor = new Color(1f, 0.8f, 0.5f);
    static readonly Color LampOff = new Color(0.55f, 0.55f, 0.5f);
    static readonly Color TailOff = new Color(0.3f, 0.04f, 0.03f);
    static readonly Color TailEmission = new Color(1f, 0.08f, 0.05f);

    static Mesh tyreMesh;
    public static Mesh TyreMesh => tyreMesh != null ? tyreMesh : tyreMesh = ForestMeshes.BuildTyre();
    static Material glassMaterial;

    // Общий материал растительности: один на весь лес, чтобы деревья рисовались инстансингом.
    public static Material FoliageMaterial { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Build()
    {
        ForestNoise.Install();
        BuildEnvironment();
        Kordon.Build();
        BuildVehicle(Kordon.CarSpot, Kordon.CarRotation);

        // Лес и меню строятся, когда появится свой игрок: у хоста сразу, у клиента после подключения.
        NetworkBootstrap.Start(CreatePlayerPrefab());
    }

    // Последним: рельеф создаётся сразу и запоминает всё, что нужно удерживать на земле.
    public static void OnLocalPlayerReady()
    {
        new GameObject("Forest").AddComponent<OptimizedForestGenerator>();
        new GameObject("Atmosphere").AddComponent<ForestAtmosphere>();
        new GameObject("Audio").AddComponent<ForestAudio>();
        new GameObject("Weather").AddComponent<ForestWeather>();
        new GameObject("Watcher").AddComponent<ForestWatcher>();
        new GameObject("GroundTracks").AddComponent<GroundTracks>();

        if (Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            DummyBotSpawner.Spawn(NetworkBootstrap.PlayerPrefab);
            NetworkBootstrap.SpawnWorld();
        }

        new GameObject("MainMenu").AddComponent<MainMenu>();
    }

    static void BuildEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.skybox = new Material(Resources.Load<Shader>("ForestSky"));
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = FogDensity;

        // Дальше тумана ничего не видно: не тратим на это ни разрешение теней, ни отрисовку.
        // Сами тени и сглаживание зависят от выбранного качества.
        GameSettings.Apply();
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;
        sun.shadowBias = 0.03f;
        sun.shadowNormalBias = 0.35f;
        sun.gameObject.AddComponent<DayNightCycle>();

        FoliageMaterial = new Material(Resources.Load<Shader>("CustomWindFoliage")) { enableInstancing = true };
    }

    static void BuildVehicle(Vector3 position, Quaternion rotation)
    {
        var vehicle = new GameObject("Vehicle");
        vehicle.transform.SetPositionAndRotation(position, rotation);

        var body = vehicle.AddComponent<Rigidbody>();
        body.mass = 1500f;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        BuildVehicleBody(vehicle.transform);

        var controller = vehicle.AddComponent<VehicleController>();
        VehicleInterior.Build(controller);
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

        vehicle.AddComponent<CarLoad>();
        vehicle.AddComponent<CarUpgrades>();
        vehicle.AddComponent<WheelTracks>();
        vehicle.AddComponent<PushableVehicle>();
        vehicle.AddComponent<WheelMudSpray>();
        var impact = vehicle.AddComponent<ImpactSound>();
        impact.MinSpeed = 2.5f;
        impact.FullSpeed = 12f;
    }

    // Кузов — дочерние объекты: масштаб на корне исказил бы WheelCollider. Физически машина — два
    // сплошных ящика; всё видимое (капот, кабина, багажник) — отдельные детали без коллайдеров,
    // чтобы форма и масса не зависели от оформления.
    static void BuildVehicleBody(Transform vehicle)
    {
        var hull = new GameObject("HullCollider");
        hull.transform.SetParent(vehicle, false);
        var hullCollider = hull.AddComponent<BoxCollider>();
        hullCollider.center = new Vector3(0f, -0.2f, 0f);
        hullCollider.size = new Vector3(1.8f, 0.6f, 4f);

        var cabin = new GameObject("CabinCollider");
        cabin.transform.SetParent(vehicle, false);
        var cabinCollider = cabin.AddComponent<BoxCollider>();
        cabinCollider.center = new Vector3(0f, 0.5f, -0.1f);
        cabinCollider.size = new Vector3(1.8f, 0.8f, 2.2f);

        // Капот и багажник тоже твёрдые: иначе взгляд на горловину бака проходит над кузовом мимо.
        foreach (float z in new[] { 1.5f, -1.6f })
        {
            var end = new GameObject("EndCollider");
            end.transform.SetParent(vehicle, false);
            var endCollider = end.AddComponent<BoxCollider>();
            endCollider.center = new Vector3(0f, 0.04f, z);
            endCollider.size = new Vector3(1.7f, 0.44f, 0.95f);
        }

        // Ходовая и нижняя часть кабины.
        AddVisual(vehicle, "Chassis", PrimitiveType.Cube, new Vector3(0f, -0.4f, 0f), new Vector3(1.5f, 0.2f, 3.9f), Quaternion.identity, CarDarkColor);
        AddVisual(vehicle, "Tub", PrimitiveType.Cube, new Vector3(0f, -0.12f, -0.1f), new Vector3(1.78f, 0.38f, 2.24f), Quaternion.identity, CarColor);

        // Капот.
        AddVisual(vehicle, "Hood", PrimitiveType.Cube, new Vector3(0f, -0.02f, 1.5f), new Vector3(1.66f, 0.34f, 1.08f), Quaternion.identity, CarColor);
        AddVisual(vehicle, "HoodPanel", PrimitiveType.Cube, new Vector3(0f, 0.17f, 1.44f), new Vector3(1.36f, 0.05f, 0.9f), Quaternion.identity, CarColor * 1.12f);
        AddVisual(vehicle, "Grille", PrimitiveType.Cube, new Vector3(0f, -0.04f, 2.05f), new Vector3(0.82f, 0.24f, 0.03f), Quaternion.identity, CarDarkColor);
        for (int i = 0; i < 5; i++)
            AddVisual(vehicle, "GrilleBar", PrimitiveType.Cube, new Vector3(-0.32f + i * 0.16f, -0.04f, 2.07f), new Vector3(0.035f, 0.24f, 0.02f), Quaternion.identity, MetalColor);

        // Багажник.
        AddVisual(vehicle, "Trunk", PrimitiveType.Cube, new Vector3(0f, 0.03f, -1.61f), new Vector3(1.7f, 0.42f, 0.84f), Quaternion.identity, CarColor);
        AddVisual(vehicle, "TrunkLid", PrimitiveType.Cube, new Vector3(0f, 0.26f, -1.61f), new Vector3(1.56f, 0.04f, 0.74f), Quaternion.identity, CarColor * 1.12f);

        foreach (float side in new[] { -1f, 1f })
        {
            // Дверь с тёмным швом и подножка.
            AddVisual(vehicle, "Door", PrimitiveType.Cube, new Vector3(side * 0.87f, 0.19f, -0.1f), new Vector3(0.05f, 0.34f, 2.1f), Quaternion.identity, CarColor);
            AddVisual(vehicle, "Step", PrimitiveType.Cube, new Vector3(side * 0.96f, -0.32f, -0.1f), new Vector3(0.14f, 0.04f, 1.5f), Quaternion.identity, CarDarkColor);

            AddVisual(vehicle, "PillarFront", PrimitiveType.Cube, new Vector3(side * 0.87f, 0.49f, 0.97f), new Vector3(0.06f, 0.78f, 0.06f), Quaternion.identity, CarColor);
            AddVisual(vehicle, "PillarRear", PrimitiveType.Cube, new Vector3(side * 0.87f, 0.49f, -1.17f), new Vector3(0.06f, 0.78f, 0.06f), Quaternion.identity, CarColor);

            AddVisual(vehicle, "MirrorArm", PrimitiveType.Cube, new Vector3(side * 0.97f, 0.38f, 0.98f), new Vector3(0.16f, 0.02f, 0.02f), Quaternion.identity, CarDarkColor);
            AddVisual(vehicle, "Mirror", PrimitiveType.Cube, new Vector3(side * 1.04f, 0.42f, 0.97f), new Vector3(0.03f, 0.12f, 0.08f), Quaternion.identity, CarDarkColor);

            AddVisual(vehicle, "RoofRail", PrimitiveType.Cube, new Vector3(side * 0.74f, 0.96f, -0.1f), new Vector3(0.04f, 0.04f, 2f), Quaternion.identity, CarDarkColor);
            AddGlass(vehicle, "SideWindow", new Vector3(side * 0.87f, 0.61f, -0.1f), new Vector3(0.012f, 0.5f, 2.1f));
        }

        AddVisual(vehicle, "Roof", PrimitiveType.Cube, new Vector3(0f, 0.88f, -0.1f), new Vector3(1.8f, 0.06f, 2.2f), Quaternion.identity, CarColor);
        foreach (float z in new[] { -0.9f, -0.1f, 0.7f })
            AddVisual(vehicle, "RoofBar", PrimitiveType.Cube, new Vector3(0f, 0.96f, z), new Vector3(1.5f, 0.03f, 0.04f), Quaternion.identity, CarDarkColor);

        AddGlass(vehicle, "Windshield", new Vector3(0f, 0.53f, 0.97f), new Vector3(1.68f, 0.66f, 0.012f));
        AddGlass(vehicle, "RearWindow", new Vector3(0f, 0.6f, -1.17f), new Vector3(1.68f, 0.52f, 0.012f));

        // Бамперы и запаска сзади.
        foreach (float z in new[] { 2.12f, -2.1f })
            AddVisual(vehicle, "Bumper", PrimitiveType.Cube, new Vector3(0f, -0.27f, z), new Vector3(1.84f, 0.13f, 0.1f), Quaternion.identity, MetalColor);

        AddTyre(vehicle, new Vector3(0f, 0.3f, -2.16f), Quaternion.Euler(0f, 90f, 0f));
        VehicleExterior.Build(vehicle);

        // Номера спереди и сзади.
        Texture2D plate = ForestTextures.LicensePlate("А137ТЛ");
        foreach (float z in new[] { 2.181f, -2.161f })
        {
            GameObject sign = ProceduralModels.Part(vehicle, "Plate", PrimitiveType.Quad, new Vector3(0f, -0.27f, z), new Vector3(0.42f, 0.1f, 1f), ProceduralModels.CreateLit(Color.white), Quaternion.Euler(0f, z > 0f ? 180f : 0f, 0f));
            sign.GetComponent<Renderer>().sharedMaterial.mainTexture = plate;
        }
    }

    static void AddTyre(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        var tyre = new GameObject("Tyre");
        tyre.transform.SetParent(parent, false);
        tyre.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        tyre.transform.localScale = new Vector3(WheelWidth, WheelRadius * 2f, WheelRadius * 2f);
        tyre.AddComponent<MeshFilter>().sharedMesh = tyreMesh != null ? tyreMesh : tyreMesh = ForestMeshes.BuildTyre();
        tyre.AddComponent<MeshRenderer>().sharedMaterial = ProceduralModels.Lit(WheelColor, 0.3f, 0f, 0.1f);

        // Обод закрывает отверстие покрышки.
        ProceduralModels.Part(tyre.transform, "Rim", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.66f, 0.47f, 0.66f), ProceduralModels.Lit(RimColor, 0.2f, 0f, 0.3f), Quaternion.Euler(0f, 0f, 90f));
        ProceduralModels.Part(tyre.transform, "Hub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.22f, 0.54f, 0.22f), ProceduralModels.Lit(CarDarkColor), Quaternion.Euler(0f, 0f, 90f));
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            foreach (float side in new[] { -0.54f, 0.54f })
                ProceduralModels.Part(tyre.transform, "Nut", PrimitiveType.Cylinder, new Vector3(side, Mathf.Sin(angle) * 0.2f, Mathf.Cos(angle) * 0.2f), new Vector3(0.035f, 0.03f, 0.035f), ProceduralModels.Lit(MetalColor), Quaternion.Euler(0f, 0f, 90f));
        }
    }

    static void AddGlass(Transform parent, string name, Vector3 localPosition, Vector3 scale)
    {
        if (glassMaterial == null)
            glassMaterial = new Material(Resources.Load<Shader>("ForestGlass"));

        GameObject glass = ProceduralModels.Part(parent, name, PrimitiveType.Cube, localPosition, scale, glassMaterial);
        glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Только вид: коллайдер на дочернем объекте стал бы частью формы и массы машины.
    static void AddVisual(Transform parent, string name, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Quaternion localRotation, Color color)
    {
        ProceduralModels.Part(parent, name, shape, localPosition, scale, ProceduralModels.Lit(color, 1f, 0f, 0.28f), localRotation);
    }

    static void AddVehicleLights(VehicleController controller)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            var headlight = CreateVehicleLight(controller, "Headlight", new Vector3(side * 0.6f, -0.1f, 2.05f), Quaternion.Euler(5f, 0f, 0f));
            headlight.type = LightType.Spot;
            headlight.range = 70f;
            headlight.spotAngle = 65f;
            headlight.intensity = 3f;
            headlight.color = HeadlightColor;
            controller.AddLight(headlight);
            LightBeam.Attach(headlight, HeadlightBeamLength, HeadlightBeamStrength);

            // Цилиндр лежит осью вдоль машины, поэтому фара видна спереди кругом, а чёрное кольцо — её корпус.
            Quaternion alongCar = Quaternion.Euler(90f, 0f, 0f);
            AddVisual(controller.transform, "HeadLampHousing", PrimitiveType.Cylinder, new Vector3(side * 0.62f, -0.02f, 2.03f), new Vector3(0.3f, 0.03f, 0.3f), alongCar, CarDarkColor);
            AddLampPart(controller, "HeadLamp", PrimitiveType.Cylinder, new Vector3(side * 0.62f, -0.02f, 2.06f), new Vector3(0.24f, 0.012f, 0.24f), alongCar, LampOff, HeadlightColor * 3f);
            AddLampPart(controller, "TailLamp", PrimitiveType.Cylinder, new Vector3(side * 0.7f, 0.06f, -2.04f), new Vector3(0.16f, 0.012f, 0.16f), alongCar, TailOff, TailEmission * 2f);
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

    // У лампы свой материал: её свечение включается отдельно от остальных деталей того же цвета.
    static void AddLampPart(VehicleController controller, string name, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Quaternion localRotation, Color color, Color emission)
    {
        GameObject lamp = ProceduralModels.Part(controller.transform, name, shape, localPosition, scale, ProceduralModels.CreateLit(color), localRotation);
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

        AddTyre(visual, Vector3.zero, Quaternion.identity);

        controller.AddWheel(wheel, visual, isFront);
    }

    // Шаблон игрока: всё, что должно быть одинаковым у своего, чужого и бота. Выключен, поэтому
    // сам не участвует в игре, а настоящие тела из него создаёт Netcode.
    static GameObject CreatePlayerPrefab()
    {
        var player = new GameObject("Player");
        player.SetActive(false);
        player.layer = InteractionRay.IgnoreRaycastLayer;
        Object.DontDestroyOnLoad(player);

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;

        var body = player.AddComponent<Rigidbody>();
        body.mass = 80f;

        // В машине игрок становится дочерним объектом кузова, а кузов пока не сетевой.
        player.AddComponent<Unity.Netcode.NetworkObject>().AutoObjectParentSync = false;

        var networkTransform = player.AddComponent<OwnerNetworkTransform>();
        networkTransform.SyncRotAngleX = false;
        networkTransform.SyncRotAngleY = false;
        networkTransform.SyncRotAngleZ = false;
        networkTransform.SyncScaleX = false;
        networkTransform.SyncScaleY = false;
        networkTransform.SyncScaleZ = false;
        networkTransform.Interpolate = true;

        ProceduralModels.BuildMannequin(player.transform);
        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerAvatar>();
        player.AddComponent<Footprints>();
        return player;
    }

    // Только для своего игрока: камера, ввод, руки и интерфейс.
    public static void BuildLocalRig(GameObject player)
    {
        player.name = "LocalPlayer";
        player.transform.position = Kordon.PlayerSpot;
        player.AddComponent<PlayerInputReader>();

        var cameraObject = new GameObject("PlayerCamera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = CameraFarClip;
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<ForestPostProcess>();
        cameraObject.AddComponent<PlayerCamera>();

        var hold = new GameObject("HoldPosition");
        hold.transform.SetParent(cameraObject.transform, false);
        hold.transform.localPosition = new Vector3(0f, -0.25f, 1.2f);

        player.AddComponent<BlueprintNotebook>();
        player.AddComponent<PlayerHands>();
        player.AddComponent<PlayerSeat>();
        player.AddComponent<PlayerWrench>();
        player.AddComponent<TorchCharge>();
        cameraObject.AddComponent<InteractionUI>();
        cameraObject.AddComponent<FirstPersonArms>();
    }

}
