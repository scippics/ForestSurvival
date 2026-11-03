using UnityEngine;

// Удар о что угодно звучит по силе столкновения.
public class ImpactSound : MonoBehaviour
{
    const float Cooldown = 0.2f;

    public float MinSpeed = 1.5f;
    public float FullSpeed = 8f;

    float nextAllowed;

    void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        if (speed < MinSpeed || Time.time < nextAllowed)
            return;

        nextAllowed = Time.time + Cooldown;
        ForestAudio.PlayAt(ForestSound.Bang, collision.GetContact(0).point, Mathf.Clamp01(speed / FullSpeed));
    }
}
