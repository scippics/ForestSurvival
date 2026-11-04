using UnityEngine;

// Канистра помнит, сколько в ней бензина: полная заметно тяжелее пустой.
[RequireComponent(typeof(Rigidbody))]
public class FuelCan : MonoBehaviour
{
    public const float Capacity = 20f;
    const float KilogramsPerLiter = 0.75f;
    const float CapOpenAngle = 120f;

    static readonly Vector3 SpoutLocal = new Vector3(0.1f, 0.24f, 0f);

    public float Liters { get; private set; }
    public bool IsFull => Liters >= Capacity;
    public int Percent => Mathf.CeilToInt(Liters / Capacity * 100f);
    public Vector3 SpoutPoint => transform.TransformPoint(SpoutLocal);

    Rigidbody body;
    Transform cap;
    Vector3 capRestPosition;
    Quaternion capRestRotation;
    float emptyMass;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        emptyMass = body.mass;
        cap = transform.Find("Cap");
        capRestPosition = cap.localPosition;
        capRestRotation = cap.localRotation;
    }

    public void SetLiters(float liters)
    {
        Liters = Mathf.Clamp(liters, 0f, Capacity);
        body.mass = emptyMass + Liters * KilogramsPerLiter;
    }

    // Крышка откидывается на петле, пока бензин идёт в канистру или из неё.
    public void SetOpen(bool open)
    {
        cap.localPosition = capRestPosition + (open ? new Vector3(0.05f, 0.03f, 0f) : Vector3.zero);
        cap.localRotation = capRestRotation * Quaternion.Euler(0f, 0f, open ? -CapOpenAngle : 0f);
    }
}
