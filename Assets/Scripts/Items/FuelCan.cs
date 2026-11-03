using UnityEngine;

// Канистра помнит, сколько в ней бензина: полная заметно тяжелее пустой.
[RequireComponent(typeof(Rigidbody))]
public class FuelCan : MonoBehaviour
{
    public const float Capacity = 20f;
    const float KilogramsPerLiter = 0.75f;

    public float Liters { get; private set; }
    public bool IsFull => Liters >= Capacity;

    Rigidbody body;
    float emptyMass;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        emptyMass = body.mass;
    }

    public void SetLiters(float liters)
    {
        Liters = Mathf.Clamp(liters, 0f, Capacity);
        body.mass = emptyMass + Liters * KilogramsPerLiter;
    }
}
