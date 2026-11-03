using System.Collections.Generic;
using UnityEngine;

// Брошенная машина: стоит мёртвым грузом, не едет. С неё сливают бензин и снимают детали.
// Что уже забрано, хранится по её имени: машина строится заново, когда игрок возвращается в этот чанк.
public class AbandonedCar : MonoBehaviour
{
    const float CapSwing = 110f;
    const float CapResponse = 7f;

    public class State
    {
        public float Liters;
        public int TakenMask;
    }

    public static readonly Dictionary<string, State> States = new Dictionary<string, State>();

    public string Id { get; private set; }
    public WreckSpec Spec { get; private set; }
    public float Liters => state.Liters;
    public Vector3 FillerPoint => filler.position;
    public Transform Filler => filler;
    public readonly List<SalvagePart> Parts = new List<SalvagePart>();

    State state;
    Transform filler;
    Transform cap;
    float capAngle;
    bool capOpen;

    public void Setup(string id, WreckSpec spec, float liters, Transform fillerPoint, Transform fillerCap)
    {
        Id = id;
        Spec = spec;
        filler = fillerPoint;
        cap = fillerCap;

        if (!States.TryGetValue(id, out state))
        {
            state = new State { Liters = liters };
            States[id] = state;
        }
    }

    public void AddPart(SalvagePart part)
    {
        part.Index = Parts.Count;
        part.Car = this;
        Parts.Add(part);
        if ((state.TakenMask & (1 << part.Index)) != 0)
            part.gameObject.SetActive(false);
    }

    public float Drain(float liters)
    {
        float taken = Mathf.Min(liters, state.Liters);
        state.Liters -= taken;
        return taken;
    }

    public void OpenCap()
    {
        capOpen = true;
    }

    public void TakePart(SalvagePart part)
    {
        state.TakenMask |= 1 << part.Index;
        part.gameObject.SetActive(false);
    }

    public void Announce()
    {
        if (WorldSync.Instance != null)
            WorldSync.Instance.AnnounceWreck(Id, state.Liters, state.TakenMask);
    }

    // Новое состояние пришло по сети или из сохранения: машина могла быть уже построена.
    public static void ApplyState(string id, float liters, int takenMask)
    {
        if (!States.TryGetValue(id, out State known))
        {
            known = new State();
            States[id] = known;
        }
        known.Liters = liters;
        known.TakenMask = takenMask;

        foreach (AbandonedCar car in FindObjectsByType<AbandonedCar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (car.Id != id)
                continue;
            foreach (SalvagePart part in car.Parts)
                part.gameObject.SetActive((takenMask & (1 << part.Index)) == 0);
        }
    }

    void Update()
    {
        if (capOpen && capAngle < CapSwing)
        {
            capAngle = Mathf.Lerp(capAngle, CapSwing + 2f, 1f - Mathf.Exp(-CapResponse * Time.deltaTime));
            cap.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(capAngle, 0f, 0f);
        }
    }
}

// Деталь на брошенной машине. Покачивается, пока её откручивают.
public class SalvagePart : MonoBehaviour
{
    public int Index;
    public AbandonedCar Car;
    public CarPartKind Kind;

    Vector3 rest;

    void Awake()
    {
        rest = transform.localPosition;
    }

    public void Wobble(float amount)
    {
        transform.localPosition = rest + new Vector3(Mathf.Sin(Time.time * 37f), Mathf.Sin(Time.time * 53f), Mathf.Sin(Time.time * 29f)) * amount;
    }
}
