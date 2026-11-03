using System.Collections.Generic;
using UnityEngine;

// Всё, что шевелится в салоне: стрелки приборов, подсветка, контрольные лампы, руль, руки на нём,
// рычаг ручника, зеркало и ёлочка. Только вид: на физику машины ничего отсюда не влияет.
[RequireComponent(typeof(VehicleController))]
public class VehicleDashboard : MonoBehaviour
{
    const float DialArcDegrees = 270f;
    const float SpeedDialMax = 120f;
    const float RpmDialMax = 6000f;
    const float IdleRpm = 800f;
    const float RpmPerMetrePerSecond = 260f;
    const float RpmThrottleBoost = 900f;
    const float NeedleResponse = 7f;

    const float SteeringWheelTurns = 4f;
    const float HandFollow = 0.35f;
    const float HandbrakeRaised = -32f;
    const float HandbrakeLowered = -6f;
    const float HandbrakeResponse = 11f;
    const float KeyOnAngle = 40f;
    const float KeyCrankAngle = 75f;
    const float KeyResponse = 18f;
    const float LowBattery = 0.25f;
    const float WornEngine = 0.4f;

    public const float FuelBarBottom = 0.248f;
    public const float FuelBarHeight = 0.086f;

    const float DialDayGlow = 0.75f;
    const float DialNightGlow = 1.15f;
    const float RadioGlowOn = 0.8f;

    const float FreshenerGravity = 14f;
    const float FreshenerDamping = 1.6f;
    const float FreshenerMaxAngle = 38f;

    const float WiperSlowSweep = 0.85f;
    const float WiperFastSweep = 1.5f;
    const float WiperSlowPause = 2.2f;
    const float WiperSweepDegrees = 90f;
    const float WiperThumpVolume = 0.08f;

    const int MirrorWidth = 384;
    const int MirrorHeight = 100;
    const int MirrorFramePeriod = 2;
    const float MirrorFarClip = 55f;

    static readonly Vector3 MirrorViewPoint = new Vector3(0f, 0.68f, -1.22f);

    class Arm
    {
        public Transform Hand;
        public Transform Forearm;
        public Vector3 Shoulder;
    }

    public Material SpeedDial;
    public Material RpmDial;
    public Transform SpeedNeedle;
    public Transform RpmNeedle;
    public Material HandbrakeLamp;
    public Material HeadlightLamp;
    public Material FuelLamp;
    public Material BatteryLamp;
    public Material EngineLamp;
    public Color BatteryOnColor;
    public Color EngineOnColor;
    public Color HandbrakeOnColor;
    public Color HeadlightOnColor;
    public Color FuelOnColor;
    public Material RadioDisplay;
    public Color RadioGlow;
    public Material MirrorGlass;
    public Transform SteeringWheel;
    public Transform HandPivot;
    public Transform HandbrakeLever;
    public Transform Freshener;
    public Transform FuelBar;
    public Material FuelBarMaterial;

    public float ShownSpeedKmh { get; private set; }
    public float ShownRpm { get; private set; }
    public float SteeringWheelAngle => -controller.SteerAngle * SteeringWheelTurns;
    public float HandbrakeLeverAngle { get; private set; } = HandbrakeLowered;
    public Camera MirrorCamera => mirrorCamera;
    public float WiperAngle => wiperAngle;

    readonly List<Arm> arms = new List<Arm>();
    VehicleController controller;
    Rigidbody body;
    Camera mirrorCamera;
    RenderTexture mirrorTexture;
    Vector3 lastVelocity;
    float freshenerAngle;
    float freshenerSpeed;
    float shownFuel;
    float keyAngle;
    float wiperPhase;
    float wiperPause;
    float wiperAngle = VehicleExterior.WiperRestAngle;
    WindshieldWater water;

    public void AddArm(Transform hand, Transform forearm, Vector3 shoulder)
    {
        arms.Add(new Arm { Hand = hand, Forearm = forearm, Shoulder = shoulder });
    }

    void Awake()
    {
        controller = GetComponent<VehicleController>();
        body = GetComponent<Rigidbody>();
        water = gameObject.AddComponent<WindshieldWater>();
    }

    void Start()
    {
        CreateMirror();
    }

    // Отдельная камера смотрит назад и пишет в текстуру. Включена, только пока кто-то сидит за рулём.
    void CreateMirror()
    {
        mirrorTexture = new RenderTexture(MirrorWidth, MirrorHeight, 16) { name = "RearMirror", antiAliasing = 2 };

        var cameraObject = new GameObject("MirrorCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.SetLocalPositionAndRotation(MirrorViewPoint, Quaternion.Euler(0f, 180f, 0f));

        mirrorCamera = cameraObject.AddComponent<Camera>();
        mirrorCamera.targetTexture = mirrorTexture;
        mirrorCamera.fieldOfView = 26f;
        mirrorCamera.nearClipPlane = 0.05f;
        mirrorCamera.farClipPlane = MirrorFarClip;
        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = true;
        mirrorCamera.clearFlags = CameraClearFlags.Skybox;
        mirrorCamera.enabled = false;

        // Зеркало переворачивает картинку по горизонтали.
        MirrorGlass.mainTextureScale = new Vector2(-1f, 1f);
        MirrorGlass.mainTextureOffset = new Vector2(1f, 0f);
        MirrorGlass.SetTexture("_EmissionMap", mirrorTexture);
        MirrorGlass.SetColor("_EmissionColor", new Color(0.8f, 0.8f, 0.8f));
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        bool occupied = controller.Driver != null;
        bool anyDriver = controller.IsOccupied;

        UpdateGauges(dt);
        UpdateBacklight();
        UpdateControls(dt);
        UpdateFreshener(dt);
        UpdateArms(occupied);
        UpdateWipers(dt, anyDriver);

        mirrorCamera.enabled = occupied && Time.frameCount % MirrorFramePeriod == 0;
    }

    void UpdateGauges(float dt)
    {
        float blend = 1f - Mathf.Exp(-NeedleResponse * dt);
        float speed = Mathf.Abs(controller.ForwardSpeed);
        bool running = controller.EngineRunning;

        float targetSpeed = speed * 3.6f;
        float targetRpm = running ? IdleRpm + speed * RpmPerMetrePerSecond + Mathf.Abs(controller.Throttle) * RpmThrottleBoost : 0f;
        ShownSpeedKmh = Mathf.Lerp(ShownSpeedKmh, targetSpeed, blend);
        ShownRpm = Mathf.Lerp(ShownRpm, Mathf.Min(targetRpm, RpmDialMax), blend);

        SpeedNeedle.localRotation = NeedleRotation(ShownSpeedKmh / SpeedDialMax);
        RpmNeedle.localRotation = NeedleRotation(ShownRpm / RpmDialMax);

        // Полоса уровня топлива растёт снизу вверх и желтеет на последних литрах.
        shownFuel = Mathf.Lerp(shownFuel, controller.FuelFraction, blend);
        FuelBar.localScale = new Vector3(FuelBar.localScale.x, Mathf.Max(0.0005f, FuelBarHeight * shownFuel), FuelBar.localScale.z);
        Vector3 position = FuelBar.localPosition;
        position.y = FuelBarBottom + FuelBarHeight * shownFuel * 0.5f;
        FuelBar.localPosition = position;
    }

    // Ноль внизу слева, максимум внизу справа; по часовой стрелке, если смотреть от водителя.
    static Quaternion NeedleRotation(float fraction)
    {
        return Quaternion.Euler(0f, 0f, DialArcDegrees * 0.5f - DialArcDegrees * Mathf.Clamp01(fraction));
    }

    void UpdateBacklight()
    {
        // Днём шкалы подсвечены едва, чтобы читаться в тени; с фарами загорается полная подсветка.
        float glow = controller.LightsOn ? DialNightGlow : DialDayGlow;
        Color dial = new Color(0.55f, 1f, 0.62f) * glow;
        SpeedDial.SetColor("_EmissionColor", dial);
        RpmDial.SetColor("_EmissionColor", dial);
        FuelBarMaterial.SetColor("_EmissionColor", controller.IsLowFuel ? FuelOnColor * glow : dial);
        RadioDisplay.SetColor("_EmissionColor", controller.EngineRunning || controller.LightsOn ? RadioGlow * RadioGlowOn : Color.black);

        HandbrakeLamp.SetColor("_EmissionColor", controller.HandbrakeOn ? HandbrakeOnColor : Color.black);
        HeadlightLamp.SetColor("_EmissionColor", controller.LightsOn ? HeadlightOnColor : Color.black);
        FuelLamp.SetColor("_EmissionColor", controller.IsLowFuel ? FuelOnColor : Color.black);

        // Лампа заряда горит, пока мотор не крутит генератор, и когда аккумулятор почти пуст.
        bool charging = controller.EngineRunning && controller.Battery >= LowBattery;
        BatteryLamp.SetColor("_EmissionColor", controller.IsOccupied && !charging ? BatteryOnColor : Color.black);
        EngineLamp.SetColor("_EmissionColor", controller.IsOccupied && controller.Condition < WornEngine ? EngineOnColor : Color.black);
    }

    void UpdateControls(float dt)
    {
        SteeringWheel.localRotation = Quaternion.Euler(0f, 0f, SteeringWheelAngle);
        HandPivot.localRotation = Quaternion.Euler(0f, 0f, SteeringWheelAngle * HandFollow);

        float lever = controller.HandbrakeOn ? HandbrakeRaised : HandbrakeLowered;
        HandbrakeLeverAngle = Mathf.Lerp(HandbrakeLeverAngle, lever, 1f - Mathf.Exp(-HandbrakeResponse * dt));
        HandbrakeLever.localRotation = Quaternion.Euler(HandbrakeLeverAngle, 0f, 0f);

        // Ключ поворачивают вправо, по часовой стрелке от водителя.
        float wanted = controller.IsCranking ? KeyCrankAngle : controller.EngineOn ? KeyOnAngle : 0f;
        keyAngle = Mathf.Lerp(keyAngle, wanted, 1f - Mathf.Exp(-KeyResponse * dt));
        controller.IgnitionKey.localRotation = Quaternion.Euler(0f, 0f, -keyAngle);
    }

    // Маятник от настоящего ускорения кузова: разгон отбрасывает ёлочку назад, поворот вбок.
    void UpdateFreshener(float dt)
    {
        if (dt <= 0f)
            return;

        Vector3 acceleration = (body.linearVelocity - lastVelocity) / dt;
        lastVelocity = body.linearVelocity;
        float push = Vector3.Dot(acceleration, transform.forward) + Vector3.Dot(acceleration, transform.right) * 0.5f;

        freshenerSpeed += (-freshenerAngle * FreshenerGravity - push * 0.6f - freshenerSpeed * FreshenerDamping) * dt;
        freshenerAngle = Mathf.Clamp(freshenerAngle + freshenerSpeed * dt, -FreshenerMaxAngle, FreshenerMaxAngle);
        Freshener.localRotation = Quaternion.Euler(freshenerAngle, freshenerAngle * 25f % 25f, 0f);
    }

    // Дворники работают от аккумулятора, поэтому им не нужен заведённый мотор: в прологе бак пуст,
    // а стекло протереть уже хочется. Выключенные дохаживают ход и ложатся на место.
    void UpdateWipers(float dt, bool occupied)
    {
        int mode = occupied ? controller.WiperMode : VehicleController.WipersOff;
        float angleBefore = wiperAngle;

        if (mode == VehicleController.WipersSlow && wiperPause > 0f)
        {
            wiperPause -= dt;
        }
        else if (mode != VehicleController.WipersOff || wiperPhase > 0f)
        {
            float before = wiperPhase;
            wiperPhase += dt * (mode == VehicleController.WipersFast ? WiperFastSweep : WiperSlowSweep);
            if (before < 1f && wiperPhase >= 1f)
                Thump();
            if (wiperPhase >= 2f)
            {
                Thump();
                wiperPhase = mode == VehicleController.WipersOff ? 0f : wiperPhase - 2f;
                if (mode == VehicleController.WipersSlow)
                {
                    wiperPhase = 0f;
                    wiperPause = WiperSlowPause;
                }
            }
        }

        float travel = wiperPhase < 1f ? wiperPhase : 2f - wiperPhase;
        wiperAngle = VehicleExterior.WiperRestAngle - Mathf.SmoothStep(0f, 1f, travel) * WiperSweepDegrees;
        Quaternion rotation = Quaternion.Euler(0f, 0f, wiperAngle);
        foreach (Transform wiper in VehicleExterior.Wipers)
            wiper.localRotation = rotation;

        water.Step(dt, controller.ForwardSpeed, angleBefore, wiperAngle);
    }

    void Thump()
    {
        if (controller.Driver != null)
            ForestAudio.Play(ForestSound.Click, WiperThumpVolume);
    }

    void UpdateArms(bool occupied)
    {
        HandPivot.gameObject.SetActive(occupied);
        foreach (Arm arm in arms)
        {
            arm.Forearm.gameObject.SetActive(occupied);
            if (!occupied)
                continue;

            Vector3 shoulder = transform.TransformPoint(arm.Shoulder);
            Vector3 toHand = arm.Hand.position - shoulder;
            arm.Forearm.SetPositionAndRotation(shoulder + toHand * 0.5f, Quaternion.FromToRotation(Vector3.up, toHand));
            arm.Forearm.localScale = new Vector3(0.07f, toHand.magnitude * 0.5f, 0.07f);
        }
    }

    void OnDestroy()
    {
        if (mirrorTexture != null)
            mirrorTexture.Release();
    }
}
