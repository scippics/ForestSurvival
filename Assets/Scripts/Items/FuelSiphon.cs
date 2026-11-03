using UnityEngine;

// Шланг. Вставляется в бак брошенной машины, потом конец наводится на канистру: подсос идёт, пока зажата
// кнопка, а бензин пойдёт, только если отпустить на зелёном секторе манометра. Дальше он льётся сам, пока канистра не полна.
public class FuelSiphon : MonoBehaviour
{
    public const float SuccessMin = 70f;
    public const float SuccessMax = 90f;
    const float PressureRate = 40f;
    const float MaxPressure = 100f;
    const float HoseReach = 5f;
    const float TearDistance = 6.5f;
    const float FlowPerSecond = 1.7f;
    const float CanMovedDistance = 0.6f;
    const float GaugeSize = 0.13f;
    const float GaugeArc = 270f;
    const float GaugeOffset = 0.2f;
    const float PumpTickPeriod = 0.28f;
    static readonly Color GaugeGreen = new Color(0.2f, 1f, 0.3f);
    static readonly Color GaugeNeedle = new Color(1f, 0.28f, 0.08f);

    public float SiphonPressure { get; private set; }
    public bool IsPumping { get; private set; }
    public bool IsFlowing { get; private set; }
    public AbandonedCar Source { get; private set; }
    public FuelCan Target { get; private set; }

    HoseRope rope;
    Vector3 canStart;
    float nextSend;
    float nextTick;
    Transform gauge;
    Transform gaugeNeedle;

    void Awake()
    {
        rope = new GameObject("HoseRope").AddComponent<HoseRope>();
        BuildGauge();
    }

    // Латунный манометр на шланге: стрелка показывает подсос, зелёный сектор показывает, когда отпускать.
    void BuildGauge()
    {
        gauge = new GameObject("PumpGauge").transform;
        gauge.SetParent(transform, false);
        gauge.localPosition = Vector3.up * GaugeOffset;

        Texture2D face = ForestTextures.Dial(100, 20, "ПОДСОС");
        GameObject dial = ProceduralModels.Part(gauge, "GaugeFace", PrimitiveType.Quad, Vector3.zero, Vector3.one * GaugeSize, ProceduralModels.CreateLit(new Color(0.1f, 0.1f, 0.09f)));
        Material material = dial.GetComponent<Renderer>().sharedMaterial;
        material.mainTexture = face;
        material.SetTexture("_EmissionMap", face);
        material.SetColor("_EmissionColor", Color.white * 0.75f);

        Material bezel = ProceduralModels.Lit(new Color(0.6f, 0.47f, 0.2f), 0f, 0f, 0.5f);
        ProceduralModels.Part(gauge, "GaugeBody", PrimitiveType.Cylinder, Vector3.forward * 0.012f, new Vector3(GaugeSize * 1.14f, 0.01f, GaugeSize * 1.14f), bezel, Quaternion.Euler(90f, 0f, 0f));

        Material green = ProceduralModels.CreateLit(Color.black);
        green.SetColor("_EmissionColor", GaugeGreen);
        for (float t = SuccessMin; t <= SuccessMax; t += 2.5f)
        {
            Quaternion turn = NeedleTurn(t / MaxPressure);
            ProceduralModels.Part(gauge, "GaugeZone", PrimitiveType.Cube, turn * Vector3.up * (GaugeSize * 0.42f) + Vector3.back * 0.001f, new Vector3(GaugeSize * 0.05f, GaugeSize * 0.09f, 0.001f), green, turn);
        }

        gaugeNeedle = new GameObject("GaugeNeedle").transform;
        gaugeNeedle.SetParent(gauge, false);
        gaugeNeedle.localPosition = Vector3.back * 0.003f;
        Material needle = ProceduralModels.CreateLit(Color.black);
        needle.SetColor("_EmissionColor", GaugeNeedle);
        ProceduralModels.Part(gaugeNeedle, "NeedleBlade", PrimitiveType.Cube, Vector3.up * (GaugeSize * 0.2f), new Vector3(GaugeSize * 0.03f, GaugeSize * 0.42f, 0.001f), needle);
        gaugeNeedle.localRotation = NeedleTurn(0f);
    }

    static Quaternion NeedleTurn(float fraction)
    {
        return Quaternion.Euler(0f, 0f, GaugeArc * 0.5f - GaugeArc * Mathf.Clamp01(fraction));
    }

    void OnDestroy()
    {
        if (rope != null)
            Destroy(rope.gameObject);
    }

    public bool CanReach(FuelCan can)
    {
        return Source != null && Vector3.Distance(Source.FillerPoint, can.transform.position) <= HoseReach;
    }

    // Возвращает, вошёл ли шланг. Сухой бак слышно сразу: шланг не намокает.
    public bool Insert(AbandonedCar car)
    {
        car.OpenCap();
        ForestAudio.Play(ForestSound.Click, 0.4f);
        if (car.Liters < 0.1f)
            return false;

        Source = car;
        return true;
    }

    public void Detach()
    {
        StopFlow();
        CancelPump();
        Source = null;
    }

    public void BeginPump()
    {
        IsPumping = true;
        SiphonPressure = 0f;
    }

    public void CancelPump()
    {
        IsPumping = false;
        SiphonPressure = 0f;
    }

    public void TickPump(bool buttonHeld, float deltaTime, FuelCan can)
    {
        if (buttonHeld)
        {
            SiphonPressure = Mathf.Min(SiphonPressure + PressureRate * deltaTime, MaxPressure);
            if (Time.time > nextTick)
            {
                nextTick = Time.time + PumpTickPeriod * Mathf.Lerp(1f, 0.4f, SiphonPressure / MaxPressure);
                ForestAudio.Play(ForestSound.Ratchet, 0.12f);
            }
            if (SiphonPressure >= MaxPressure)
            {
                ForestAudio.Play(ForestSound.Cough, 0.8f);
                CancelPump();
            }
            return;
        }

        if (SiphonPressure >= SuccessMin && SiphonPressure <= SuccessMax)
            StartFlow(can);
        else
            ForestAudio.Play(ForestSound.Click, 0.25f);
        CancelPump();
    }

    void StartFlow(FuelCan can)
    {
        Target = can;
        IsFlowing = true;
        canStart = can.transform.position;
        can.SetOpen(true);
        ForestAudio.SetFlowing(true);
        StoryDirector.Instance.OnFuelInHands();
    }

    void StopFlow()
    {
        if (!IsFlowing)
            return;

        IsFlowing = false;
        ForestAudio.SetFlowing(false);
        if (Target != null)
        {
            Target.SetOpen(false);
            if (WorldSync.Instance != null)
                WorldSync.Instance.SendItem(Target.GetComponent<Rigidbody>());
        }
        if (Source != null)
            Source.Announce();
        Target = null;
    }

    void FixedUpdate()
    {
        if (Source == null)
            return;

        // Шланг не резиновый: отошёл слишком далеко, и он выскочил из горловины.
        if (Vector3.Distance(transform.position, Source.FillerPoint) > TearDistance)
        {
            ForestAudio.Play(ForestSound.Click, 0.5f);
            Detach();
            return;
        }

        if (!IsFlowing)
            return;

        if (Target == null || (Target.transform.position - canStart).sqrMagnitude > CanMovedDistance * CanMovedDistance)
        {
            StopFlow();
            return;
        }

        float room = FuelCan.Capacity - Target.Liters;
        float taken = Source.Drain(Mathf.Min(FlowPerSecond * Time.fixedDeltaTime, room));
        Target.SetLiters(Target.Liters + taken);

        if (Time.time > nextSend && WorldSync.Instance != null)
        {
            nextSend = Time.time + 1f;
            WorldSync.Instance.SendItem(Target.GetComponent<Rigidbody>());
        }

        if (Target.IsFull)
        {
            ForestAudio.Play(ForestSound.Click, 0.3f);
            AbandonedCar finished = Source;
            Detach();
            finished.Announce();
        }
        else if (Source.Liters <= 0.01f)
        {
            ForestAudio.Play(ForestSound.Cough, 0.3f);
            Detach();
        }
    }

    void LateUpdate()
    {
        // Манометр всегда смотрит на того, кто держит шланг, и лежит стрелкой на нуле, пока не качают.
        Camera view = Camera.main;
        if (view != null)
            gauge.rotation = Quaternion.LookRotation(gauge.position - view.transform.position, view.transform.up);
        gaugeNeedle.localRotation = NeedleTurn(SiphonPressure / MaxPressure);

        if (Source == null)
        {
            rope.Hide();
            return;
        }

        if (IsFlowing && Target != null)
        {
            Vector3 mouth = Target.SpoutPoint;
            rope.Show(Source.FillerPoint, mouth + Vector3.up * 0.12f);
            FuelFx.ShowStream(mouth + Vector3.up * 0.12f, mouth);
        }
        else
        {
            rope.Show(Source.FillerPoint, transform.position + Vector3.up * 0.1f);
        }
    }
}

// Провисающий шланг из коротких отрезков: простая цепная линия, без физики.
public class HoseRope : MonoBehaviour
{
    const int Segments = 16;
    const float Radius = 0.016f;
    const float SagPerMeter = 0.16f;

    Transform[] parts;

    void Awake()
    {
        parts = new Transform[Segments];
        Material rubber = ProceduralModels.Lit(new Color(0.03f, 0.03f, 0.03f), 0f, 0f, 0.35f);
        for (int i = 0; i < Segments; i++)
            parts[i] = ProceduralModels.Part(transform, "HoseBit", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, rubber).transform;
        Hide();
    }

    public void Hide()
    {
        if (parts[0].gameObject.activeSelf)
        {
            foreach (Transform part in parts)
                part.gameObject.SetActive(false);
        }
    }

    Vector3 Point(Vector3 a, Vector3 b, float t, float sag)
    {
        Vector3 p = Vector3.Lerp(a, b, t);
        p.y -= Mathf.Sin(t * Mathf.PI) * sag;
        float ground = OptimizedForestGenerator.TerrainHeight(p.x, p.z) + Radius;
        p.y = Mathf.Max(p.y, ground);
        return p;
    }

    public void Show(Vector3 a, Vector3 b)
    {
        float sag = Vector3.Distance(a, b) * SagPerMeter + 0.1f;
        Vector3 previous = Point(a, b, 0f, sag);
        for (int i = 0; i < Segments; i++)
        {
            Vector3 next = Point(a, b, (i + 1f) / Segments, sag);
            Vector3 along = next - previous;
            Transform part = parts[i];
            if (!part.gameObject.activeSelf)
                part.gameObject.SetActive(true);
            part.SetPositionAndRotation((previous + next) * 0.5f, Quaternion.FromToRotation(Vector3.up, along.sqrMagnitude > 1e-6f ? along : Vector3.up));
            part.localScale = new Vector3(Radius * 2f, along.magnitude * 0.55f, Radius * 2f);
            previous = next;
        }
    }
}

// Струйка бензина: тонкий блестящий цилиндр, который подрагивает, пока кто-то зовёт его в этом кадре.
public class FuelFx : MonoBehaviour
{
    const float Width = 0.016f;

    static FuelFx instance;

    Transform stream;
    float lastTime = -10f;

    public static void ShowStream(Vector3 from, Vector3 to)
    {
        if (instance == null)
        {
            instance = new GameObject("FuelFx").AddComponent<FuelFx>();
            instance.stream = ProceduralModels.Part(instance.transform, "FuelStream", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, ProceduralModels.Lit(new Color(0.55f, 0.42f, 0.14f), 0f, 0f, 0.92f)).transform;
        }

        instance.lastTime = Time.time;
        Vector3 along = to - from;
        float flicker = 1f + Mathf.Sin(Time.time * 41f) * 0.25f;
        instance.stream.gameObject.SetActive(true);
        instance.stream.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, along));
        instance.stream.localScale = new Vector3(Width * flicker, along.magnitude * 0.5f, Width * flicker);
    }

    void LateUpdate()
    {
        if (Time.time - lastTime > 0.06f && stream.gameObject.activeSelf)
            stream.gameObject.SetActive(false);
    }
}
