using System.Collections.Generic;
using UnityEngine;

// Полный привод: момент идёт на все колёса, поворот — только на передние.
// Ввод приходит через SetDriverInput, а применяется в FixedUpdate.
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    const float MotorTorque = 900f;
    const float MaxSpeed = 22f;
    const float ReverseSpeedFactor = 0.5f;
    const float BrakeTorque = 3000f;
    const float HandbrakeTorque = 100000f;
    const float EngineBrakeTorque = 800f;
    const float RollingResistance = 25f;
    const float MaxSteerAngle = 32f;
    const float SteerSpeed = 70f;
    const float OpposingSpeedThreshold = 0.5f;
    const float WakeTorque = 0.001f;

    // Грязь отнимает скорость, а в глубокой яме засасывает вставшую машину: тяги мотора там уже не хватает.
    // Выбираться надо раскачкой, толчками в багажник или подкладками под колёса.
    const float MudForwardStiffness = 0.6f;
    const float MudSidewaysStiffness = 0.5f;
    const float GripMargin = 0.9f;
    const float AntiRollStiffness = 9000f;
    const float OverturnedUpDot = 0.5f;
    const float RecoverHeight = 1.5f;
    const float MudResistanceBase = 4000f;
    const float MudResistancePerSpeed = 200f;
    const float MudDamping = 0.25f;
    const float StuckDepth = 0.55f;
    const float StuckForce = 7000f;
    const float SuctionPerSecond = 0.3f;
    const float SuctionMoveLoss = 0.45f;
    const float SuctionStopSpeed = 0.25f;
    const float RockPerThrottleFlip = 0.22f;
    const float PadForwardStiffness = 1.35f;
    const float PadSidewaysStiffness = 1.2f;
    const float StiffnessEpsilon = 0.01f;

    public const float BaseTank = 45f;
    const float IdleBurn = 0.008f;
    const float ThrottleBurn = 0.03f;

    // В глубокой грязи мотор тянет на тех же оборотах вдвое тяжелее.
    const float MudBurn = 1f;
    const float BatteryChargePerSecond = 0.012f;
    const float LightsDrainPerSecond = 1f / 300f;
    const float WornTorque = 0.45f;
    const float FillerSwing = 110f;
    const float FillerResponse = 9f;
    const float FillerOpenOut = 0.052f;
    const float FillerOpenForward = 0.074f;
    const float LowFuelFraction = 0.15f;

    public const int WipersOff = 0;
    public const int WipersSlow = 1;
    public const int WipersFast = 2;
    const int WiperModeCount = 3;

    public bool LightsOn { get; private set; }
    public int WiperMode { get; set; }
    public bool HandbrakeOn { get; set; }
    public float CurrentMudDepth { get; private set; }
    public float Suction { get; private set; }
    public bool IsOverturned => Vector3.Dot(transform.up, Vector3.up) < OverturnedUpDot;
    public PlayerSeat Driver { get; set; }
    public bool RemoteDriven { get; set; }
    public bool IsOccupied => Driver != null || RemoteDriven;
    public float SteerInput => steer;
    public Transform DriverSeat { get; set; }
    public Transform PassengerSeat { get; set; }

    // Места по порядку: водитель, рядом и два сзади.
    public readonly List<Transform> Seats = new List<Transform>();
    public Transform IgnitionKey { get; set; }
    public bool IsCranking { get; set; }
    public Transform FillerCap => fillerCap;
    public float ForwardSpeed => Vector3.Dot(body.linearVelocity, transform.forward);
    public float SteerAngle => currentSteer;
    public float Throttle => throttle;
    public bool EngineOn { get; set; }
    public bool EngineRunning => IsOccupied && EngineOn && FuelLiters > 0f;
    public float Battery { get; set; } = 1f;
    public float Condition { get; set; } = 1f;
    public float FuelLiters { get; private set; } = BaseTank;
    public float TankSize => BaseTank + tankBonus;
    public float FuelFraction => FuelLiters / TankSize;
    public bool FillerOpen { get; set; }
    public Vector3 FillerPoint => fillerCap != null ? transform.TransformPoint(fillerRest) : transform.position;
    public bool IsLowFuel => FuelFraction < LowFuelFraction;
    public IReadOnlyList<Wheel> Wheels => wheels;

    public class Wheel
    {
        public WheelCollider Collider;
        public Transform Visual;
        public bool IsFront;
    }

    class Lamp
    {
        public Material Material;
        public Color Emission;
    }

    static readonly int WorldToLocalId = Shader.PropertyToID("_ForestVehicleWorldToLocal");

    readonly List<Wheel> wheels = new List<Wheel>();
    readonly List<Light> lights = new List<Light>();
    readonly List<Lamp> lamps = new List<Lamp>();
    Rigidbody body;
    float throttle;
    float steer;
    float currentSteer;
    bool releasedForPush;
    float torqueFactor = 1f;
    float speedFactor = 1f;
    float mudGrip;
    float tankBonus;
    float burnFactor = 1f;
    Transform fillerCap;
    Vector3 fillerRest;
    Quaternion fillerRestRotation;
    float fillerAngle;
    float lastThrottleSign;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void ApplyUpgrades(float torque, float speed, float grip, float tank, float burn)
    {
        torqueFactor = torque;
        speedFactor = speed;
        mudGrip = Mathf.Clamp01(grip);
        tankBonus = tank;
        burnFactor = burn;
        FuelLiters = Mathf.Min(FuelLiters, TankSize);
    }

    void Start()
    {
        fillerCap = transform.Find("FuelCap");
        if (fillerCap != null)
        {
            fillerRest = fillerCap.localPosition;
            fillerRestRotation = fillerCap.localRotation;
        }

        // Подшаги колёс убирают дрожание подвески на малых скоростях.
        if (wheels.Count > 0)
            wheels[0].Collider.ConfigureVehicleSubsteps(5f, 12, 15);
    }

    public void AddWheel(WheelCollider wheelCollider, Transform visual, bool isFront)
    {
        wheels.Add(new Wheel { Collider = wheelCollider, Visual = visual, IsFront = isFront });
    }

    public void AddLight(Light vehicleLight)
    {
        vehicleLight.enabled = LightsOn;
        lights.Add(vehicleLight);
        ForestLights.Register(vehicleLight);
    }

    public void AddLamp(Material lampMaterial, Color emission)
    {
        lamps.Add(new Lamp { Material = lampMaterial, Emission = emission });
    }

    public void AddLamp(Renderer lampRenderer, Color emission)
    {
        lamps.Add(new Lamp { Material = lampRenderer.sharedMaterial, Emission = emission });
    }

    public void CycleWipers()
    {
        WiperMode = (WiperMode + 1) % WiperModeCount;
    }

    public void SetLights(bool on)
    {
        LightsOn = on;
        foreach (Light vehicleLight in lights)
            vehicleLight.enabled = on;
        foreach (Lamp lamp in lamps)
            lamp.Material.SetColor("_EmissionColor", on ? lamp.Emission : Color.black);
    }

    // Колёса без момента на малой скорости держат машину намертво, и внешняя сила её не сдвинет.
    // Ничтожный момент выводит их из этого режима, а тормоз снимается на время толкания.
    public void ReleaseWheelsForPush()
    {
        releasedForPush = true;
    }

    public void SetFuel(float liters)
    {
        FuelLiters = Mathf.Clamp(liters, 0f, TankSize);
    }

    // Возвращает, сколько вошло: лишнее остаётся в канистре.
    public float AddFuel(float liters)
    {
        float accepted = Mathf.Min(liters, TankSize - FuelLiters);
        FuelLiters += accepted;
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceFuelAdded(accepted);
        return accepted;
    }

    public void AddFuelFromNetwork(float liters)
    {
        FuelLiters = Mathf.Min(FuelLiters + liters, TankSize);
    }

    public void SetDriverInput(Vector2 move)
    {
        throttle = move.y;
        steer = move.x;
    }

    void ApplyMud()
    {
        float total = 0f;
        foreach (Wheel wheel in wheels)
        {
            // Только сама земля: рампа или кузов чужой машины над дорогой грязью не считаются.
            float depth = 0f;
            bool onPad = false;
            if (wheel.Collider.GetGroundHit(out WheelHit hit))
            {
                if (hit.collider.CompareTag(OptimizedForestGenerator.TerrainTag))
                {
                    depth = OptimizedForestGenerator.MudDepth(hit.point.x, hit.point.z);
                }
                else if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.TryGetComponent(out TractionPad pad))
                {
                    pad.Press();
                    onPad = true;
                }
            }
            total += depth;

            if (onPad)
                SetStiffness(wheel.Collider, PadForwardStiffness, PadSidewaysStiffness);
            else
                SetStiffness(wheel.Collider, Mathf.Lerp(1f, Mathf.Lerp(MudForwardStiffness, 1f, mudGrip), depth), Mathf.Lerp(1f, Mathf.Lerp(MudSidewaysStiffness, 1f, mudGrip), depth));
        }

        CurrentMudDepth = wheels.Count > 0 ? total / wheels.Count : 0f;

        // Чем больше людей в сессии, тем гуще грязь: вчетвером её и должны вытаскивать вчетвером.
        float viscosity = WorldSync.MudViscosity;
        float damping = CurrentMudDepth * MudDamping * viscosity * (1f - mudGrip);
        if (!Mathf.Approximately(body.linearDamping, damping))
            body.linearDamping = damping;

        UpdateSuction();
        if (CurrentMudDepth <= 0f)
            return;

        // Сила не больше той, что гасит скорость за один шаг, иначе грязь толкала бы машину назад.
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (speed < 0.001f)
            return;

        float stuck = Suction * StuckForce * Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(StuckDepth, 1f, CurrentMudDepth));
        float resistance = viscosity * (1f - mudGrip) * (CurrentMudDepth * (MudResistanceBase + MudResistancePerSpeed * speed) + stuck);
        resistance = Mathf.Min(resistance, body.mass * speed / Time.fixedDeltaTime);
        body.AddForce(-velocity / speed * resistance, ForceMode.Force);
    }

    // Стоящую в глубокой грязи машину засасывает, а любое движение и смена направления газа её отпускают.
    void UpdateSuction()
    {
        float sign = throttle > 0f ? 1f : throttle < 0f ? -1f : lastThrottleSign;
        if (sign != lastThrottleSign)
        {
            if (lastThrottleSign != 0f && EngineRunning)
                RockFree(RockPerThrottleFlip);
            lastThrottleSign = sign;
        }

        float speed = body.linearVelocity.magnitude;
        if (CurrentMudDepth < StuckDepth)
            Suction = 0f;
        else if (speed < SuctionStopSpeed)
            Suction = Mathf.Min(1f, Suction + SuctionPerSecond * Time.fixedDeltaTime);
        else
            Suction = Mathf.Max(0f, Suction - speed * SuctionMoveLoss * Time.fixedDeltaTime);
    }

    public void RockFree(float amount)
    {
        Suction = Mathf.Max(0f, Suction - amount);
    }

    // Сорванное колесо теряет сцепление, поэтому на него идёт не больше момента, чем оно
    // может передать на землю: так он весь уходит в тягу, а не в раскрутку.
    static float LimitToGrip(WheelCollider wheel, float motor)
    {
        if (!wheel.GetGroundHit(out WheelHit hit))
            return 0f;

        float grip = wheel.forwardFriction.stiffness * hit.force * wheel.radius * GripMargin;
        return Mathf.Clamp(motor, -grip, grip);
    }

    // Стабилизатор поперечной устойчивости: разница в ходе подвески одной оси
    // прижимает приподнятую сторону, и машина кренится меньше.
    void ApplyAntiRoll()
    {
        for (int i = 0; i + 1 < wheels.Count; i += 2)
        {
            WheelCollider left = wheels[i].Collider;
            WheelCollider right = wheels[i + 1].Collider;
            float force = (SuspensionTravel(left) - SuspensionTravel(right)) * AntiRollStiffness;
            if (left.isGrounded)
                body.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (right.isGrounded)
                body.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }
    }

    static float SuspensionTravel(WheelCollider wheel)
    {
        if (!wheel.GetGroundHit(out WheelHit hit))
            return 1f;
        return (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / wheel.suspensionDistance;
    }

    // Лежащую машину одному не поднять, поэтому она ставится на колёса на том же месте.
    public void RecoverUpright()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);

        Vector3 position = body.position;
        position.y = OptimizedForestGenerator.TerrainHeight(position.x, position.z) + RecoverHeight;
        body.position = position;
        body.rotation = Quaternion.LookRotation(forward, Vector3.up);
        transform.SetPositionAndRotation(position, body.rotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    static void SetStiffness(WheelCollider wheel, float forward, float sideways)
    {
        WheelFrictionCurve curve = wheel.forwardFriction;
        if (Mathf.Abs(curve.stiffness - forward) > StiffnessEpsilon)
        {
            curve.stiffness = forward;
            wheel.forwardFriction = curve;
        }

        curve = wheel.sidewaysFriction;
        if (Mathf.Abs(curve.stiffness - sideways) > StiffnessEpsilon)
        {
            curve.stiffness = sideways;
            wheel.sidewaysFriction = curve;
        }
    }

    void FixedUpdate()
    {
        ApplyMud();
        ApplyAntiRoll();

        // Генератор заряжает от мотора. Фары без мотора садят аккумулятор, и он гаснет насовсем.
        if (EngineRunning)
        {
            Battery = Mathf.Min(1f, Battery + BatteryChargePerSecond * Time.fixedDeltaTime);
        }
        else if (LightsOn && !RemoteDriven)
        {
            Battery = Mathf.Max(0f, Battery - LightsDrainPerSecond * Time.fixedDeltaTime);
            if (Battery <= 0f)
                SetLights(false);
        }
        if (EngineOn && FuelLiters <= 0f)
            EngineOn = false;

        if (EngineRunning)
            FuelLiters = Mathf.Max(0f, FuelLiters - (IdleBurn + ThrottleBurn * Mathf.Abs(throttle) * (1f + MudBurn * CurrentMudDepth)) * burnFactor * Time.fixedDeltaTime);

        float speed = ForwardSpeed;
        float absSpeed = Mathf.Abs(speed);
        float limit = MaxSpeed * speedFactor;
        if (throttle < 0f)
            limit *= ReverseSpeedFactor;

        // Газ против движения сначала тормозит машину и только потом включает обратный ход.
        bool opposing = throttle * speed < 0f && absSpeed > OpposingSpeedThreshold;
        float motor = opposing || HandbrakeOn || !EngineRunning ? 0f : throttle * MotorTorque * torqueFactor * Mathf.Lerp(WornTorque, 1f, Condition) * Mathf.Clamp01(1f - absSpeed / limit);

        float brake = RollingResistance;
        if (HandbrakeOn)
            brake = HandbrakeTorque;
        else if (opposing)
            brake = BrakeTorque;
        else if (absSpeed > limit)
            brake = EngineBrakeTorque;
        else if (throttle != 0f)
            brake = 0f;

        if (releasedForPush && throttle == 0f && !HandbrakeOn)
        {
            motor = WakeTorque;
            brake = 0f;
        }
        releasedForPush = false;

        currentSteer = Mathf.MoveTowards(currentSteer, steer * MaxSteerAngle, SteerSpeed * Time.fixedDeltaTime);

        foreach (Wheel wheel in wheels)
        {
            float wheelSpeed = Mathf.Abs(wheel.Collider.rpm) * Mathf.PI * 2f * wheel.Collider.radius / 60f;
            wheel.Collider.motorTorque = wheelSpeed > limit ? 0f : LimitToGrip(wheel.Collider, motor);
            wheel.Collider.brakeTorque = brake;
            if (wheel.IsFront)
                wheel.Collider.steerAngle = currentSteer;
        }
    }

    void LateUpdate()
    {
        // Грязь и пыль на кузове считаются от машины, иначе плыли бы по нему на ходу.
        Shader.SetGlobalMatrix(WorldToLocalId, transform.worldToLocalMatrix);

        if (fillerCap != null)
        {
            fillerAngle = Mathf.Lerp(fillerAngle, FillerOpen ? FillerSwing : 0f, 1f - Mathf.Exp(-FillerResponse * Time.deltaTime));
            // Лючок на боку кузова: петля у переднего края, задний край отходит наружу.
            Vector3 swing = new Vector3(Mathf.Sign(fillerRest.x) * FillerOpenOut, 0f, FillerOpenForward) * (fillerAngle / FillerSwing);
            fillerCap.SetLocalPositionAndRotation(fillerRest + swing, Quaternion.AngleAxis(fillerAngle, Vector3.up) * fillerRestRotation);
        }

        foreach (Wheel wheel in wheels)
        {
            wheel.Collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheel.Visual.SetPositionAndRotation(position, rotation);
        }
    }
}
