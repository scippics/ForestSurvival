using UnityEngine;

// Шланг. Вставляется в бак брошенной машины, потом конец наводится на канистру: подсос идёт, пока зажата
// кнопка, а бензин пойдёт, только если отпустить в зелёной зоне шкалы. Дальше он льётся сам, пока канистра не полна.
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
    const float BarDistance = 9f;

    public const string BarSuck = "Подсос: отпусти в зелёной зоне";

    public float SiphonPressure { get; private set; }
    public bool IsPumping { get; private set; }
    public bool IsFlowing { get; private set; }
    public AbandonedCar Source { get; private set; }
    public FuelCan Target { get; private set; }

    HoseRope rope;
    Vector3 canStart;
    float nextSend;

    void Awake()
    {
        rope = new GameObject("HoseRope").AddComponent<HoseRope>();
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
        {
            StoryUI.Instance.Say("Сухо. В этом баке давно ничего нет.");
            return false;
        }

        Source = car;
        StoryUI.Instance.Say(car.Spec.Name + ": в баке плещется, литров " + Mathf.RoundToInt(car.Liters) + ". Теперь конец шланга к канистре.");
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
            StoryUI.Instance.ShowBar(BarSuck, SiphonPressure / MaxPressure, SuccessMin / MaxPressure, SuccessMax / MaxPressure);
            if (SiphonPressure >= MaxPressure)
            {
                StoryUI.Instance.Say("Подавился бензином!");
                ForestAudio.Play(ForestSound.Cough, 0.8f);
                CancelPump();
            }
            return;
        }

        if (SiphonPressure >= SuccessMin && SiphonPressure <= SuccessMax)
            StartFlow(can);
        else
            StoryUI.Instance.Say("Не пошло: слишком рано отпустил.");
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
        StoryUI.Instance.Say("Пошло. Держи канистру на месте.");
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
            StoryUI.Instance.Say("Шланг выскочил из бака.");
            Detach();
            return;
        }

        if (!IsFlowing)
            return;

        if (Target == null || (Target.transform.position - canStart).sqrMagnitude > CanMovedDistance * CanMovedDistance)
        {
            StoryUI.Instance.Say("Канистру сдвинули, бензин перестал идти.");
            StopFlow();
            return;
        }

        float room = FuelCan.Capacity - Target.Liters;
        float taken = Source.Drain(Mathf.Min(FlowPerSecond * Time.fixedDeltaTime, room));
        Target.SetLiters(Target.Liters + taken);

        Camera view = Camera.main;
        if (view != null && Vector3.Distance(view.transform.position, Target.transform.position) < BarDistance)
            StoryUI.Instance.ShowBar("Канистра " + Mathf.RoundToInt(Target.Liters) + "/" + FuelCan.Capacity + " л, в баке " + Mathf.RoundToInt(Source.Liters) + " л", Target.Liters / FuelCan.Capacity);

        if (Time.time > nextSend && WorldSync.Instance != null)
        {
            nextSend = Time.time + 1f;
            WorldSync.Instance.SendItem(Target.GetComponent<Rigidbody>());
        }

        if (Target.IsFull)
        {
            StoryUI.Instance.Say("Канистра полная.");
            ForestAudio.Play(ForestSound.Click, 0.3f);
            AbandonedCar finished = Source;
            Detach();
            finished.Announce();
        }
        else if (Source.Liters <= 0.01f)
        {
            StoryUI.Instance.Say("Бак высох. В шланге только воздух.");
            Detach();
        }
    }

    void LateUpdate()
    {
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
