using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    const float WalkSpeed = 4.5f;
    const float GroundAcceleration = 40f;
    const float AirAcceleration = 6f;
    const float MaxSlopeAngle = 50f;
    const float GroundProbe = 0.1f;

    public bool IsGrounded { get; private set; }

    PlayerInputReader input;
    PlayerCamera view;
    Rigidbody body;
    CapsuleCollider capsule;
    Vector3 groundNormal = Vector3.up;

    void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        view = GetComponentInChildren<PlayerCamera>();
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

    void FixedUpdate()
    {
        UpdateGround();

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
        Vector3 change = Vector3.ClampMagnitude(wish * WalkSpeed - current, acceleration * Time.fixedDeltaTime);
        body.AddForce(change, ForceMode.VelocityChange);
    }

    void UpdateGround()
    {
        float radius = capsule.radius * 0.95f;
        float distance = capsule.height * 0.5f - radius + GroundProbe;
        IsGrounded = Physics.SphereCast(body.position, radius, Vector3.down, out RaycastHit hit, distance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                     && Vector3.Angle(hit.normal, Vector3.up) <= MaxSlopeAngle;
        groundNormal = IsGrounded ? hit.normal : Vector3.up;
    }
}
