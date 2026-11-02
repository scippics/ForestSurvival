using UnityEngine;

// Брызги грязи из-под колёс: чем глубже грязь и быстрее колесо, тем их больше.
[RequireComponent(typeof(VehicleController))]
public class WheelMudSpray : MonoBehaviour
{
    const float MinSpeed = 1.5f;
    const float ParticlesPerMeter = 1.6f;
    const float BackwardFactor = 0.35f;
    const float UpwardSpeed = 2.6f;
    const float Spread = 0.9f;

    static readonly Color MudColor = new Color(0.07f, 0.05f, 0.035f, 1f);

    VehicleController vehicle;
    ParticleSystem spray;
    float pending;

    void Start()
    {
        vehicle = GetComponent<VehicleController>();

        var sprayObject = new GameObject("MudSpray");
        sprayObject.transform.SetParent(transform, false);
        spray = sprayObject.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = spray.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.gravityModifier = 1f;
        main.startLifetime = 0.8f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = spray.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = spray.shape;
        shape.enabled = false;

        sprayObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ForestAtmosphere.CreateParticleMaterial(0.1f);
        spray.Play();
    }

    void LateUpdate()
    {
        float speed = Mathf.Abs(vehicle.ForwardSpeed);
        if (speed < MinSpeed || vehicle.CurrentMudDepth <= 0f)
            return;

        pending += speed * vehicle.CurrentMudDepth * ParticlesPerMeter * Time.deltaTime;
        int count = Mathf.FloorToInt(pending);
        pending -= count;

        for (int i = 0; i < count; i++)
        {
            VehicleController.Wheel wheel = vehicle.Wheels[Random.Range(0, vehicle.Wheels.Count)];
            if (!wheel.Collider.GetGroundHit(out WheelHit hit))
                continue;

            spray.Emit(new ParticleSystem.EmitParams
            {
                position = hit.point + Vector3.up * 0.08f,
                velocity = transform.forward * (-Mathf.Sign(vehicle.ForwardSpeed) * speed * BackwardFactor)
                           + Vector3.up * (UpwardSpeed * Random.value)
                           + transform.right * Random.Range(-Spread, Spread),
                startColor = MudColor,
                startSize = Random.Range(0.03f, 0.1f),
                startLifetime = Random.Range(0.4f, 0.9f)
            }, 1);
        }
    }
}
