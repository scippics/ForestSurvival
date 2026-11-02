using System.Collections.Generic;
using UnityEngine;

// Триггер — дочерний объект машины, но его сообщения приходят на объект с Rigidbody.
// Сила суммируется по всем, кто толкает, поэтому в кооперативе вдвоём легче.
[RequireComponent(typeof(Rigidbody))]
public class PushableVehicle : MonoBehaviour
{
    const float PushForcePerPlayer = 1500f;
    const float MaxPushSpeed = 2.5f;

    readonly HashSet<PlayerSeat> playersInZone = new HashSet<PlayerSeat>();
    Rigidbody body;
    VehicleController vehicle;

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

    void FixedUpdate()
    {
        Vector3 forward = transform.forward;
        if (Vector3.Dot(body.linearVelocity, forward) >= MaxPushSpeed)
            return;

        foreach (PlayerSeat player in playersInZone)
        {
            if (player.Input.PushHeld)
            {
                body.AddForce(forward * PushForcePerPlayer, ForceMode.Force);
                vehicle.ReleaseWheelsForPush();
            }
        }
    }
}
