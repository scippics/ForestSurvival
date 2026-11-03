using UnityEngine;

// Груз на машине: брёвна на багажнике, ящики на крыше, игрок на капоте. Они утяжеляют её и сдвигают
// центр масс к себе, поэтому пружины под грузом садятся, а колёса сильнее давят в грязь.
[RequireComponent(typeof(Rigidbody))]
public class CarLoad : MonoBehaviour
{
    const float ScanPeriod = 0.2f;
    const float MassResponse = 400f;
    const float CenterResponse = 0.6f;
    const float MaxRelativeSpeed = 2f;
    const int MaxOverlaps = 32;

    // Над капотом, над багажником и над крышей. Сбоку от машины не считается: там просто стоят рядом.
    static readonly Bounds[] Zones =
    {
        new Bounds(new Vector3(0f, 0.7f, 1.55f), new Vector3(1.75f, 1f, 1.15f)),
        new Bounds(new Vector3(0f, 0.85f, -1.65f), new Vector3(1.75f, 1.15f, 0.95f)),
        new Bounds(new Vector3(0f, 1.45f, -0.1f), new Vector3(1.75f, 1f, 2.15f))
    };

    public float LoadMass { get; private set; }
    public Vector3 LoadPoint { get; private set; }

    readonly Collider[] overlaps = new Collider[MaxOverlaps];
    Rigidbody body;
    PlayerHands hands;
    float baseMass;
    Vector3 baseCenter;
    float wantedLoad;
    Vector3 wantedPoint;
    float nextScan;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        baseMass = body.mass;
        baseCenter = body.centerOfMass;
    }

    // Масса машины без груза: её задают установленные детали.
    public void SetBaseMass(float mass)
    {
        baseMass = mass;
        Apply();
    }

    void FixedUpdate()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + ScanPeriod;
            Scan();
        }

        float step = Time.fixedDeltaTime;
        LoadMass = Mathf.MoveTowards(LoadMass, wantedLoad, MassResponse * step);
        LoadPoint = Vector3.MoveTowards(LoadPoint, wantedPoint, CenterResponse * step);
        Apply();
    }

    void Apply()
    {
        float total = baseMass + LoadMass;
        if (!Mathf.Approximately(body.mass, total))
            body.mass = total;

        Vector3 center = baseCenter + (LoadPoint - baseCenter) * (LoadMass / total);
        if ((body.centerOfMass - center).sqrMagnitude > 0.000001f)
            body.centerOfMass = center;
    }

    // Считается только то, что едет вместе с машиной: брошенный мимо предмет пролетит, не задев расчёт.
    void Scan()
    {
        if (hands == null)
        {
            PlayerSeat seat = FindAnyObjectByType<PlayerSeat>();
            if (seat != null)
                hands = seat.GetComponent<PlayerHands>();
        }

        float mass = 0f;
        Vector3 weighted = Vector3.zero;
        foreach (Bounds zone in Zones)
        {
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(zone.center), zone.extents, overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody other = overlaps[i].attachedRigidbody;
                if (other == null || other == body || !IsRiding(other))
                    continue;

                // Деталь с двумя коллайдерами или на границе зон не считается дважды.
                Vector3 local = transform.InverseTransformPoint(other.worldCenterOfMass);
                if (!Contains(zone, local) || !IsFirstZone(zone, local))
                    continue;
                if (HasSeen(other, i, overlaps))
                    continue;

                mass += other.mass;
                weighted += local * other.mass;
            }
        }

        wantedLoad = mass;
        if (mass > 0f)
            wantedPoint = weighted / mass;
    }

    bool IsRiding(Rigidbody other)
    {
        if (hands != null && hands.Held == other)
            return false;
        Vector3 relative = other.linearVelocity - body.GetPointVelocity(other.worldCenterOfMass);
        return other.isKinematic || relative.sqrMagnitude < MaxRelativeSpeed * MaxRelativeSpeed;
    }

    static bool Contains(Bounds zone, Vector3 local)
    {
        return zone.Contains(local);
    }

    static bool IsFirstZone(Bounds zone, Vector3 local)
    {
        foreach (Bounds candidate in Zones)
        {
            if (candidate.Contains(local))
                return candidate.center == zone.center;
        }
        return false;
    }

    static bool HasSeen(Rigidbody other, int index, Collider[] list)
    {
        for (int i = 0; i < index; i++)
        {
            if (list[i].attachedRigidbody == other)
                return true;
        }
        return false;
    }
}
