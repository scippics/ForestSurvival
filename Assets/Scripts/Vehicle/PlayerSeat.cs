using UnityEngine;

// Посадка за руль и передача ввода машине. Пока игрок сидит, его физика выключена.
public class PlayerSeat : MonoBehaviour
{
    // Точка сиденья — на уровне таза; центр капсулы ниже неё, чтобы глаза оказались на уровне сидящего.
    static readonly Vector3 SeatedBodyOffset = new Vector3(0f, -0.38f, 0f);
    static readonly float[] ExitSides = { -1f, 1f };
    const float ExitSideDistance = 1.6f;
    const float ExitGroundClearance = 1f;

    public const int NoSeat = -1;
    public const int DriverSeatIndex = 0;

    public VehicleController Vehicle { get; private set; }
    public bool IsSeated => Vehicle != null;
    public int SeatIndex { get; private set; } = NoSeat;
    public bool IsDriver => IsSeated && SeatIndex == DriverSeatIndex;
    public PushableVehicle PushZone { get; set; }
    public PlayerInputReader Input { get; private set; }
    public bool IsPouring { get; private set; }

    // Ключ не имеет коллайдера, чтобы не стать частью кузова: на него смотрят, а не целятся лучом.
    public bool AimingAtKey => IsDriver && Vector3.Angle(view.transform.forward, Vehicle.IgnitionKey.position - view.transform.position) < KeyAimDegrees;

    const float SwayPerAcceleration = 0.006f;
    const float SwayMax = 0.06f;
    const float SwayResponse = 6f;
    const float RoadShakePerSpeed = 0.00035f;
    const float RoadShakeFrequency = 17f;
    const float PourLitersPerSecond = 4f;
    public const float PourReach = 2.8f;
    const float CrankMinSeconds = 0.7f;
    const float CrankMaxSeconds = 1.9f;
    const float CrankDrainPerSecond = 0.035f;
    const float CrankChurnPeriod = 0.24f;
    const float DeadBattery = 0.08f;
    const float NightCrankFactor = 1.35f;
    const float WornCrankFactor = 2f;
    const float KeyAimDegrees = 5f;

    float crankTime;
    float crankNeed;
    float nextChurn;
    bool crankFailed;
    FuelCan pouringCan;
    VehicleController pouringInto;
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
            if (IsDriver)
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
        if (IsDriver && Input.ConsumeSpace())
        {
            Vehicle.HandbrakeOn = !Vehicle.HandbrakeOn;
            ForestAudio.Play(ForestSound.Ratchet, 0.6f);
        }

        bool wipersPressed = Input.ConsumeWipers();
        if (IsDriver && wipersPressed)
        {
            Vehicle.CycleWipers();
            ForestAudio.Play(ForestSound.Click, 0.5f);
        }

        bool headlightsPressed = Input.ConsumeHeadlights();
        if (IsDriver && headlightsPressed)
        {
            Vehicle.SetLights(!Vehicle.LightsOn);
            ForestAudio.Play(ForestSound.Click, 0.5f);
        }

        bool recoverPressed = Input.ConsumeRecover();
        if (IsDriver && recoverPressed && Vehicle.IsOverturned)
            Vehicle.RecoverUpright();

        bool interactPressed = Input.ConsumeInteract();
        if (IsSeated && AimingAtKey)
        {
            UpdateIgnition(interactPressed);
            return;
        }
        StopCranking();

        if (TryPour() || !interactPressed)
            return;

        if (IsSeated)
            Exit();
        else if (InteractionRay.Cast(view.transform, out RaycastHit hit) && InteractionRay.IsCar(hit, out VehicleController target))
        {
            int free = ChooseSeat(target);
            if (free != NoSeat)
                Enter(target, free);
        }
    }

    // За руль садится первый, остальные рядом и сзади. В гараже руль за хостом: он и заводит.
    public int ChooseSeat(VehicleController target)
    {
        bool hostOnly = GarageLobby.InLobby && !Unity.Netcode.NetworkManager.Singleton.IsServer;
        if (!target.IsOccupied && !hostOnly)
            return DriverSeatIndex;

        PlayerAvatar[] avatars = FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
        for (int candidate = DriverSeatIndex + 1; candidate < target.Seats.Count; candidate++)
        {
            bool taken = false;
            foreach (PlayerAvatar avatar in avatars)
                taken |= avatar.NetworkSeat == candidate;
            if (!taken)
                return candidate;
        }
        return NoSeat;
    }

    void StopCranking()
    {
        crankTime = 0f;
        crankFailed = false;
        if (Vehicle != null)
            Vehicle.IsCranking = false;
    }

    // Зажигание: смотреть на ключ и держать E, пока стартер не схватит. Холодной ночью и на битой машине дольше.
    // Короткое нажатие на работающем моторе глушит его.
    void UpdateIgnition(bool pressed)
    {
        if (Vehicle.EngineOn)
        {
            StopCranking();
            if (pressed)
            {
                Vehicle.EngineOn = false;
                ForestAudio.Play(ForestSound.Click, 0.6f);
            }
            return;
        }

        if (!Input.InteractHeld)
        {
            StopCranking();
            return;
        }
        if (crankFailed)
            return;

        if (Vehicle.Battery < DeadBattery)
        {
            ForestAudio.Play(ForestSound.Click, 0.7f);
            crankFailed = true;
            return;
        }

        Vehicle.IsCranking = true;

        if (crankTime <= 0f)
        {
            crankNeed = Random.Range(CrankMinSeconds, CrankMaxSeconds)
                * (DayNightCycle.IsDark ? NightCrankFactor : 1f)
                * Mathf.Lerp(WornCrankFactor, 1f, Vehicle.Condition);
            ForestAudio.Play(ForestSound.Click, 0.6f);
        }

        crankTime += Time.fixedDeltaTime;
        Vehicle.Battery = Mathf.Max(0f, Vehicle.Battery - CrankDrainPerSecond * Time.fixedDeltaTime);
        if (Time.time > nextChurn)
        {
            nextChurn = Time.time + CrankChurnPeriod;
            ForestAudio.Play(ForestSound.Ratchet, 0.22f);
        }

        if (crankTime < crankNeed)
            return;

        crankTime = 0f;
        Vehicle.IsCranking = false;

        // Нажатие, которым заводили, не должно сразу заглушить: мотор глушится только новым нажатием.
        Input.ConsumeInteract();
        if (Vehicle.FuelLiters > 0f)
        {
            Vehicle.EngineOn = true;
            return;
        }

        crankFailed = true;
        ForestAudio.Play(ForestSound.Stall, 0.6f);
    }

    // С полной канистрой в руках E у машины льёт в бак: садиться с ней незачем, пока бак не полон.
    bool TryPour()
    {
        FuelCan can = null;
        VehicleController target = null;
        bool canPour = !IsSeated && hands.Held != null && hands.Held.TryGetComponent(out can) && can.Liters > 0f
            && InteractionRay.Cast(view.transform, out RaycastHit hit) && InteractionRay.IsFreeVehicle(hit, out target)
            && target.FuelLiters < target.TankSize;

        IsPouring = canPour && Input.InteractHeld && Vector3.Distance(transform.position, target.FillerPoint) <= PourReach;
        hands.PourTilt = IsPouring;
        if (IsPouring)
        {
            float poured = target.AddFuel(Mathf.Min(PourLitersPerSecond * Time.fixedDeltaTime, can.Liters));
            can.SetLiters(can.Liters - poured);
            FuelFx.ShowStream(can.SpoutPoint, target.FillerPoint);
        }

        // Крышки открыты только пока льётся: и у канистры, и у машины.
        FuelCan openCan = IsPouring ? can : null;
        VehicleController openCar = IsPouring ? target : null;
        if (pouringCan != openCan)
        {
            if (pouringCan != null)
                pouringCan.SetOpen(false);
            if (openCan != null)
                openCan.SetOpen(true);
            pouringCan = openCan;
        }
        if (pouringInto != openCar)
        {
            if (pouringInto != null)
                pouringInto.FillerOpen = false;
            if (openCar != null)
                openCar.FillerOpen = true;
            pouringInto = openCar;
        }

        ForestAudio.SetPouring(IsPouring);
        return canPour;
    }

    public void Enter(VehicleController target, int seatIndex = DriverSeatIndex)
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
        transform.SetLocalPositionAndRotation(target.Seats[seatIndex].localPosition + SeatedBodyOffset, Quaternion.identity);

        Vehicle = target;
        SeatIndex = seatIndex;
        if (!IsDriver)
            return;

        target.Driver = this;
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
        if (IsDriver)
        {
            Vehicle.SetDriverInput(Vector2.zero);
            Vehicle.Driver = null;
            Vehicle.EngineOn = false;
            Vehicle.IsCranking = false;
            if (WorldSync.Instance != null)
                WorldSync.Instance.SetLocalDriver(false);
        }

        view.ExitSeat(Vehicle.transform.eulerAngles.y);
        transform.SetParent(null, false);
        transform.SetPositionAndRotation(exit, Quaternion.identity);

        capsule.enabled = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.position = exit;
        // Иначе тело сохраняет курс машины, и взгляд расходится с направлением шага.
        body.rotation = Quaternion.identity;
        body.linearVelocity = vehicleBody.linearVelocity;

        movement.enabled = true;
        hands.enabled = true;
        Vehicle = null;
        SeatIndex = NoSeat;
    }

    bool TryFindExit(out Vector3 exit)
    {
        Transform vehicle = Vehicle.transform;
        float half = capsule.height * 0.5f - capsule.radius;
        foreach (float side in ExitSides)
        {
            exit = vehicle.position + vehicle.right * (side * ExitSideDistance);
            exit.y = GarageLobby.GroundHeight(exit.x, exit.z) + ExitGroundClearance;
            bool blocked = Physics.CheckCapsule(exit + Vector3.up * half, exit - Vector3.up * half, capsule.radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (!blocked)
                return true;
        }
        exit = default;
        return false;
    }
}
