using System.Collections.Generic;
using UnityEngine;

// Полный привод: момент идёт на все колёса, поворот — только на передние.
// Ввод приходит через SetDriverInput, а применяется в FixedUpdate.
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    const float MotorTorque = 600f;
    const float MaxSpeed = 22f;
    const float LowGearTorque = 1800f;
    const float LowGearMaxSpeed = 6f;
    const float ReverseSpeedFactor = 0.5f;
    const float BrakeTorque = 3000f;
    const float EngineBrakeTorque = 800f;
    const float RollingResistance = 25f;
    const float MaxSteerAngle = 32f;
    const float SteerSpeed = 70f;
    const float OpposingSpeedThreshold = 0.5f;
    const float WakeTorque = 0.001f;

    // Сцепление в грязи зависит от передачи: иначе лишний момент пониженной упирался бы в тот же
    // предел трения и она ничего не давала. На обычной колёса срываются в пробуксовку.
    const float MudForwardStiffness = 0.3f;
    const float MudForwardStiffnessLowGear = 0.7f;
    const float MudSidewaysStiffness = 0.5f;
    const float LowGearMaxSlip = 1.5f;
    const float GripMargin = 0.9f;
    const float OverspinBrakeTorque = 600f;
    const float AntiRollStiffness = 9000f;
    const float OverturnedUpDot = 0.5f;
    const float RecoverHeight = 1.5f;
    const float BoggedDepth = 0.5f;
    const float BoggedSpeed = 1f;
    const float MudResistanceBase = 6500f;
    const float MudResistancePerSpeed = 300f;
    const float StiffnessEpsilon = 0.01f;

    public bool isLowGear;

    public bool LightsOn { get; private set; }
    public float CurrentMudDepth { get; private set; }
    public bool IsOverturned => Vector3.Dot(transform.up, Vector3.up) < OverturnedUpDot;
    public bool IsBogged => !isLowGear && throttle != 0f && CurrentMudDepth > BoggedDepth && Mathf.Abs(ForwardSpeed) < BoggedSpeed;
    public PlayerSeat Driver { get; set; }
    public Transform DriverSeat { get; set; }
    public Transform PassengerSeat { get; set; }
    public float ForwardSpeed => Vector3.Dot(body.linearVelocity, transform.forward);
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

    readonly List<Wheel> wheels = new List<Wheel>();
    readonly List<Light> lights = new List<Light>();
    readonly List<Lamp> lamps = new List<Lamp>();
    Rigidbody body;
    float throttle;
    float steer;
    bool brakeHeld;
    float currentSteer;
    bool releasedForPush;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    void Start()
    {
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

    public void AddLamp(Renderer lampRenderer, Color emission)
    {
        lamps.Add(new Lamp { Material = lampRenderer.sharedMaterial, Emission = emission });
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

    public void SetDriverInput(Vector2 move, bool brake)
    {
        throttle = move.y;
        steer = move.x;
        brakeHeld = brake;
    }

    void ApplyMud()
    {
        float total = 0f;
        foreach (Wheel wheel in wheels)
        {
            // Только сама земля: рампа или кузов чужой машины над дорогой грязью не считаются.
            float depth = 0f;
            if (wheel.Collider.GetGroundHit(out WheelHit hit) && hit.collider.CompareTag(OptimizedForestGenerator.TerrainTag))
                depth = OptimizedForestGenerator.MudDepth(hit.point.x, hit.point.z);
            total += depth;

            float mudForward = isLowGear ? MudForwardStiffnessLowGear : MudForwardStiffness;
            SetStiffness(wheel.Collider, Mathf.Lerp(1f, mudForward, depth), Mathf.Lerp(1f, MudSidewaysStiffness, depth));
        }

        CurrentMudDepth = wheels.Count > 0 ? total / wheels.Count : 0f;
        if (CurrentMudDepth <= 0f)
            return;

        // Сила не больше той, что гасит скорость за один шаг, иначе грязь толкала бы машину назад.
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        if (speed < 0.001f)
            return;

        float resistance = CurrentMudDepth * (MudResistanceBase + MudResistancePerSpeed * speed);
        resistance = Mathf.Min(resistance, body.mass * speed / Time.fixedDeltaTime);
        body.AddForce(-velocity / speed * resistance, ForceMode.Force);
    }

    // Сорванное колесо теряет сцепление, поэтому пониженная отдаёт не больше момента, чем колесо
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

        float speed = ForwardSpeed;
        float absSpeed = Mathf.Abs(speed);
        float limit = isLowGear ? LowGearMaxSpeed : MaxSpeed;
        if (throttle < 0f)
            limit *= ReverseSpeedFactor;

        // Газ против движения сначала тормозит машину и только потом включает обратный ход.
        bool opposing = throttle * speed < 0f && absSpeed > OpposingSpeedThreshold;
        float peakTorque = isLowGear ? LowGearTorque : MotorTorque;
        float motor = opposing ? 0f : throttle * peakTorque * Mathf.Clamp01(1f - absSpeed / limit);

        float brake = RollingResistance;
        if (brakeHeld || opposing)
            brake = BrakeTorque;
        else if (absSpeed > limit)
            brake = EngineBrakeTorque;
        else if (throttle != 0f)
            brake = 0f;

        if (releasedForPush && throttle == 0f && !brakeHeld)
        {
            motor = WakeTorque;
            brake = 0f;
        }
        releasedForPush = false;

        currentSteer = Mathf.MoveTowards(currentSteer, steer * MaxSteerAngle, SteerSpeed * Time.fixedDeltaTime);

        foreach (Wheel wheel in wheels)
        {
            float wheelSpeed = Mathf.Abs(wheel.Collider.rpm) * Mathf.PI * 2f * wheel.Collider.radius / 60f;
            float wheelMotor = wheelSpeed > limit ? 0f : motor;
            float wheelBrake = brake;
            if (isLowGear)
            {
                wheelMotor = LimitToGrip(wheel.Collider, wheelMotor);

                // Колесо, раскрученное ещё на обычной передаче, само не остановится:
                // пока оно буксует, тяги нет, поэтому пониженная сначала его притормаживает.
                if (wheelSpeed - absSpeed > LowGearMaxSlip)
                {
                    wheelMotor = 0f;
                    wheelBrake = Mathf.Max(wheelBrake, OverspinBrakeTorque);
                }
            }

            wheel.Collider.motorTorque = wheelMotor;
            wheel.Collider.brakeTorque = wheelBrake;
            if (wheel.IsFront)
                wheel.Collider.steerAngle = currentSteer;
        }
    }

    void LateUpdate()
    {
        foreach (Wheel wheel in wheels)
        {
            wheel.Collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheel.Visual.SetPositionAndRotation(position, rotation);
        }
    }
}
