using System.Collections.Generic;
using UnityEngine;

// Машину раскачивают руками: каждое нажатие F это рывок в багажник, выше центра масс, поэтому кузов клюёт
// и пружинит. Рывок раскачивает засевшую машину, а толкать вчетвером легче, чем одному.
[RequireComponent(typeof(Rigidbody))]
public class PushableVehicle : MonoBehaviour
{
    const float SteadyForce = 700f;
    const float PulseForce = 5200f;
    const float PulseSeconds = 0.35f;
    const float MaxPushSpeed = 2.5f;
    const float RockPerPulse = 0.12f;
    static readonly Vector3 PushPoint = new Vector3(0f, 0.3f, -2.1f);

    readonly HashSet<PlayerSeat> playersInZone = new HashSet<PlayerSeat>();
    Rigidbody body;
    VehicleController vehicle;
    bool wasHeld;
    float pulseLeft;
    int pulsesRunning;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        vehicle = GetComponent<VehicleController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerSeat player) && playersInZone.Add(player))
            player.PushZone = this;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PlayerSeat player))
            RemovePlayer(player);
    }

    // Выключенный коллайдер не присылает OnTriggerExit, поэтому при посадке игрок убирается вручную.
    public void RemovePlayer(PlayerSeat player)
    {
        if (playersInZone.Remove(player) && player.PushZone == this)
            player.PushZone = null;
    }

    // Рывок другого игрока приходит по сети к тому, кто считает физику машины.
    public void ApplyPulse()
    {
        pulseLeft = PulseSeconds;
        pulsesRunning++;
        vehicle.RockFree(RockPerPulse);
    }

    void FixedUpdate()
    {
        bool held = false;
        foreach (PlayerSeat player in playersInZone)
            held |= player.Input.PushHeld;

        if (held && !wasHeld)
        {
            // Чужая машина считается у другого игрока: ему и уходит рывок.
            if (body.isKinematic)
            {
                if (WorldSync.Instance != null)
                    WorldSync.Instance.AnnouncePush();
            }
            else
            {
                ApplyPulse();
            }
        }
        wasHeld = held;

        if (body.isKinematic)
            return;

        Vector3 forward = transform.forward;
        float force = held ? SteadyForce : 0f;
        if (pulseLeft > 0f)
        {
            // Несколько рывков сразу складываются: так толкают вдвоём и втроём.
            force += PulseForce * Mathf.Max(1, pulsesRunning);
            pulseLeft -= Time.fixedDeltaTime;
            if (pulseLeft <= 0f)
                pulsesRunning = 0;
        }

        if (force <= 0f || Vector3.Dot(body.linearVelocity, forward) >= MaxPushSpeed)
            return;

        body.AddForceAtPosition(forward * force, transform.TransformPoint(PushPoint), ForceMode.Force);
        vehicle.ReleaseWheelsForPush();
    }
}
