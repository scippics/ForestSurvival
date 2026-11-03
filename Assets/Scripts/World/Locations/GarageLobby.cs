using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Гараж: лобби сессии. Он стоит высоко над кордоном в своей сцене, поэтому лес не строит под него новых чанков.
// Игроки рассаживаются в машину, хост заводит её ключом, и сцена гаража выгружается, а все переходят в тайгу.
public class GarageLobby : MonoBehaviour
{
    public const float FloorY = 300f;
    const string SceneName = "Garage";
    const float HalfWidth = 6f;
    const float HalfLength = 8f;
    const float WallHeight = 4.5f;
    const float WallThickness = 0.4f;
    const float CarHeight = 1.2f;
    const float PlayerHeight = 1f;
    const float PlayerSideOffset = 2.6f;
    const float PlayerRowStep = 1.1f;
    const float StartDelay = 1.3f;
    const float FadeSeconds = 0.8f;
    const float LobbyAmbience = 0.2f;

    static readonly Color Concrete = new Color(0.13f, 0.13f, 0.125f);
    static readonly Color Brick = new Color(0.085f, 0.075f, 0.065f);
    static readonly Color DoorPaint = new Color(0.16f, 0.2f, 0.17f);
    static readonly Color Steel = new Color(0.12f, 0.12f, 0.12f);
    static readonly Color Bench = new Color(0.25f, 0.18f, 0.11f);
    static readonly Color DrumPaint = new Color(0.3f, 0.12f, 0.08f);
    static readonly Color LampColor = new Color(1f, 0.8f, 0.55f);

    public static GarageLobby Instance { get; private set; }

    // Свой игрок сейчас в гараже.
    public static bool InLobby { get; private set; }

    Scene scene;
    bool sceneLoaded;
    Vector3 origin;
    VehicleController car;
    Rigidbody carBody;
    PlayerSeat seat;
    Rigidbody playerBody;
    MainMenu menu;
    GameObject[] hiddenWorld;
    bool pendingLoad;
    float engineRunningFor;
    bool startRequested;
    bool leaving;

    void Awake()
    {
        Instance = this;
        origin = new Vector3(Kordon.CarSpot.x, FloorY, Kordon.CarSpot.z);
        car = FindAnyObjectByType<VehicleController>();
        carBody = car.GetComponent<Rigidbody>();
        seat = FindAnyObjectByType<PlayerSeat>();
        playerBody = seat.GetComponent<Rigidbody>();

        scene = SceneManager.CreateScene(SceneName);
        sceneLoaded = true;
        GameObject root = Build();
        SceneManager.MoveGameObjectToScene(root, scene);

        // Машину ведёт хост. Подключившийся позже получит её положение по сети.
        if (Unity.Netcode.NetworkManager.Singleton.IsServer)
            MoveCar(origin + Vector3.up * CarHeight, Quaternion.identity);
    }

    void MoveCar(Vector3 position, Quaternion rotation)
    {
        carBody.position = position;
        carBody.rotation = rotation;
        car.transform.SetPositionAndRotation(position, rotation);
        carBody.linearVelocity = Vector3.zero;
        carBody.angularVelocity = Vector3.zero;
    }

    public static float GroundHeight(float x, float z)
    {
        return InLobby ? FloorY : OptimizedForestGenerator.TerrainHeight(x, z);
    }

    // Игрок вошёл из меню: сессия ещё не началась, поэтому ждём в гараже.
    public void Enter(bool load)
    {
        menu = FindAnyObjectByType<MainMenu>();
        pendingLoad = load;
        InLobby = true;

        // Погода, листья и ночной гость в помещении не нужны.
        hiddenWorld = new[] { GameObject.Find("Atmosphere"), GameObject.Find("Weather"), GameObject.Find("Watcher") };
        foreach (GameObject part in hiddenWorld)
        {
            if (part != null)
                part.SetActive(false);
        }
        ForestAudio.SetAmbience(LobbyAmbience);

        if (Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            car.SetFuel(car.TankSize);
            car.HandbrakeOn = true;
            car.EngineOn = false;
        }

        ulong id = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        float side = id % 2 == 0 ? -1f : 1f;
        Vector3 spot = origin + new Vector3(side * PlayerSideOffset, PlayerHeight, -PlayerRowStep * (id / 2));
        playerBody.position = spot;
        seat.transform.position = spot;
        playerBody.linearVelocity = Vector3.zero;
    }

    // Только хост и только когда мотор схватил. Через сетевую переменную это узнают все, и те, кто зайдёт позже.
    public void RequestStart()
    {
        if (startRequested || WorldSync.Instance == null)
            return;
        startRequested = true;
        WorldSync.Instance.StartSession();
    }

    void Update()
    {
        bool started = WorldSync.Instance != null && WorldSync.Instance.SessionStarted;

        if (InLobby)
        {
            if (started && !leaving)
            {
                leaving = true;
                StartCoroutine(Leave());
                return;
            }

            bool hostKey = Unity.Netcode.NetworkManager.Singleton.IsServer && seat.IsDriver && car.EngineOn;
            engineRunningFor = hostKey ? engineRunningFor + Time.deltaTime : 0f;
            if (engineRunningFor >= StartDelay)
                RequestStart();
            return;
        }

        // Сессия началась, пока этот игрок был в меню: гараж ему уже не понадобится.
        if (started && sceneLoaded && !leaving)
            UnloadScene();
    }

    IEnumerator Leave()
    {
        StoryUI ui = StoryUI.Instance;
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            ui.SetBlack(t / FadeSeconds);
            yield return null;
        }
        ui.SetBlack(1f);

        // Загрузка сама посадит игрока туда, где он был в сохранении.
        bool isServer = Unity.Netcode.NetworkManager.Singleton.IsServer;
        if (pendingLoad && isServer && seat.IsSeated)
            seat.Exit();

        InLobby = false;
        if (isServer)
        {
            car.EngineOn = false;
            car.HandbrakeOn = true;
            MoveCar(Kordon.CarSpot, Kordon.CarRotation);
        }

        if (!seat.IsSeated)
        {
            playerBody.position = Kordon.PlayerSpot;
            seat.transform.position = Kordon.PlayerSpot;
            playerBody.linearVelocity = Vector3.zero;
        }

        UnloadScene();
        foreach (GameObject part in hiddenWorld)
        {
            if (part != null)
                part.SetActive(true);
        }

        menu.BeginTaiga(pendingLoad);

        // У хоста дальше ведёт пролог, а всем остальным просто открывается лес.
        if (StoryDirector.Instance.InCutscene)
            yield break;
        ForestAudio.SetAmbience(1f, FadeSeconds);
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            ui.SetBlack(1f - t / FadeSeconds);
            yield return null;
        }
        ui.SetBlack(0f);
        leaving = false;
    }

    void UnloadScene()
    {
        sceneLoaded = false;
        SceneManager.UnloadSceneAsync(scene);
    }

    GameObject Build()
    {
        var root = new GameObject("Garage");
        Transform t = root.transform;
        t.position = origin;

        Material concrete = ProceduralModels.Lit(Concrete, 0f, 0f, 0.12f, 4);
        Material brick = ProceduralModels.Lit(Brick, 0f, 0f, 0.08f, 4);
        Material door = ProceduralModels.Lit(DoorPaint, 0f, 0f, 0.2f, CarPartModels.SurfaceRust);
        Material steel = ProceduralModels.Lit(Steel, 0f, 0f, 0.3f, 4);
        Material wood = ProceduralModels.Lit(Bench, 0f, 0f, 0.08f, 7);
        Material drum = ProceduralModels.Lit(DrumPaint, 0f, 0f, 0.2f, CarPartModels.SurfaceRust);

        Solid(t, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(HalfWidth * 2f, 0.5f, HalfLength * 2f), concrete);
        Solid(t, "Roof", new Vector3(0f, WallHeight + 0.2f, 0f), new Vector3(HalfWidth * 2f, 0.4f, HalfLength * 2f), concrete);
        foreach (float side in new[] { -1f, 1f })
            Solid(t, "Wall", new Vector3(side * (HalfWidth + WallThickness * 0.5f), WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight + 0.8f, HalfLength * 2f), brick);
        Solid(t, "BackWall", new Vector3(0f, WallHeight * 0.5f, -HalfLength - WallThickness * 0.5f), new Vector3(HalfWidth * 2f, WallHeight + 0.8f, WallThickness), brick);

        // Ворота перед машиной: за ними та самая просека.
        Solid(t, "GarageDoor", new Vector3(0f, WallHeight * 0.5f, HalfLength + WallThickness * 0.5f), new Vector3(HalfWidth * 2f, WallHeight + 0.8f, WallThickness), door);
        for (int i = 0; i < 5; i++)
            ProceduralModels.Part(t, "DoorRib", PrimitiveType.Cube, new Vector3(0f, 0.5f + i * 0.85f, HalfLength - 0.02f), new Vector3(HalfWidth * 2f, 0.05f, 0.04f), steel);

        // Верстак и бочки вдоль стены.
        Solid(t, "BenchTop", new Vector3(-HalfWidth + 0.45f, 0.9f, -3.5f), new Vector3(0.8f, 0.08f, 3.2f), wood);
        foreach (float z in new[] { -4.9f, -2.1f })
            Solid(t, "BenchLeg", new Vector3(-HalfWidth + 0.45f, 0.43f, z), new Vector3(0.7f, 0.86f, 0.08f), steel);
        foreach (float z in new[] { 3.4f, 4.3f })
            Solid(t, "Drum", new Vector3(HalfWidth - 0.6f, 0.45f, z), new Vector3(0.65f, 0.45f, 0.65f), drum, PrimitiveType.Cylinder);
        Solid(t, "TyreStack", new Vector3(-HalfWidth + 0.7f, 0.2f, 4.2f), new Vector3(0.95f, 0.2f, 0.95f), ProceduralModels.Lit(new Color(0.03f, 0.03f, 0.03f), 0f, 0f, 0.1f), PrimitiveType.Cylinder);
        Solid(t, "TyreStack", new Vector3(-HalfWidth + 0.7f, 0.6f, 4.2f), new Vector3(0.95f, 0.2f, 0.95f), ProceduralModels.Lit(new Color(0.03f, 0.03f, 0.03f), 0f, 0f, 0.1f), PrimitiveType.Cylinder);

        foreach (float z in new[] { -4f, 3f })
            AddLamp(t, new Vector3(0f, WallHeight - 0.25f, z));
        return root;
    }

    static void Solid(Transform parent, string name, Vector3 position, Vector3 scale, Material material, PrimitiveType shape = PrimitiveType.Cube)
    {
        ProceduralModels.Part(parent, name, shape, position, scale, material, null, true);
    }

    static void AddLamp(Transform parent, Vector3 position)
    {
        Material glow = ProceduralModels.CreateLit(Color.black);
        glow.SetColor("_EmissionColor", LampColor * 2.5f);
        ProceduralModels.Part(parent, "LampShade", PrimitiveType.Cube, position, new Vector3(0.9f, 0.05f, 0.14f), glow);

        var lamp = new GameObject("GarageLamp").AddComponent<Light>();
        lamp.transform.SetParent(parent, false);
        lamp.transform.localPosition = position + Vector3.down * 0.2f;
        lamp.type = LightType.Point;
        lamp.range = 13f;
        lamp.intensity = 1.1f;
        lamp.color = LampColor;
        lamp.renderMode = LightRenderMode.ForcePixel;
        ForestLights.Register(lamp);
    }
}
