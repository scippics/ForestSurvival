using UnityEngine;

// Салон из деталей без коллайдеров: торпеда, приборы, руль, сиденья, обивка. Подвижные части
// (стрелки, руль, рычаг ручника, лампы) передаются в VehicleDashboard, который ведёт их каждый кадр.
public static class VehicleInterior
{
    const int SurfaceFabric = 1;
    const int SurfacePlastic = 2;
    const int SurfaceRubber = 3;
    const int SurfacePlain = 4;

    static readonly Color PlasticDark = new Color(0.135f, 0.13f, 0.125f);
    static readonly Color PlasticMid = new Color(0.235f, 0.225f, 0.21f);
    static readonly Color Upholstery = new Color(0.24f, 0.19f, 0.135f);
    static readonly Color UpholsteryDark = new Color(0.16f, 0.13f, 0.1f);
    static readonly Color Headliner = new Color(0.3f, 0.285f, 0.25f);
    static readonly Color Carpet = new Color(0.16f, 0.15f, 0.14f);
    static readonly Color Chrome = new Color(0.36f, 0.36f, 0.35f);
    static readonly Color BlackMetal = new Color(0.07f, 0.07f, 0.07f);
    static readonly Color Webbing = new Color(0.16f, 0.16f, 0.17f);
    static readonly Color Sleeve = new Color(0.16f, 0.2f, 0.13f);
    static readonly Color Skin = new Color(0.16f, 0.12f, 0.08f);
    static readonly Color Freshener = new Color(0.1f, 0.42f, 0.16f);
    static readonly Color ToolboxColor = new Color(0.45f, 0.1f, 0.07f);

    static readonly Color DialGlow = new Color(0.55f, 1f, 0.62f);
    static readonly Color RadioGlow = new Color(1f, 0.6f, 0.15f);
    static readonly Color NeedleGlow = new Color(1f, 0.28f, 0.08f);
    static readonly Color HandbrakeLamp = new Color(1f, 0.08f, 0.05f);
    static readonly Color HeadlightLamp = new Color(0.15f, 0.9f, 0.3f);
    static readonly Color FuelLamp = new Color(1f, 0.6f, 0.05f);
    static readonly Color BatteryLamp = new Color(1f, 0.1f, 0.06f);
    static readonly Color EngineLamp = new Color(1f, 0.45f, 0.04f);

    static Mesh wheelRing;

    public static void Build(VehicleController controller)
    {
        Transform vehicle = controller.transform;
        var dash = vehicle.gameObject.AddComponent<VehicleDashboard>();

        BuildShell(vehicle);
        BuildDashboard(vehicle, dash);
        BuildSteering(vehicle, dash);
        BuildConsole(vehicle, dash);
        BuildMirror(vehicle, dash);

        controller.IgnitionKey = BuildIgnition(vehicle);
        controller.RadioFace = vehicle.Find("RadioFace");
        controller.DriverSeat = BuildSeat(vehicle, "DriverSeatPos", -0.42f);
        controller.PassengerSeat = BuildSeat(vehicle, "PassengerSeatPos", 0.42f);
        controller.Seats.Add(controller.DriverSeat);
        controller.Seats.Add(controller.PassengerSeat);
        foreach (float x in new[] { -0.42f, 0.42f })
            controller.Seats.Add(BuildRearSeat(vehicle, x));

        // Слабый свет приборов на лицо панели и руки, включается вместе с фарами.
        var glow = new GameObject("DashGlow").AddComponent<Light>();
        glow.transform.SetParent(vehicle, false);
        glow.transform.localPosition = new Vector3(-0.1f, 0.42f, 0.5f);
        glow.type = LightType.Point;
        glow.range = 1.9f;
        glow.intensity = 0.9f;
        glow.color = DialGlow;
        glow.renderMode = LightRenderMode.ForcePixel;
        glow.shadows = LightShadows.None;
        controller.AddLight(glow);
    }

    static Material Inner(Color color, int surface = SurfacePlastic, float smoothness = 0.2f)
    {
        return ProceduralModels.Lit(color, 0f, 0f, smoothness, surface);
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
    {
        return ProceduralModels.Part(parent, name, PrimitiveType.Cube, position, scale, material, rotation);
    }

    // Цилиндр осью вдоль машины: так лежат ручки, кнопки и крышки, обращённые к водителю.
    static GameObject Knob(Transform parent, string name, Vector3 position, float diameter, float depth, Material material)
    {
        return ProceduralModels.Part(parent, name, PrimitiveType.Cylinder, position, new Vector3(diameter, depth * 0.5f, diameter), material, Quaternion.Euler(90f, 0f, 0f));
    }

    static Material Glowing(Color emission, out Material own)
    {
        own = ProceduralModels.CreateLit(Color.black);
        own.SetColor("_EmissionColor", emission);
        return own;
    }

    static void BuildShell(Transform vehicle)
    {
        Box(vehicle, "Floor", new Vector3(0f, 0.08f, -0.1f), new Vector3(1.66f, 0.012f, 2.1f), Inner(Carpet, SurfaceRubber));
        Box(vehicle, "Tunnel", new Vector3(0f, 0.14f, 0.2f), new Vector3(0.26f, 0.12f, 1.05f), Inner(Carpet, SurfaceFabric));
        Box(vehicle, "Headliner", new Vector3(0f, 0.845f, -0.1f), new Vector3(1.7f, 0.016f, 2.12f), Inner(Headliner, SurfaceFabric));
        Box(vehicle, "RearShelf", new Vector3(0f, 0.2f, -0.88f), new Vector3(1.64f, 0.24f, 0.5f), Inner(UpholsteryDark, SurfaceFabric));
        Box(vehicle, "Bulkhead", new Vector3(0f, 0.2f, -1.14f), new Vector3(1.68f, 0.32f, 0.02f), Inner(PlasticDark));

        // Ящик с инструментами за сиденьями виден, когда оборачиваешься.
        Box(vehicle, "Toolbox", new Vector3(0.3f, 0.385f, -0.9f), new Vector3(0.42f, 0.13f, 0.2f), ProceduralModels.Lit(ToolboxColor, 0f, 0f, 0.3f, SurfacePlain), Quaternion.Euler(0f, 12f, 0f));

        foreach (float side in new[] { -1f, 1f })
        {
            // Дверь изнутри: карта, подлокотник, ручка и стеклоподъёмник.
            Box(vehicle, "DoorCard", new Vector3(side * 0.835f, 0.21f, -0.1f), new Vector3(0.025f, 0.25f, 1.95f), Inner(UpholsteryDark, SurfaceFabric));
            Box(vehicle, "DoorRail", new Vector3(side * 0.83f, 0.355f, -0.1f), new Vector3(0.05f, 0.03f, 1.98f), Inner(PlasticDark));
            Box(vehicle, "Armrest", new Vector3(side * 0.8f, 0.28f, -0.05f), new Vector3(0.07f, 0.04f, 0.55f), Inner(PlasticMid));
            ProceduralModels.Part(vehicle, "DoorHandle", PrimitiveType.Cylinder, new Vector3(side * 0.81f, 0.32f, 0.3f), new Vector3(0.018f, 0.05f, 0.018f), Inner(Chrome, SurfacePlain, 0.7f), Quaternion.Euler(90f, 0f, 0f));

            ProceduralModels.Part(vehicle, "CrankHub", PrimitiveType.Cylinder, new Vector3(side * 0.815f, 0.2f, 0.12f), new Vector3(0.03f, 0.012f, 0.03f), Inner(Chrome, SurfacePlain, 0.7f), Quaternion.Euler(0f, 0f, 90f));
            Box(vehicle, "CrankArm", new Vector3(side * 0.8f, 0.17f, 0.12f), new Vector3(0.008f, 0.07f, 0.012f), Inner(Chrome, SurfacePlain, 0.7f));
            ProceduralModels.Part(vehicle, "CrankKnob", PrimitiveType.Sphere, new Vector3(side * 0.785f, 0.14f, 0.12f), Vector3.one * 0.022f, Inner(BlackMetal));

            // Обшивка стоек и опущенный козырёк: у края кабины не остаётся голого металла.
            Box(vehicle, "PillarTrim", new Vector3(side * 0.82f, 0.59f, 0.93f), new Vector3(0.05f, 0.52f, 0.05f), Inner(PlasticMid));
            Box(vehicle, "Visor", new Vector3(side * 0.42f, 0.825f, 0.82f), new Vector3(0.62f, 0.016f, 0.18f), Inner(Headliner, SurfaceFabric));
            Box(vehicle, "VisorHinge", new Vector3(side * 0.72f, 0.835f, 0.84f), new Vector3(0.02f, 0.02f, 0.02f), Inner(PlasticDark));

            // Вентиляция на торцах панели.
            Box(vehicle, "SideVent", new Vector3(side * 0.74f, 0.27f, 0.738f), new Vector3(0.1f, 0.05f, 0.012f), Inner(BlackMetal));
        }
    }

    static void BuildDashboard(Transform vehicle, VehicleDashboard dash)
    {
        Material body = Inner(PlasticMid);
        Material pad = Inner(PlasticDark, SurfaceFabric, 0.1f);

        Box(vehicle, "DashBody", new Vector3(0f, 0.22f, 0.86f), new Vector3(1.66f, 0.2f, 0.24f), body);
        Box(vehicle, "DashPad", new Vector3(0f, 0.325f, 0.86f), new Vector3(1.68f, 0.02f, 0.25f), pad);
        Box(vehicle, "DashLower", new Vector3(0f, 0.09f, 0.82f), new Vector3(1.66f, 0.08f, 0.16f), Inner(PlasticDark));

        // Козырёк щитка приборов: он прячет лампы от бликов на стекле.
        Box(vehicle, "BinnacleHood", new Vector3(-0.42f, 0.375f, 0.77f), new Vector3(0.58f, 0.03f, 0.2f), pad, Quaternion.Euler(-6f, 0f, 0f));
        Box(vehicle, "BinnacleBack", new Vector3(-0.42f, 0.29f, 0.77f), new Vector3(0.56f, 0.15f, 0.08f), Inner(BlackMetal));
        Box(vehicle, "BinnacleBrow", new Vector3(-0.42f, 0.355f, 0.695f), new Vector3(0.58f, 0.03f, 0.06f), pad);
        foreach (float side in new[] { -1f, 1f })
            Box(vehicle, "BinnacleCheek", new Vector3(-0.42f + side * 0.28f, 0.29f, 0.72f), new Vector3(0.02f, 0.14f, 0.11f), pad);

        dash.SpeedDial = BuildDial(vehicle, "SpeedDial", new Vector3(-0.54f, 0.3f, 0.727f), 120, 20, "КМ/Ч", out dash.SpeedNeedle);
        dash.RpmDial = BuildDial(vehicle, "RpmDial", new Vector3(-0.3f, 0.3f, 0.727f), 6, 1, "Х1000", out dash.RpmNeedle);

        // Контрольные лампы между приборами.
        dash.HandbrakeLamp = BuildWarningLamp(vehicle, new Vector3(-0.42f, 0.325f, 0.727f));
        dash.HeadlightLamp = BuildWarningLamp(vehicle, new Vector3(-0.42f, 0.29f, 0.727f));
        dash.FuelLamp = BuildWarningLamp(vehicle, new Vector3(-0.42f, 0.255f, 0.727f));
        dash.BatteryLamp = BuildWarningLamp(vehicle, new Vector3(-0.448f, 0.325f, 0.727f));
        dash.EngineLamp = BuildWarningLamp(vehicle, new Vector3(-0.448f, 0.29f, 0.727f));
        dash.BatteryOnColor = BatteryLamp;
        dash.EngineOnColor = EngineLamp;
        dash.HandbrakeOnColor = HandbrakeLamp;
        dash.HeadlightOnColor = HeadlightLamp;
        dash.FuelOnColor = FuelLamp;

        // Уровень топлива: тонкая шкала между приборами.
        Box(vehicle, "FuelGaugeBack", new Vector3(-0.392f, 0.291f, 0.7275f), new Vector3(0.012f, 0.094f, 0.002f), Inner(BlackMetal));
        GameObject fuelBar = ProceduralModels.Part(vehicle, "FuelBar", PrimitiveType.Cube, new Vector3(-0.392f, VehicleDashboard.FuelBarBottom, 0.7262f), new Vector3(0.007f, 0.0005f, 0.001f), ProceduralModels.CreateLit(Color.black));
        dash.FuelBar = fuelBar.transform;
        dash.FuelBarMaterial = fuelBar.GetComponent<Renderer>().sharedMaterial;

        // Центральная консоль с дефлекторами, магнитолой и печкой.
        Box(vehicle, "CenterStack", new Vector3(0f, 0.2f, 0.737f), new Vector3(0.36f, 0.25f, 0.012f), body);
        Box(vehicle, "VentFrame", new Vector3(0f, 0.285f, 0.73f), new Vector3(0.3f, 0.06f, 0.006f), Inner(BlackMetal));
        for (int i = 0; i < 9; i++)
            Box(vehicle, "VentFin", new Vector3(-0.13f + i * 0.0325f, 0.285f, 0.726f), new Vector3(0.004f, 0.05f, 0.008f), Inner(PlasticMid));

        Box(vehicle, "RadioFace", new Vector3(0f, 0.222f, 0.73f), new Vector3(0.3f, 0.055f, 0.006f), Inner(BlackMetal));
        GameObject display = ProceduralModels.Part(vehicle, "RadioDisplay", PrimitiveType.Quad, new Vector3(0f, 0.222f, 0.726f), new Vector3(0.14f, 0.032f, 1f), ProceduralModels.CreateLit(Color.black));
        Material displayMaterial = display.GetComponent<Renderer>().sharedMaterial;
        displayMaterial.SetTexture("_EmissionMap", ForestTextures.RadioDisplay());
        dash.RadioDisplay = displayMaterial;
        dash.RadioGlow = RadioGlow;
        foreach (float side in new[] { -1f, 1f })
            Knob(vehicle, "RadioKnob", new Vector3(side * 0.115f, 0.222f, 0.722f), 0.03f, 0.016f, Inner(Chrome, SurfacePlain, 0.5f));

        foreach (float x in new[] { -0.09f, 0f, 0.09f })
            Knob(vehicle, "HeaterKnob", new Vector3(x, 0.15f, 0.726f), 0.04f, 0.018f, Inner(PlasticDark));

        // Перед пассажиром: перчаточный ящик с замком.
        Box(vehicle, "Glovebox", new Vector3(0.48f, 0.21f, 0.736f), new Vector3(0.54f, 0.14f, 0.008f), body);
        Box(vehicle, "GloveLine", new Vector3(0.48f, 0.285f, 0.733f), new Vector3(0.54f, 0.004f, 0.004f), Inner(BlackMetal));
        Knob(vehicle, "GloveLock", new Vector3(0.48f, 0.25f, 0.73f), 0.03f, 0.008f, Inner(Chrome, SurfacePlain, 0.6f));
    }

    static Material BuildDial(Transform vehicle, string name, Vector3 position, int max, int step, string caption, out Transform needle)
    {
        Texture2D face = ForestTextures.Dial(max, step, caption);
        GameObject dial = ProceduralModels.Part(vehicle, name, PrimitiveType.Quad, position, new Vector3(0.14f, 0.14f, 1f), ProceduralModels.CreateLit(new Color(0.02f, 0.02f, 0.02f)));
        Material material = dial.GetComponent<Renderer>().sharedMaterial;
        material.mainTexture = face;
        material.SetTexture("_EmissionMap", face);

        needle = new GameObject(name + "Needle").transform;
        needle.SetParent(vehicle, false);
        needle.localPosition = position + Vector3.back * 0.003f;
        Glowing(NeedleGlow, out Material needleMaterial);
        Box(needle, "Blade", new Vector3(0f, 0.022f, 0f), new Vector3(0.0035f, 0.06f, 0.001f), needleMaterial);
        Knob(needle, "Cap", Vector3.back * 0.001f, 0.014f, 0.003f, Inner(BlackMetal));
        return material;
    }

    static Material BuildWarningLamp(Transform vehicle, Vector3 position)
    {
        Material material = ProceduralModels.CreateLit(new Color(0.03f, 0.03f, 0.03f));
        Knob(vehicle, "WarningLamp", position, 0.018f, 0.002f, material);
        return material;
    }

    static void BuildSteering(Transform vehicle, VehicleDashboard dash)
    {
        // Рулевая колонка наклонена к водителю: локальная ось Z идёт от него вниз вперёд.
        var column = new GameObject("SteeringColumn").transform;
        column.SetParent(vehicle, false);
        column.localPosition = new Vector3(-0.42f, 0.33f, 0.45f);
        column.localRotation = Quaternion.Euler(24f, 0f, 0f);

        ProceduralModels.Part(column, "Column", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(0.055f, 0.15f, 0.055f), Inner(PlasticDark), Quaternion.Euler(90f, 0f, 0f));
        ProceduralModels.Part(column, "Shroud", PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.1f), new Vector3(0.09f, 0.075f, 0.13f), Inner(PlasticMid));
        foreach (float side in new[] { -1f, 1f })
            ProceduralModels.Part(column, "Stalk", PrimitiveType.Cylinder, new Vector3(side * 0.09f, 0.005f, 0.09f), new Vector3(0.012f, 0.055f, 0.012f), Inner(BlackMetal), Quaternion.Euler(0f, 0f, side * 78f));

        dash.SteeringWheel = new GameObject("SteeringWheel").transform;
        dash.SteeringWheel.SetParent(column, false);
        Transform wheel = dash.SteeringWheel;

        Material rim = Inner(new Color(0.13f, 0.12f, 0.11f), SurfaceRubber, 0.3f);
        var ring = new GameObject("Rim");
        ring.transform.SetParent(wheel, false);
        ring.transform.localScale = Vector3.one * 0.36f;
        ring.AddComponent<MeshFilter>().sharedMesh = wheelRing != null ? wheelRing : wheelRing = ForestMeshes.BuildTorus(0.5f, 0.055f);
        ring.AddComponent<MeshRenderer>().sharedMaterial = rim;

        Material spoke = Inner(Chrome, SurfacePlain, 0.55f);
        foreach (float side in new[] { -1f, 1f })
            Box(wheel, "Spoke", new Vector3(side * 0.09f, -0.005f, 0f), new Vector3(0.14f, 0.025f, 0.006f), spoke);
        Box(wheel, "SpokeLow", new Vector3(0f, -0.095f, 0f), new Vector3(0.03f, 0.14f, 0.006f), spoke);
        Knob(wheel, "HubPad", new Vector3(0f, -0.005f, -0.012f), 0.11f, 0.03f, Inner(PlasticDark, SurfaceRubber));
        Knob(wheel, "Badge", new Vector3(0f, -0.005f, -0.028f), 0.03f, 0.002f, spoke);

        // Руки на руле: кисти держатся за обод, а предплечья тянутся к плечам водителя.
        dash.HandPivot = new GameObject("Hands").transform;
        dash.HandPivot.SetParent(column, false);
        Material skin = ProceduralModels.Lit(Skin, 0f, 0f, 0.15f, SurfacePlain);
        Material sleeve = ProceduralModels.Lit(Sleeve, 0f, 0f, 0.1f, SurfaceFabric);
        foreach (float side in new[] { -1f, 1f })
        {
            var hand = new GameObject("Hand").transform;
            hand.SetParent(dash.HandPivot, false);
            hand.localPosition = new Vector3(side * 0.172f, 0.03f, -0.008f);
            ProceduralModels.Part(hand, "Palm", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.075f, 0.09f, 0.06f), skin);
            for (int finger = 0; finger < 4; finger++)
                ProceduralModels.Part(hand, "Finger", PrimitiveType.Capsule, new Vector3(-side * 0.012f, 0.034f - finger * 0.022f, 0.02f), new Vector3(0.018f, 0.024f, 0.018f), skin, Quaternion.Euler(0f, 0f, side * 88f));

            GameObject arm = ProceduralModels.Part(vehicle, "Forearm", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.3f, 0.07f), sleeve);

            // Локоть ниже и шире плеч: так предплечье не лезет в лицо и не закрывает приборы.
            dash.AddArm(hand, arm.transform, new Vector3(-0.42f + side * 0.3f, 0.14f, 0.06f));
        }
    }

    // Замок правее консоли: там его не закрывают от водителя ни обод, ни рука на руле.
    static Transform BuildIgnition(Transform vehicle)
    {
        var position = new Vector3(0.27f, 0.17f, 0.727f);
        Knob(vehicle, "IgnitionLock", position, 0.075f, 0.012f, Inner(Chrome, SurfacePlain, 0.65f));

        // Узел поворачивается вокруг оси замка, и ключ поворачивается вместе с ним.
        var key = new GameObject("IgnitionKey").transform;
        key.SetParent(vehicle, false);
        key.localPosition = position + Vector3.back * 0.006f;
        Box(key, "KeyBlade", new Vector3(0f, 0f, -0.024f), new Vector3(0.018f, 0.005f, 0.05f), Inner(Chrome, SurfacePlain, 0.75f));
        Box(key, "KeyBow", new Vector3(0f, 0f, -0.066f), new Vector3(0.075f, 0.02f, 0.05f), Inner(ToolboxColor, SurfacePlain, 0.4f));
        return key;
    }

    static void BuildConsole(Transform vehicle, VehicleDashboard dash)
    {
        // Рычаг КПП с резиновым чехлом.
        Box(vehicle, "ShifterBoot", new Vector3(0f, 0.215f, 0.42f), new Vector3(0.11f, 0.03f, 0.11f), Inner(BlackMetal, SurfaceRubber));
        ProceduralModels.Part(vehicle, "ShiftStick", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0.41f), new Vector3(0.014f, 0.07f, 0.014f), Inner(Chrome, SurfacePlain, 0.6f), Quaternion.Euler(-8f, 0f, 0f));
        ProceduralModels.Part(vehicle, "ShiftKnob", PrimitiveType.Sphere, new Vector3(0f, 0.355f, 0.4f), Vector3.one * 0.04f, Inner(BlackMetal, SurfacePlain, 0.55f));

        // Ручник. Точка вращения у пола: рычаг поднимается, пока он включён.
        Box(vehicle, "ConsoleBox", new Vector3(0f, 0.225f, -0.1f), new Vector3(0.18f, 0.05f, 0.5f), Inner(UpholsteryDark, SurfaceFabric));
        dash.HandbrakeLever = new GameObject("HandbrakeLever").transform;
        dash.HandbrakeLever.SetParent(vehicle, false);
        dash.HandbrakeLever.localPosition = new Vector3(0f, 0.25f, -0.2f);
        ProceduralModels.Part(dash.HandbrakeLever, "LeverRod", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.13f), new Vector3(0.024f, 0.13f, 0.024f), Inner(Chrome, SurfacePlain, 0.6f), Quaternion.Euler(90f, 0f, 0f));
        ProceduralModels.Part(dash.HandbrakeLever, "LeverGrip", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.27f), new Vector3(0.036f, 0.08f, 0.036f), Inner(BlackMetal, SurfaceRubber), Quaternion.Euler(90f, 0f, 0f));
        Knob(dash.HandbrakeLever, "LeverButton", new Vector3(0f, 0f, 0.36f), 0.018f, 0.012f, Inner(Chrome, SurfacePlain, 0.7f));

        // Педали: видны, если опустить голову.
        foreach (Vector2 pedal in new[] { new Vector2(-0.59f, 0.56f), new Vector2(-0.45f, 0.58f), new Vector2(-0.28f, 0.58f) })
        {
            bool isGas = pedal.x > -0.3f;
            Box(vehicle, "Pedal", new Vector3(pedal.x, 0.16f, pedal.y), isGas ? new Vector3(0.06f, 0.17f, 0.012f) : new Vector3(0.085f, 0.09f, 0.012f), Inner(BlackMetal, SurfaceRubber), Quaternion.Euler(-30f, 0f, 0f));
        }
    }

    static void BuildMirror(Transform vehicle, VehicleDashboard dash)
    {
        Box(vehicle, "MirrorStem", new Vector3(0f, 0.8f, 0.86f), new Vector3(0.016f, 0.08f, 0.016f), Inner(BlackMetal));
        Box(vehicle, "MirrorBody", new Vector3(0f, 0.75f, 0.85f), new Vector3(0.27f, 0.08f, 0.018f), Inner(PlasticDark));

        GameObject glass = ProceduralModels.Part(vehicle, "MirrorGlass", PrimitiveType.Quad, new Vector3(0f, 0.75f, 0.84f), new Vector3(0.25f, 0.065f, 1f), ProceduralModels.CreateLit(Color.black));
        dash.MirrorGlass = glass.GetComponent<Renderer>().sharedMaterial;

        // Ёлочка-ароматизатор качается, когда машина разгоняется и тормозит.
        dash.Freshener = new GameObject("Freshener").transform;
        dash.Freshener.SetParent(vehicle, false);
        dash.Freshener.localPosition = new Vector3(0f, 0.71f, 0.84f);
        Box(dash.Freshener, "String", new Vector3(0f, -0.035f, 0f), new Vector3(0.002f, 0.07f, 0.002f), Inner(Chrome));
        Material green = ProceduralModels.Lit(Freshener, 0f, 0f, 0.1f, SurfacePlain);
        Box(dash.Freshener, "TreeLow", new Vector3(0f, -0.09f, 0f), new Vector3(0.05f, 0.04f, 0.002f), green);
        Box(dash.Freshener, "TreeMid", new Vector3(0f, -0.07f, 0f), new Vector3(0.034f, 0.03f, 0.002f), green);
        Box(dash.Freshener, "TreeTop", new Vector3(0f, -0.052f, 0f), new Vector3(0.016f, 0.03f, 0.002f), green);
        Box(dash.Freshener, "TreeStem", new Vector3(0f, -0.118f, 0f), new Vector3(0.008f, 0.02f, 0.002f), Inner(Upholstery, SurfacePlain));
    }

    // Сзади жёсткая лавка на коробе: двое ещё помещаются, хоть и низко под крышей.
    static Transform BuildRearSeat(Transform vehicle, float x)
    {
        Box(vehicle, "RearCushion", new Vector3(x, 0.34f, -0.86f), new Vector3(0.5f, 0.07f, 0.42f), Inner(Upholstery, SurfaceFabric, 0.08f));
        var point = new GameObject("RearSeatPos").transform;
        point.SetParent(vehicle, false);
        point.localPosition = new Vector3(x, 0.42f, -0.82f);
        return point;
    }

    static Transform BuildSeat(Transform vehicle, string pointName, float x)
    {
        Material fabric = Inner(Upholstery, SurfaceFabric, 0.08f);
        Material bolster = Inner(UpholsteryDark, SurfaceFabric, 0.1f);

        Box(vehicle, "SeatBase", new Vector3(x, 0.1f, -0.15f), new Vector3(0.46f, 0.06f, 0.5f), Inner(BlackMetal));
        Box(vehicle, "SeatCushion", new Vector3(x, 0.19f, -0.13f), new Vector3(0.44f, 0.1f, 0.52f), fabric);

        Quaternion recline = Quaternion.Euler(-10f, 0f, 0f);
        Box(vehicle, "SeatBack", new Vector3(x, 0.5f, -0.47f), new Vector3(0.44f, 0.56f, 0.1f), fabric, recline);
        Box(vehicle, "Headrest", new Vector3(x, 0.84f - 0.03f, -0.53f), new Vector3(0.24f, 0.13f, 0.09f), fabric, recline);

        foreach (float side in new[] { -1f, 1f })
        {
            Box(vehicle, "CushionBolster", new Vector3(x + side * 0.22f, 0.215f, -0.13f), new Vector3(0.07f, 0.12f, 0.52f), bolster);
            Box(vehicle, "BackBolster", new Vector3(x + side * 0.22f, 0.5f, -0.45f), new Vector3(0.07f, 0.54f, 0.12f), bolster, recline);
        }

        // Ремень от стойки через спинку к замку.
        float outer = Mathf.Sign(x);
        Box(vehicle, "Belt", new Vector3(x + outer * 0.13f, 0.5f, -0.39f), new Vector3(0.05f, 0.62f, 0.006f), Inner(Webbing, SurfaceFabric), Quaternion.Euler(-8f, 0f, outer * 22f));
        Box(vehicle, "BeltBuckle", new Vector3(x - outer * 0.25f, 0.27f, -0.25f), new Vector3(0.03f, 0.06f, 0.03f), Inner(BlackMetal));

        var point = new GameObject(pointName).transform;
        point.SetParent(vehicle, false);
        point.localPosition = new Vector3(x, 0.3f, -0.15f);
        return point;
    }
}
