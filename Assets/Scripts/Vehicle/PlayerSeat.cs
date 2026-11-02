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
            Vehicle.SetDriverInput(Input.Move, Input.BrakeHeld);
    }

    void FixedUpdate()
    {
        bool lowGearPressed = Input.ConsumeLowGear();
        if (IsSeated && lowGearPressed)
            Vehicle.isLowGear = !Vehicle.isLowGear;

        bool headlightsPressed = Input.ConsumeHeadlights();
        if (IsSeated && headlightsPressed)
            Vehicle.SetLights(!Vehicle.LightsOn);

        if (!Input.ConsumeInteract())
            return;

        if (IsSeated)
            Exit();
        else if (InteractionRay.Cast(view.transform, out RaycastHit hit) && InteractionRay.IsFreeVehicle(hit, out VehicleController target))
            Enter(target);
    }

    void Enter(VehicleController target)
    {
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

        if (DayNightCycle.IsDark)
            target.SetLights(true);
    }

    void Exit()
    {
        if (!TryFindExit(out Vector3 exit))
            return;

        Rigidbody vehicleBody = Vehicle.GetComponent<Rigidbody>();
        Vehicle.SetDriverInput(Vector2.zero, false);
        Vehicle.Driver = null;

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
