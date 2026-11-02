using Unity.Netcode;
using UnityEngine;

// На шаблоне игрока, поэтому есть и у чужих тел, и у бота; управляет только своим.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : NetworkBehaviour
{
    const float WalkSpeed = 4.5f;
    const float BurdenedSpeedFactor = 0.7f;
    const float GroundAcceleration = 40f;
    const float AirAcceleration = 6f;
    const float MaxSlopeAngle = 50f;
    const float GroundProbe = 0.1f;
    const float JumpHeight = 1.05f;
    const float BurdenedJumpHeight = 0.5f;
    const float JumpGroundLockout = 0.2f;

    public bool IsGrounded { get; private set; }

    // Предмет в руках замедляет шаг. Камера берёт отсюда же вес и настоящую скорость,
    // поэтому голова качается и при упоре в стену не качается, даже если клавиша зажата.
    public bool IsBurdened => hands != null && hands.Held != null;
    public float GroundSpeed
    {
        get
        {
            if (!enabled || !IsGrounded)
                return 0f;
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    public static float NormalWalkSpeed => WalkSpeed;

    PlayerInputReader input;
    PlayerCamera view;
    PlayerHands hands;
    Rigidbody body;
    CapsuleCollider capsule;
    Vector3 groundNormal = Vector3.up;
    float jumpLockout;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Без трения капсула не прилипает к стенам, а скорость задаётся только в FixedUpdate.
        capsule.material = new PhysicsMaterial("Player")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
    }

    // Ввод, камера и руки собираются при появлении в сети и только у своего игрока.
    void BindLocalRig()
    {
        input = GetComponent<PlayerInputReader>();
        view = GetComponentInChildren<PlayerCamera>();
        hands = GetComponent<PlayerHands>();
    }

    void FixedUpdate()
    {
        // IsOwner мало: бот принадлежит серверу, как и игрок на хосте.
        if (!IsSpawned || !IsOwner || !NetworkObject.IsPlayerObject)
            return;
        if (input == null)
            BindLocalRig();

        UpdateGround();
        TryJump();

        Vector3 wish = Quaternion.Euler(0f, view.Yaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
        if (IsGrounded)
        {
            wish = Vector3.ProjectOnPlane(wish, groundNormal);
            // Иначе на склоне капсулу без трения медленно сносит вниз.
            body.AddForce(-Vector3.ProjectOnPlane(Physics.gravity, groundNormal), ForceMode.Acceleration);
        }

        Vector3 velocity = body.linearVelocity;
        Vector3 current = IsGrounded ? Vector3.ProjectOnPlane(velocity, groundNormal) : new Vector3(velocity.x, 0f, velocity.z);
        float acceleration = IsGrounded ? GroundAcceleration : AirAcceleration;
        float targetSpeed = IsBurdened ? WalkSpeed * BurdenedSpeedFactor : WalkSpeed;
        Vector3 change = Vector3.ClampMagnitude(wish * targetSpeed - current, acceleration * Time.fixedDeltaTime);
        body.AddForce(change, ForceMode.VelocityChange);
    }

    // Нажатие в воздухе сгорает: иначе оно сработало бы само при приземлении.
    void TryJump()
    {
        if (!input.ConsumeSpace() || !IsGrounded)
            return;

        float height = IsBurdened ? BurdenedJumpHeight : JumpHeight;
        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Sqrt(2f * height * -Physics.gravity.y);
        body.linearVelocity = velocity;

        IsGrounded = false;
        groundNormal = Vector3.up;
        jumpLockout = JumpGroundLockout;
    }

    void UpdateGround()
    {
        // Сразу после прыжка ноги ещё в зоне проверки, а земля гасила бы скорость вверх.
        if (jumpLockout > 0f)
        {
            jumpLockout -= Time.fixedDeltaTime;
            IsGrounded = false;
            groundNormal = Vector3.up;
            return;
        }

        float radius = capsule.radius * 0.95f;
        float distance = capsule.height * 0.5f - radius + GroundProbe;
        IsGrounded = Physics.SphereCast(body.position, radius, Vector3.down, out RaycastHit hit, distance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                     && Vector3.Angle(hit.normal, Vector3.up) <= MaxSlopeAngle;
        groundNormal = IsGrounded ? hit.normal : Vector3.up;
    }
}
