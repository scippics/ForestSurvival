using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum StoryStage
{
    None,
    Intro,
    FindFuel,
    GetFuel,
    PourFuel,
    DriveNorth,
    Free
}

// Ведёт пролог: титры, облёт, задачи. Стадии идут только вперёд и опираются на то, что
// игрок сделал на самом деле: залил бак без записки — задача про записку просто пропускается.
public class StoryDirector : MonoBehaviour
{
    const float IntroTimeOfDay = 0.035f;

    const float CardFade = 0.9f;
    const float CardHold = 2.8f;
    const float CardGap = 0.35f;
    const float FlightSeconds = 16f;
    const float FlightFadeSeconds = 2.5f;
    const float TitleFade = 1f;
    const float TitleHold = 2.4f;

    const float PollPeriod = 0.5f;
    const float CampNoteSpawnDistance = 45f;
    const int CampNoteSpotTries = 16;
    const float EndTitleSeconds = 6f;

    public static StoryDirector Instance { get; private set; }

    public StoryStage Stage { get; private set; } = StoryStage.None;
    public bool CampNoteRead { get; private set; }
    public bool InCutscene => GameFlow.State == GameState.Cutscene;

    StoryUI ui;
    PlayerSeat seat;
    PlayerCamera view;
    VehicleController car;
    DayNightCycle cycle;
    OptimizedForestGenerator forest;
    Coroutine intro;
    float nextPoll;
    bool campNoteSpawned;
    StoryStage hostStage;

    void Awake()
    {
        Instance = this;
        ui = gameObject.AddComponent<StoryUI>();
        seat = FindAnyObjectByType<PlayerSeat>();
        view = seat.GetComponentInChildren<PlayerCamera>();
        car = FindAnyObjectByType<VehicleController>();
        cycle = FindAnyObjectByType<DayNightCycle>();
        forest = FindAnyObjectByType<OptimizedForestGenerator>();
    }

    // Новая игра. Клиент попадает в уже идущий мир, поэтому пролог только у хоста.
    public void Begin()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Stage = StoryStage.Free;
            return;
        }

        car.SetFuel(0f);
        car.HandbrakeOn = true;
        cycle.TimeOfDay = IntroTimeOfDay;
        seat.Enter(car);
        car.SetLights(false);

        Stage = StoryStage.Intro;
        ui.SetBlack(1f);
        ui.SetSkipHint(true);
        view.enabled = false;
        GameFlow.SetState(GameState.Cutscene);
        intro = StartCoroutine(PlayIntro());
    }

    public void Resume(StoryStage stage, bool campNoteRead)
    {
        CampNoteRead = campNoteRead;
        Stage = stage == StoryStage.None || stage == StoryStage.Intro ? StoryStage.Free : stage;
        ui.SetGoal(GoalFor(Stage));
    }

    // Клиент не проходит пролог заново: он видит ту же задачу, что и хост.
    public void FollowHost(StoryStage stage)
    {
        if (stage == hostStage)
            return;
        hostStage = stage;
        ui.SetGoal(GoalFor(stage));
    }

    public void SkipIntro()
    {
        if (intro == null)
            return;
        StopCoroutine(intro);
        FinishIntro();
    }

    IEnumerator PlayIntro()
    {
        ForestAudio.SetAmbience(0f);
        foreach (string text in StoryText.IntroCards)
            yield return ShowCard(text);

        ForestAudio.SetAmbience(1f, FlightFadeSeconds);
        yield return Fly();
        yield return ShowTitle();
        FinishIntro();
    }

    IEnumerator ShowCard(string text)
    {
        float total = CardFade * 2f + CardHold + text.Length * 0.02f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            ui.SetCard(text, Mathf.Min(Mathf.Clamp01(t / CardFade), Mathf.Clamp01((total - t) / CardFade)));
            yield return null;
        }
        ui.SetCard("", 0f);
        yield return new WaitForSeconds(CardGap);
    }

    // Камера идёт над просекой к машине и через заднее стекло садится на место водителя.
    IEnumerator Fly()
    {
        Vector3 seatPoint = SeatEye();
        Vector3 back = -car.transform.forward;
        Vector3 left = -car.transform.right;
        float z = seatPoint.z;
        Vector3[] path =
        {
            RoadPoint(z - 95f, 11f),
            RoadPoint(z - 95f, 11f),
            RoadPoint(z - 62f, 7f),
            RoadPoint(z - 30f, 3.6f),
            seatPoint + back * 10f + left * 4f + Vector3.up * 1.2f,
            seatPoint + back * 2.5f + left * 3.6f + Vector3.up * 0.25f,
            seatPoint + left * 1.4f,
            seatPoint,
            seatPoint
        };

        for (float t = 0f; t < FlightSeconds; t += Time.deltaTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, t / FlightSeconds);
            ui.SetBlack(1f - Mathf.Clamp01(t / FlightFadeSeconds));

            Vector3 position = Sample(path, blend);
            Vector3 ahead = Sample(path, Mathf.Min(blend + 0.04f, 1f)) - position;
            Vector3 forward = Vector3.Lerp(ahead.sqrMagnitude > 0.0001f ? ahead.normalized : car.transform.forward, car.transform.forward, Mathf.SmoothStep(0.72f, 1f, blend));
            view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            yield return null;
        }
        ui.SetBlack(0f);
    }

    IEnumerator ShowTitle()
    {
        float total = TitleFade * 2f + TitleHold;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            ui.SetTitle(StoryText.Title, StoryText.TitleLine, Mathf.Min(Mathf.Clamp01(t / TitleFade), Mathf.Clamp01((total - t) / TitleFade)));
            view.transform.SetPositionAndRotation(SeatEye(), Quaternion.LookRotation(car.transform.forward, car.transform.up));
            yield return null;
        }
    }

    Vector3 SeatEye()
    {
        return view.transform.parent.TransformPoint(view.RestPosition);
    }

    static Vector3 RoadPoint(float z, float height)
    {
        float x = OptimizedForestGenerator.RoadCenterX(0, z);
        return new Vector3(x, OptimizedForestGenerator.TerrainHeight(x, z) + height, z);
    }

    static Vector3 Sample(Vector3[] points, float t)
    {
        float scaled = Mathf.Clamp01(t) * (points.Length - 3);
        int i = Mathf.Min(Mathf.FloorToInt(scaled), points.Length - 4);
        float u = scaled - i;
        Vector3 a = points[i], b = points[i + 1], c = points[i + 2], d = points[i + 3];
        return 0.5f * (2f * b + (c - a) * u + (2f * a - 5f * b + 4f * c - d) * u * u + (3f * b - a - 3f * c + d) * u * u * u);
    }

    void FinishIntro()
    {
        intro = null;
        ForestAudio.SetAmbience(1f);
        ui.SetBlack(0f);
        ui.SetCard("", 0f);
        ui.SetTitle("", "", 0f);
        ui.SetSkipHint(false);

        view.ResetToBody();
        view.enabled = true;
        GameFlow.SetState(GameState.Playing);

        Advance(StoryStage.FindFuel);
        ui.Say(StoryText.ArrivalFirst);
        ui.Say(StoryText.ArrivalSecond);
    }

    void Advance(StoryStage stage)
    {
        if (stage <= Stage)
            return;
        Stage = stage;
        ui.SetGoal(GoalFor(stage));
    }

    static string GoalFor(StoryStage stage)
    {
        switch (stage)
        {
            case StoryStage.FindFuel: return StoryText.GoalLook;
            case StoryStage.GetFuel: return StoryText.GoalSiphon;
            case StoryStage.PourFuel: return StoryText.GoalPour;
            case StoryStage.DriveNorth: return StoryText.GoalDrive;
            case StoryStage.Free: return StoryText.GoalRiver;
            default: return "";
        }
    }

    // Бензин появился в руках: неважно, из бочки или из канистры со стоянки.
    public void OnFuelInHands()
    {
        if (Stage >= StoryStage.FindFuel)
            Advance(StoryStage.PourFuel);
    }

    public void OnNoteRead(string id)
    {
        if (id == StoryText.KordonNoteId)
        {
            Advance(StoryStage.GetFuel);
        }
        else if (id == StoryText.CampNoteId && !CampNoteRead)
        {
            CampNoteRead = true;
            Advance(StoryStage.Free);
            StartCoroutine(ShowEnd());
        }
    }

    IEnumerator ShowEnd()
    {
        // Сначала игрок дочитывает записку.
        while (ui.NoteOpen)
            yield return null;

        for (float t = 0f; t < EndTitleSeconds; t += Time.deltaTime)
        {
            ui.SetTitle(StoryText.EndTitle, StoryText.EndLine, Mathf.Min(Mathf.Clamp01(t), Mathf.Clamp01(EndTitleSeconds - t)));
            yield return null;
        }
        ui.SetTitle("", "", 0f);
    }

    static bool FindOpenGround(Vector3 center, out Vector3 spot)
    {
        for (int i = 0; i < CampNoteSpotTries; i++)
        {
            float angle = i * 2.4f;
            float radius = 3f + i * 0.25f;
            Vector3 origin = center + new Vector3(Mathf.Cos(angle) * radius, 6f, Mathf.Sin(angle) * radius);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.CompareTag(OptimizedForestGenerator.TerrainTag))
            {
                spot = hit.point;
                return true;
            }
        }
        spot = default;
        return false;
    }

    void Update()
    {
        if (seat.Input != null && GameFlow.State == GameState.Cutscene && seat.Input.ConsumeSkip())
            SkipIntro();

        if (GameFlow.State != GameState.Playing || Stage < StoryStage.FindFuel || Stage >= StoryStage.Free || Time.time < nextPoll)
            return;
        nextPoll = Time.time + PollPeriod;

        if (Stage < StoryStage.DriveNorth && car.FuelLiters > 0.1f)
            Advance(StoryStage.DriveNorth);

        if (Stage == StoryStage.DriveNorth)
            TrySpawnCampNote();
    }

    // Записка ложится на стоянку, когда игрок уже рядом: чанк и его земля к этому времени готовы.
    void TrySpawnCampNote()
    {
        if (campNoteSpawned || CampNoteRead || !OptimizedForestGenerator.FindFirstCampNorth(out Vector3 center))
            return;
        if (Vector3.Distance(center, seat.transform.position) > CampNoteSpawnDistance)
            return;

        // Лист должен лечь на открытую землю, а не на крышу навеса. Если коллайдер земли
        // ещё не собрался, ни одна точка не подойдёт, и попытка повторится на следующем опросе.
        if (!FindOpenGround(center, out Vector3 spot))
            return;

        campNoteSpawned = true;
        if (GameObject.Find(ProceduralModels.CampNoteName) != null)
            return;

        GameObject note = ProceduralModels.CreateNote(ProceduralModels.CampNoteName, spot + Vector3.up * 0.15f, StoryText.CampNoteId, StoryText.CampNote);
        forest.Track(note.GetComponent<Rigidbody>());
        ProceduralModels.BuildMarkerStake(spot + new Vector3(0.35f, 0f, 0.2f));
        ui.Say(StoryText.CampArrival);
    }
}
