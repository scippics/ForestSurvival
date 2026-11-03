using UnityEngine;

// То, что ходит вокруг кордона. Ночью стоит в тумане между деревьями, смотрит на игрока
// и исчезает, если подойти ближе или долго смотреть. Вреда пока не наносит: это только присутствие.
public class ForestWatcher : MonoBehaviour
{
    const float MinWaitSeconds = 45f;
    const float MaxWaitSeconds = 130f;
    const float SpawnMinDistance = 26f;
    const float SpawnMaxDistance = 36f;
    const float SpawnHalfAngle = 55f;
    const float VanishDistance = 15f;
    const float StareSeconds = 2.6f;
    const float StareDot = 0.985f;
    const float MaxShownSeconds = 25f;
    const float RoadMargin = 5f;

    static readonly Color BodyColor = new Color(0.012f, 0.012f, 0.014f);
    static readonly Color EyeGlow = new Color(1.6f, 1.75f, 1.4f);

    public bool IsShown => figure.gameObject.activeSelf;
    public Vector3 Position => figure.position;

    Transform figure;
    PlayerSeat seat;
    float nextAppear;
    float shownSince;
    float stare;
    bool announced;

    void Start()
    {
        seat = FindAnyObjectByType<PlayerSeat>();
        figure = BuildFigure();
        figure.gameObject.SetActive(false);
        nextAppear = Time.time + MaxWaitSeconds;
    }

    // Слишком высокий и слишком тонкий: издалека это почти сухой ствол, пока он не повернётся.
    Transform BuildFigure()
    {
        var root = new GameObject("Watcher").transform;
        root.SetParent(transform, false);
        Material black = ProceduralModels.Lit(BodyColor, 0f, 0f, 0f);

        Mesh body = ForestMeshes.BuildLathe("WatcherBody", new[]
        {
            new Vector2(1.1f, 0f), new Vector2(1.12f, 0.085f), new Vector2(1.5f, 0.1f), new Vector2(2.1f, 0.15f), new Vector2(2.32f, 0.13f), new Vector2(2.4f, 0.05f), new Vector2(2.42f, 0f)
        }, 10, 1.2f, 0.7f);
        ProceduralModels.MeshPart(root, "Body", body, Vector3.zero, Vector3.one, black);
        ProceduralModels.Part(root, "Head", PrimitiveType.Sphere, new Vector3(0.04f, 2.58f, 0.02f), new Vector3(0.19f, 0.29f, 0.21f), black, Quaternion.Euler(0f, 0f, -14f));

        // Единственное, что видно в темноте: две бледные точки на месте глаз.
        Material eyes = ProceduralModels.CreateLit(Color.black);
        eyes.SetColor("_EmissionColor", EyeGlow);
        foreach (float side in new[] { -1f, 1f })
            ProceduralModels.Part(root, "Eye", PrimitiveType.Sphere, new Vector3(0.04f + side * 0.045f, 2.63f, 0.1f), Vector3.one * 0.034f, eyes);

        foreach (float side in new[] { -1f, 1f })
        {
            Limb(root, black, new Vector3(side * 0.08f, 1.15f, 0f), new Vector3(side * 0.13f, 0.55f, 0.02f), 0.05f);
            Limb(root, black, new Vector3(side * 0.13f, 0.55f, 0.02f), new Vector3(side * 0.11f, 0f, 0.01f), 0.038f);
            Limb(root, black, new Vector3(side * 0.2f, 2.26f, 0f), new Vector3(side * 0.27f, 1.45f, 0.04f), 0.035f);
            Limb(root, black, new Vector3(side * 0.27f, 1.45f, 0.04f), new Vector3(side * 0.24f, 0.72f, 0.09f), 0.026f);
        }
        return root;
    }

    static void Limb(Transform root, Material material, Vector3 from, Vector3 to, float radius)
    {
        Vector3 along = to - from;
        ProceduralModels.Part(root, "Limb", PrimitiveType.Cylinder, (from + to) * 0.5f, new Vector3(radius * 2f, along.magnitude * 0.5f, radius * 2f), material, Quaternion.FromToRotation(Vector3.up, along));
    }

    // Появляется только там, куда игрок уже смотрит вдаль: иначе его просто никто не заметит.
    public bool TryAppear()
    {
        Camera view = Camera.main;
        if (view == null)
            return false;

        Vector3 flat = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector3 direction = Quaternion.Euler(0f, Random.Range(-SpawnHalfAngle, SpawnHalfAngle), 0f) * flat;
            Vector3 spot = seat.transform.position + direction * Random.Range(SpawnMinDistance, SpawnMaxDistance);
            if (Mathf.Abs(spot.x - OptimizedForestGenerator.RoadCenterX(Mathf.RoundToInt(spot.x / OptimizedForestGenerator.ChunkLength), spot.z)) < RoadMargin)
                continue;

            spot.y = OptimizedForestGenerator.TerrainHeight(spot.x, spot.z);

            // Ствол или холм перед ним скрыли бы всё появление.
            if (Physics.Linecast(view.transform.position, spot + Vector3.up * 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                continue;

            figure.position = spot;
            figure.gameObject.SetActive(true);
            shownSince = Time.time;
            stare = 0f;
            return true;
        }
        return false;
    }

    void Hide()
    {
        figure.gameObject.SetActive(false);
        nextAppear = Time.time + Random.Range(MinWaitSeconds, MaxWaitSeconds);
    }

    void Update()
    {
        if (GameFlow.State != GameState.Playing)
            return;

        if (!IsShown)
        {
            if (DayNightCycle.IsDark && Time.time >= nextAppear && StoryDirector.Instance.Stage >= StoryStage.FindFuel)
            {
                if (!TryAppear())
                    nextAppear = Time.time + 5f;
            }
            return;
        }

        Camera view = Camera.main;
        Vector3 toFigure = figure.position + Vector3.up * 1.6f - view.transform.position;
        Vector3 facing = -Vector3.ProjectOnPlane(toFigure, Vector3.up);
        if (facing.sqrMagnitude > 0.01f)
            figure.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

        if (Vector3.Dot(view.transform.forward, toFigure.normalized) > StareDot)
        {
            stare += Time.deltaTime;
            if (!announced)
            {
                announced = true;
                StoryUI.Instance.Say("Там кто-то стоит.");
            }
        }

        bool tooClose = toFigure.magnitude < VanishDistance;
        bool stared = stare > StareSeconds;
        bool leftAlone = Time.time > shownSince + MaxShownSeconds || !DayNightCycle.IsDark;
        if (tooClose || stared || leftAlone)
        {
            if (tooClose || stared)
                ForestAudio.PlayAt(ForestSound.Crack, figure.position, 0.8f);
            Hide();
        }
    }
}
