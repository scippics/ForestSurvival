using UnityEngine;

// Посадка за руль и передача ввода машине. Пока игрок сидит, его физика выключена.
public class PlayerSeat : MonoBehaviour
{
    // Точка сиденья — на уровне таза; центр капсулы ниже неё, чтобы глаза оказались на уровне сидящего.
    static readonly Vector3 SeatedBodyOffset = new Vector3(0f, -0.38f, 0f);
    static readonly float[] ExitSides = { -1f, 1f };
    const float ExitSideDistance = 1.6f;
    const float ExitGroundClearance = 1f;

    public VehicleController Vehicle { get; private set; }
    public bool IsSeated => Vehicle != null;
    public PushableVehicle PushZone { get; set; }
    public PlayerInputReader Input { get; private set; }
    public bool IsPouring { get; private set; }

    const float SwayPerAcceleration = 0.006f;
    const float SwayMax = 0.06f;
    const float SwayResponse = 6f;
    const float RoadShakePerSpeed = 0.00035f;
    const float RoadShakeFrequency = 17f;
    const float PourLitersPerSecond = 4f;

    Vector3 lastVehicleVelocity;
    Vector3 sway;

    Rigidbody body;
    CapsuleCollider capsule;
    PlayerMovement movement;
    PlayerHands hands;
    PlayerCamera view;

    void Awake()
    {
        Input = GetComponent<PlayerInputReader>();
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        movement = GetComponent<PlayerMovement>();
        hands = GetComponent<PlayerHands>();
        view = GetComponentInChildren<PlayerCamera>();
    }

    void Update()
    {
        if (IsSeated)
        {
            Vehicle.SetDriverInput(Input.Move);
            UpdateSway();
        }
    }

    void UpdateSway()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 velocity = Vehicle.GetComponent<Rigidbody>().linearVelocity;
        Vector3 acceleration = Vehicle.transform.InverseTransformDirection((velocity - lastVehicleVelocity) / dt);
        lastVehicleVelocity = velocity;

        Vector3 target = Vector3.ClampMagnitude(-acceleration * SwayPerAcceleration, SwayMax);
        sway = Vector3.Lerp(sway, target, 1f - Mathf.Exp(-SwayResponse * dt));

        // Дрожь от дороги растёт со скоростью.
        float shake = velocity.magnitude * RoadShakePerSpeed;
        float t = Time.time * RoadShakeFrequency;
        Vector3 road = new Vector3(Mathf.PerlinNoise(t, 3.1f) - 0.5f, Mathf.PerlinNoise(7.7f, t) - 0.5f, 0f) * (shake * 2f);
        view.SetSeatSway(sway + road);
    }

    void FixedUpdate()
    {
        // Ручник защёлкивается и остаётся после выхода: машину можно оставить на склоне.
        if (IsSeated && Input.ConsumeSpace())
        {
            Vehicle.HandbrakeOn = !Vehicle.HandbrakeOn;
            ForestAudio.Play(ForestSound.Ratchet, 0.6f);
        }

        bool wipersPressed = Input.ConsumeWipers();
        if (IsSeated && wipersPressed)
        {
            Vehicle.CycleWipers();
            ForestAudio.Play(ForestSound.Click, 0.5f);
        }

        bool headlightsPressed = Input.ConsumeHeadlights();
        if (IsSeated && headlightsPressed)
        {
            Vehicle.SetLights(!Vehicle.LightsOn);
            ForestAudio.Play(ForestSound.Click, 0.5f);
        }

        bool recoverPressed = Input.ConsumeRecover();
        if (IsSeated && recoverPressed && Vehicle.IsOverturned)
            Vehicle.RecoverUpright();

        bool interactPressed = Input.ConsumeInteract();
        if (TryPour() || !interactPressed)
            return;

        if (IsSeated)
            Exit();
        else if (InteractionRay.Cast(view.transform, out RaycastHit hit) && InteractionRay.IsFreeVehicle(hit, out VehicleController target))
            Enter(target);
    }

    // С полной канистрой в руках E у машины льёт в бак: садиться с ней незачем, пока бак не полон.
    bool TryPour()
    {
        FuelCan can = null;
        VehicleController target = null;
        bool canPour = !IsSeated && hands.Held != null && hands.Held.TryGetComponent(out can) && can.Liters > 0f
            && InteractionRay.Cast(view.transform, out RaycastHit hit) && InteractionRay.IsFreeVehicle(hit, out target)
            && target.FuelLiters < VehicleController.TankCapacity;

        IsPouring = canPour && Input.InteractHeld;
        if (IsPouring)
        {
            float poured = target.AddFuel(Mathf.Min(PourLitersPerSecond * Time.fixedDeltaTime, can.Liters));
            can.SetLiters(can.Liters - poured);
        }
        ForestAudio.SetPouring(IsPouring);
        return canPour;
    }

    public void Enter(VehicleController target)
    {
        ForestAudio.Play(ForestSound.Door, 0.7f);
        hands.ReleaseHeld();
        hands.enabled = false;
        movement.enabled = false;

        if (PushZone != null)
            PushZone.RemovePlayer(this);

        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None;
        capsule.enabled = false;

        view.EnterSeat();
        transform.SetParent(target.transform, false);
        transform.SetLocalPositionAndRotation(target.DriverSeat.localPosition + SeatedBodyOffset, Quaternion.identity);

        target.Driver = this;
        Vehicle = target;
        if (WorldSync.Instance != null)
            WorldSync.Instance.SetLocalDriver(true);

        if (DayNightCycle.IsDark)
            target.SetLights(true);
    }

    public void Exit()
    {
        if (!TryFindExit(out Vector3 exit))
            return;

        ForestAudio.Play(ForestSound.Door, 0.7f);

        Rigidbody vehicleBody = Vehicle.GetComponent<Rigidbody>();
        Vehicle.SetDriverInput(Vector2.zero);
        Vehicle.Driver = null;
        if (WorldSync.Instance != null)
            WorldSync.Instance.SetLocalDriver(false);

        view.ExitSeat(Vehicle.transform.eulerAngles.y);
        transform.SetParent(null, false);
        transform.SetPositionAndRotation(exit, Quaternion.identity);

        capsule.enabled = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.position = exit;
        body.linearVelocity = vehicleBody.linearVelocity;

        movement.enabled = true;
        hands.enabled = true;
        Vehicle = null;
    }

    bool TryFindExit(out Vector3 exit)
    {
        Transform vehicle = Vehicle.transform;
        float half = capsule.height * 0.5f - capsule.radius;
        foreach (float side in ExitSides)
        {
            exit = vehicle.position + vehicle.right * (side * ExitSideDistance);
            exit.y = OptimizedForestGenerator.TerrainHeight(exit.x, exit.z) + ExitGroundClearance;
            bool blocked = Physics.CheckCapsule(exit + Vector3.up * half, exit - Vector3.up * half, capsule.radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (!blocked)
                return true;
        }
        exit = default;
        return false;
    }
}
