using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Бесконечный мир на чанках. Рельеф и деревья видны в сетке 5x5 вокруг камеры, а трава и коллайдеры
// деревьев существуют только в ближней сетке 3x3. Содержимое чанка зависит лишь от его координат и сида,
// поэтому при возврате мир тот же, а у игроков в кооперативе он совпадёт без передачи данных.
public class OptimizedForestGenerator : MonoBehaviour
{
    const float ChunkSize = 50f;
    const int ViewRadius = 2;
    const int NearRadius = 1;
    const int WorldSeed = 1337;

    const int TerrainCells = 25;
    const int TerrainVertices = TerrainCells + 1;
    const float NormalSampleStep = 1f;

    const float HillFrequency = 0.012f;
    const float HillHeight = 8f;
    const float BumpFrequency = 0.09f;
    const float BumpHeight = 2.6f;
    const float RippleFrequency = 0.27f;
    const float RippleHeight = 0.6f;
    const float RoadRoughness = 0.1f;
    const float StartClearingBlend = 15f;

    const int MaxBatchSize = 1023;
    const int GrassGridSide = 110;
    const float GrassClumpFrequency = 0.09f;
    const float GrassNoiseMin = 0.36f;
    const float GrassNoiseMax = 0.6f;
    const float GrassMaxHeight = 1f;
    const float GrassSink = 0.05f;

    const int TreeGridSide = 16;
    const int MaxTreesPerChunk = 256;
    const float TreeDensityFrequency = 0.035f;
    const float TreeNoiseMin = 0.06f;
    const float TreeNoiseMax = 0.3f;
    const float TreeMaxHeight = 25f;
    const float BiomeFrequency = 0.016f;

    const int SpeciesSpruce = 0;
    const int SpeciesPine = 1;
    const int SpeciesBirch = 2;
    const int SpeciesDead = 3;
    const int SpeciesCount = 4;

    const int SaplingsPerChunk = 140;
    const int BushesPerChunk = 140;
    const int StumpsPerChunk = 6;
    const int LogsPerChunk = 4;
    const int MaxSolidPerChunk = MaxTreesPerChunk + StumpsPerChunk + LogsPerChunk;
    const float TreeSink = 0.3f;
    const float TrunkModelRadius = 0.13f;
    const float ShadowCullMargin = 12f;

    const float RoadHalfWidth = 5.5f;
    const float RoadSwing = 14f;
    const float RoadFrequency = 0.015f;
    const float RoadColumnStep = 13.37f;
    const float NoiseOffset = 97.3f;

    const float MudFrequency = 0.07f;
    const float MudDepthMin = 0.25f;
    const float MudNoiseMin = 0.35f;
    const float MudNoiseMax = 0.82f;

    const float StartClearingRadius = 24f;
    const double CampCanFillChance = 0.45;
    const float CampCanMinLiters = 3f;
    const float CampCanMaxLiters = 14f;

    // Поляна целиком лежит в своём чанке, вместе с полосой сглаживания рельефа:
    // тогда соседние чанки не нужно проверять ни для деревьев, ни для высоты.
    const float CampChance = 0.05f;
    const int FirstCampRow = 7;
    const float CampRadius = 9f;
    const float CampBlendRadius = 12f;
    const int CampMinDistanceFromStart = 2;
    const float CampLootMinDistance = 2.5f;
    const float CampLootMaxDistance = 6.5f;
    const int CampLootMin = 3;
    const int CampLootMax = 5;
    const float CampLootDropHeight = 0.5f;

    const float DeadTreeChance = 0.08f;
    const int RocksPerChunk = 14;
    const int FernsPerChunk = 180;
    const float FernNoiseMin = 0.42f;

    public const string TerrainTag = "Terrain";
    public static float ChunkLength => ChunkSize;

    static readonly Color TrunkColor = new Color(0.2f, 0.14f, 0.09f);
    static readonly Color CrownColor = new Color(0.1f, 0.2f, 0.11f);
    static readonly Color GrassColor = new Color(0.17f, 0.19f, 0.11f);
    static readonly Color DeadTrunkColor = new Color(0.17f, 0.15f, 0.13f);
    static readonly Color PineTrunkColor = new Color(0.25f, 0.17f, 0.12f);
    static readonly Color PineCrownColor = new Color(0.08f, 0.18f, 0.13f);
    static readonly Color BirchTrunkColor = new Color(0.72f, 0.7f, 0.64f);
    static readonly Color BirchCrownColor = new Color(0.24f, 0.27f, 0.08f);
    static readonly Color BushColor = new Color(0.075f, 0.14f, 0.055f);
    static readonly Color LogColor = new Color(0.17f, 0.13f, 0.09f);
    static readonly Color FernColor = new Color(0.09f, 0.17f, 0.07f);
    static readonly Color RockColor = new Color(0.27f, 0.27f, 0.26f);

    static readonly Dictionary<Vector2Int, Camp> CampCache = new Dictionary<Vector2Int, Camp>();
    static readonly Dictionary<Vector2Int, WreckSite> WreckCache = new Dictionary<Vector2Int, WreckSite>();

    const double RoadBlockChance = 0.13;
    const int RoadBlockMinRow = 2;
    const double RoadBlockTreeChance = 0.6;
    const double WreckChance = 0.2;
    const double WreckCellsChance = 0.3;
    const double WreckCanChance = 0.15;
    const double WreckKitChance = 0.2;
    const double WreckBatteryChance = 0.25;
    const double WreckPadChance = 0.45;
    const double WreckByRoadChance = 0.6;
    const double DerelictShare = 0.2;
    const float DerelictRadius = 4.6f;
    // Те же числа в CustomWindFoliage.shader.
    const float CrownFadeStart = 26f;
    const float CrownFadeEnd = 34f;
    const float LodRefreshDistance = 4f;

    public int ActiveChunks => chunks.Count;
    public int ActivePhysicalTrees { get; private set; }
    public int ActiveGrassBlades { get; private set; }
    public int ActiveTerrainPieces { get; private set; }
    public int LastGrassBatchesTotal { get; private set; }
    public int LastGrassBatchesDrawn { get; private set; }
    public int LastTreeChunksDrawn { get; private set; }

    // Чанки, где завал уже лёг: сдвинутый не должен появиться снова.
    public List<Vector2Int> VisitedBlocks { get; set; } = new List<Vector2Int>();
    public List<Vector2Int> VisitedWrecks { get; set; } = new List<Vector2Int>();

    // Стоянки, где лут уже выпал: дальше за него отвечает сохранение, а не генератор.
    public List<Vector2Int> VisitedCamps { get; set; } = new List<Vector2Int>();

    struct WreckSite
    {
        public bool Exists;
        public float X;
        public float Z;
        public float Yaw;
        public float Radius;
        public int Spec;
        public int Seed;
        public bool Derelict;
    }

    struct Camp
    {
        public bool Exists;
        public float X;
        public float Z;
    }

    class TerrainPiece
    {
        public GameObject Object;
        public Mesh Mesh;
        public MeshCollider Collider;
    }

    class Chunk
    {
        public Vector2Int Coord;
        public Bounds TreeBounds;
        public Bounds GrassBounds;
        public float[] Heights;
        public TerrainPiece Terrain;
        public bool TerrainPending;
        public Matrix4x4[] Trees;
        public int TreeCount;
        public Matrix4x4[][] Species;
        public int[] SpeciesCounts;
        public Matrix4x4[][] LodClose;
        public Matrix4x4[][] LodFar;
        public int[] LodCloseCounts;
        public int[] LodFarCounts;
        public Vector3 LodEye = new Vector3(1e6f, 0f, 0f);
        public Matrix4x4[] Saplings;
        public int SaplingCount;
        public Matrix4x4[] Bushes;
        public int BushCount;
        public Matrix4x4[] Stumps;
        public int StumpCount;
        public Matrix4x4[] Logs;
        public int LogCount;
        public Matrix4x4[] Rocks;
        public int RockCount;
        public Matrix4x4[] Ferns;
        public int FernCount;
        public readonly List<CapsuleCollider> Colliders = new List<CapsuleCollider>();
        public readonly List<Matrix4x4[]> GrassBatches = new List<Matrix4x4[]>();
        public int GrassCount;
        public bool GrassPending;
    }

    readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    readonly List<Vector2Int> chunksToRemove = new List<Vector2Int>();
    readonly List<Chunk> pendingGrass = new List<Chunk>();
    float grassDensity = 1f;
    readonly List<Chunk> pendingTerrain = new List<Chunk>();
    readonly Stack<CapsuleCollider> colliderPool = new Stack<CapsuleCollider>();
    readonly Stack<Matrix4x4[]> batchPool = new Stack<Matrix4x4[]>();
    readonly Stack<TerrainPiece> terrainPool = new Stack<TerrainPiece>();
    readonly List<Rigidbody> trackedBodies = new List<Rigidbody>();
    public static readonly HashSet<Rigidbody> NetworkFollowed = new HashSet<Rigidbody>();
    readonly Plane[] frustumPlanes = new Plane[6];
    readonly Dictionary<Vector2Int, GameObject> campObjects = new Dictionary<Vector2Int, GameObject>();
    readonly Dictionary<Vector2Int, GameObject> wreckObjects = new Dictionary<Vector2Int, GameObject>();

    readonly Vector3[] terrainVertices = new Vector3[TerrainVertices * TerrainVertices];
    readonly Vector3[] terrainNormals = new Vector3[TerrainVertices * TerrainVertices];
    readonly Color32[] terrainColors = new Color32[TerrainVertices * TerrainVertices];
    readonly Vector2[] terrainRoadOffsets = new Vector2[TerrainVertices * TerrainVertices];
    readonly Vector4[] terrainTangents = new Vector4[TerrainVertices * TerrainVertices];
    int[] terrainTriangles;

    class TreeKind
    {
        public Mesh CrownNear;
        public Mesh CrownFar;
        public Material CrownFadeNear;
        public Material CrownFadeFar;
        public Material Trunk;
        public Material Crown;
    }

    readonly TreeKind[] treeKinds = new TreeKind[SpeciesCount];
    Mesh trunkMesh, grassMesh, fernMesh, stumpMesh, saplingMesh;
    Mesh[] rockMeshes, bushMeshes;
    Material grassMaterial, groundMaterial, fernMaterial, rockMaterial, bushMaterial, stumpMaterial, logMaterial;
    Vector2Int centerChunk;

    // В Awake: земля должна появиться раньше первого шага физики, иначе машина и предметы провалятся.
    void Awake()
    {
        trunkMesh = ForestMeshes.BuildTrunk(TrunkModelRadius);
        saplingMesh = ForestMeshes.BuildSapling();
        grassMesh = ForestMeshes.BuildGrass();
        bushMeshes = NatureMeshes.Bushes(ForestMeshes.BuildBush());
        stumpMesh = ForestMeshes.BuildStump(TrunkModelRadius);
        fernMesh = ForestMeshes.BuildFern();
        rockMeshes = NatureMeshes.Rocks(ForestMeshes.BuildRock());

        Material spruceTrunk = CreateFoliageMaterial(TrunkColor, 0.3f, 0f);
        spruceTrunk.SetFloat("_Bark", 1f);
        spruceTrunk.SetFloat("_Moss", 0.8f);
        Material spruceCrown = CreateFoliageMaterial(CrownColor, 0.3f, 0.04f);
        spruceCrown.SetFloat("_Needles", 1f);
        treeKinds[SpeciesSpruce] = new TreeKind { CrownNear = ForestMeshes.BuildCrown(false), CrownFar = ForestMeshes.BuildCrown(true), Trunk = spruceTrunk, Crown = spruceCrown };

        Material pineTrunk = CreateFoliageMaterial(PineTrunkColor, 0.35f, 0f);
        pineTrunk.SetFloat("_Bark", 1f);
        pineTrunk.SetFloat("_Moss", 0.5f);
        Material pineCrown = CreateFoliageMaterial(PineCrownColor, 0.35f, 0.05f);
        pineCrown.SetFloat("_Needles", 1f);
        treeKinds[SpeciesPine] = new TreeKind { CrownNear = ForestMeshes.BuildPineCrown(false), CrownFar = ForestMeshes.BuildPineCrown(true), Trunk = pineTrunk, Crown = pineCrown };

        Material birchTrunk = CreateFoliageMaterial(BirchTrunkColor, 0.4f, 0f);
        birchTrunk.SetFloat("_Birch", 1f);
        Material birchCrown = CreateFoliageMaterial(BirchCrownColor, 0.4f, 0.07f);
        birchCrown.SetFloat("_Grass", 0f);
        birchCrown.SetFloat("_Needles", 1f);
        treeKinds[SpeciesBirch] = new TreeKind { CrownNear = ForestMeshes.BuildBirchCrown(false), CrownFar = ForestMeshes.BuildBirchCrown(true), Trunk = birchTrunk, Crown = birchCrown };

        Material deadTrunk = CreateFoliageMaterial(DeadTrunkColor, 0.3f, 0f);
        deadTrunk.SetFloat("_Bark", 1f);
        Mesh deadBranches = ForestMeshes.BuildDeadBranches();
        treeKinds[SpeciesDead] = new TreeKind { CrownNear = deadBranches, CrownFar = deadBranches, Trunk = deadTrunk, Crown = deadTrunk };

        foreach (TreeKind kind in treeKinds)
        {
            kind.CrownFadeNear = new Material(kind.Crown);
            kind.CrownFadeNear.SetFloat("_LodSide", 1f);
            kind.CrownFadeFar = new Material(kind.Crown);
            kind.CrownFadeFar.SetFloat("_LodSide", -1f);
        }

        grassMaterial = CreateFoliageMaterial(GrassColor, 0.08f, 0.03f);
        grassMaterial.SetFloat("_Grass", 1f);
        bushMaterial = CreateFoliageMaterial(BushColor, 0.12f, 0.05f);
        bushMaterial.SetFloat("_Needles", 1f);
        if (NatureMeshes.BushLeaves != null)
        {
            bushMaterial.mainTexture = NatureMeshes.BushLeaves;
            bushMaterial.SetFloat("_Cutoff", 0.45f);
        }
        stumpMaterial = CreateFoliageMaterial(DeadTrunkColor, 0f, 0f);
        stumpMaterial.SetFloat("_Bark", 1f);
        stumpMaterial.SetFloat("_Moss", 1f);
        logMaterial = CreateFoliageMaterial(LogColor, 0f, 0f);
        logMaterial.SetFloat("_Bark", 1f);
        logMaterial.SetFloat("_Moss", 1f);
        fernMaterial = CreateFoliageMaterial(FernColor, 0.09f, 0.04f);
        fernMaterial.SetFloat("_Grass", 1f);
        rockMaterial = CreateFoliageMaterial(RockColor, 0f, 0f);
        rockMaterial.SetFloat("_Moss", 1f);
        groundMaterial = new Material(Resources.Load<Shader>("ForestGround"));
        terrainTriangles = BuildTerrainTriangles();
        for (int i = 0; i < terrainTangents.Length; i++)
            terrainTangents[i] = new Vector4(1f, 0f, 0f, 1f);

        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (body.GetComponent<PlayerMovement>() == null)
                trackedBodies.Add(body);
        }

        centerChunk = WorldToChunk(Camera.main.transform.position);
        RefreshChunks();

        // На старте мир должен быть готов сразу; потом он догружается по одному чанку за кадр.
        while (pendingTerrain.Count > 0)
            GenerateNextTerrain();
        while (pendingGrass.Count > 0)
            GenerateNextGrass();
    }

    // После телепорта (загрузка, выход из меню) земля нужна сразу, а не в следующем кадре.
    public void RefreshNow()
    {
        Physics.SyncTransforms();
        centerChunk = WorldToChunk(Camera.main.transform.position);
        RefreshChunks();
        UpdateTrackedBodies();
        Physics.SyncTransforms();
    }

    static Material CreateFoliageMaterial(Color color, float windStrength, float flutterStrength)
    {
        var material = new Material(RuntimeBootstrapper.FoliageMaterial) { enableInstancing = true, color = color };
        material.SetFloat("_WindStrength", windStrength);
        material.SetFloat("_FlutterStrength", flutterStrength);
        return material;
    }

    void Update()
    {
        Camera view = Camera.main;
        if (view == null)
            return;

        Vector2Int center = WorldToChunk(view.transform.position);
        if (center != centerChunk)
        {
            centerChunk = center;
            RefreshChunks();
        }

        if (grassDensity != GameSettings.GrassDensity)
            RegrowGrass();

        GenerateNextTerrain();
        GenerateNextGrass();
        UpdateTrackedBodies();

        // Из гаража леса не видно, и рисовать его незачем.
        if (!GarageLobby.InLobby)
            Draw(view);
    }

    // В LateUpdate: к этому моменту камера и машина уже заняли положение этого кадра.
    void LateUpdate()
    {
        ForestLights.Push();
    }

    static Vector2Int WorldToChunk(Vector3 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x / ChunkSize), Mathf.RoundToInt(position.z / ChunkSize));
    }

    static int ChunkDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    // Просека идёт вдоль Z через каждый столбец чанков. Шум берётся от мировой Z,
    // поэтому на границах чанков полоса не разрывается.
    public static float RoadCenterX(int column, float z)
    {
        float noise = Mathf.Clamp01(Mathf.PerlinNoise(column * RoadColumnStep + NoiseOffset, z * RoadFrequency + NoiseOffset));
        return column * ChunkSize + (noise - 0.5f) * 2f * RoadSwing;
    }

    static bool IsInStartClearing(float x, float z)
    {
        return x * x + z * z < StartClearingRadius * StartClearingRadius;
    }

    static bool IsBlockedForPlants(float x, float z, float roadCenterX)
    {
        return Mathf.Abs(x - roadCenterX) < RoadHalfWidth || IsInStartClearing(x, z) || CampInfluence(x, z) > 0f || IsNearWreck(x, z);
    }

    // Двор кордона зарос: трава растёт везде, кроме вытоптанной земли у построек.
    static bool IsBlockedForGrass(float x, float z, float roadCenterX)
    {
        if (Mathf.Abs(x - roadCenterX) < RoadHalfWidth || CampInfluence(x, z) > 0f || IsNearWreck(x, z))
            return true;
        return IsInStartClearing(x, z) && Kordon.YardBare(x, z) > 0.2f;
    }

    // Брошенные машины: примерно в каждом пятом чанке. Больше половины у дороги, остальные глубже в лесу,
    // куда кто-то когда-то съехал. Вид и место зависят только от координат чанка.
    static WreckSite GetWreckSite(Vector2Int coord)
    {
        if (WreckCache.TryGetValue(coord, out WreckSite site))
            return site;

        var random = new System.Random(ChunkSeed(coord, 11));
        site = new WreckSite();
        if (random.NextDouble() < WreckChance && Mathf.Max(Mathf.Abs(coord.x), Mathf.Abs(coord.y)) >= 1 && !GetCamp(coord).Exists)
        {
            site.Spec = random.Next(WreckBuilder.Catalog.Length);
            WreckSpec spec = WreckBuilder.Catalog[site.Spec];

            // Свой генератор: остальные развалины остаются на прежних местах. Первое число от похожих зёрен
            // почти одинаковое, поэтому оно пропускается.
            var derelictRandom = new System.Random(ChunkSeed(coord, 13));
            derelictRandom.Next();
            derelictRandom.Next();
            site.Derelict = derelictRandom.NextDouble() < DerelictShare;
            float room = ChunkSize * 0.5f - spec.Length * 0.5f - 3f;
            float centerX = coord.x * ChunkSize;
            site.Z = coord.y * ChunkSize + ((float)random.NextDouble() * 2f - 1f) * room;
            float road = RoadCenterX(coord.x, site.Z);

            bool byRoad = random.NextDouble() < WreckByRoadChance || site.Derelict;
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            float offset = RoadHalfWidth + spec.Width * 0.5f + 0.6f + (float)random.NextDouble() * (byRoad ? 2.5f : 14f);
            site.X = Mathf.Clamp(road + side * offset, centerX - room, centerX + room);

            float heading = Mathf.Atan2(RoadCenterX(coord.x, site.Z + 5f) - road, 5f) * Mathf.Rad2Deg;
            site.Yaw = byRoad ? heading + ((float)random.NextDouble() - 0.5f) * 30f + (random.NextDouble() < 0.5 ? 180f : 0f) : (float)random.NextDouble() * 360f;
            site.Radius = site.Derelict ? DerelictRadius : spec.Length * 0.5f + 1.8f;
            site.Seed = ChunkSeed(coord, 12);
            site.Exists = Mathf.Abs(site.X - road) > RoadHalfWidth + spec.Width * 0.4f;
        }

        WreckCache[coord] = site;
        return site;
    }

    static bool IsNearWreck(float x, float z)
    {
        WreckSite site = GetWreckSite(new Vector2Int(Mathf.RoundToInt(x / ChunkSize), Mathf.RoundToInt(z / ChunkSize)));
        if (!site.Exists)
            return false;
        float dx = x - site.X;
        float dz = z - site.Z;
        return dx * dx + dz * dz < site.Radius * site.Radius;
    }

    // Завал ложится поперёк дороги один раз за игру. Дерево закрывает её почти целиком, ветки лежат кучей.
    void AcquireRoadBlock(Chunk chunk)
    {
        Vector2Int coord = chunk.Coord;
        if (Mathf.Abs(coord.y) < RoadBlockMinRow || VisitedBlocks.Contains(coord))
            return;
        VisitedBlocks.Add(coord);

        var random = new System.Random(ChunkSeed(coord, 21));
        if (random.NextDouble() >= RoadBlockChance)
            return;

        float z = coord.y * ChunkSize + ((float)random.NextDouble() * 2f - 1f) * ChunkSize * 0.35f;
        float road = RoadCenterX(coord.x, z);
        string site = coord.x + "." + coord.y;
        if (random.NextDouble() < RoadBlockTreeChance)
        {
            PlaceBlock(site, 0, RoadBlock.KindTree, road + ((float)random.NextDouble() - 0.5f) * 1.5f, z, ((float)random.NextDouble() - 0.5f) * 16f);
            return;
        }

        for (int i = 0; i < 4; i++)
            PlaceBlock(site, i, RoadBlock.KindBranch, road + (i - 1.5f) * 2.1f, z + ((float)random.NextDouble() - 0.5f) * 1.6f, ((float)random.NextDouble() - 0.5f) * 50f);
    }

    static void PlaceBlock(string site, int index, int kind, float x, float z, float yaw)
    {
        var position = new Vector3(x, TerrainHeight(x, z) + 0.17f, z);
        RoadBlock.Create(RoadBlock.MakeName(site, index, kind), kind, position, Quaternion.Euler(0f, yaw, 0f));
    }

    void AcquireWreck(Chunk chunk)
    {
        WreckSite site = GetWreckSite(chunk.Coord);
        if (!site.Exists)
            return;
        if (wreckObjects.TryGetValue(chunk.Coord, out GameObject wreck))
        {
            wreck.SetActive(true);
            return;
        }

        var ground = new Vector3(site.X, TerrainHeight(site.X, site.Z), site.Z);
        string wreckId = chunk.Coord.x + "." + chunk.Coord.y;
        wreck = site.Derelict && DerelictCar.Available
            ? DerelictCar.Spawn(wreckId, ground, site.Yaw, site.Seed)
            : WreckBuilder.Spawn(wreckId, site.Spec, ground, site.Yaw, site.Seed);
        wreck.transform.SetParent(transform, true);
        wreckObjects[chunk.Coord] = wreck;

        // Из бардачка: батарейки и иногда канистра. Раз за игру, как и лут стоянок.
        if (VisitedWrecks.Contains(chunk.Coord))
            return;
        VisitedWrecks.Add(chunk.Coord);
        var random = new System.Random(site.Seed + 5);
        if (random.NextDouble() < WreckCellsChance)
            DropWreckLoot(ProceduralModels.CellsKind, chunk.Coord, 0, ground, site, random);
        if (random.NextDouble() < WreckCanChance)
            DropWreckLoot(ProceduralModels.JerrycanKind, chunk.Coord, 1, ground, site, random);
        if (random.NextDouble() < WreckKitChance)
            DropWreckLoot(ProceduralModels.KitKind, chunk.Coord, 2, ground, site, random);
        if (random.NextDouble() < WreckBatteryChance)
            DropWreckLoot(ProceduralModels.BatteryKind, chunk.Coord, 3, ground, site, random);
        if (random.NextDouble() < WreckPadChance)
            DropWreckLoot(random.NextDouble() < 0.5 ? ProceduralModels.PlankKind : ProceduralModels.StoneKind, chunk.Coord, 4, ground, site, random);
    }

    void DropWreckLoot(string kind, Vector2Int coord, int index, Vector3 ground, WreckSite site, System.Random random)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float x = ground.x + Mathf.Cos(angle) * (site.Radius + 0.3f);
        float z = ground.z + Mathf.Sin(angle) * (site.Radius + 0.3f);
        string lootName = ProceduralModels.LootPrefix + kind + "_w" + coord.x + "_" + coord.y + "_" + index;
        GameObject loot = ProceduralModels.CreateLoot(lootName, new Vector3(x, TerrainHeight(x, z) + CampLootDropHeight, z), Quaternion.Euler(0f, angle * 57f, 0f));
        Track(loot.GetComponent<Rigidbody>());
    }

    void ReleaseWreck(Chunk chunk)
    {
        if (wreckObjects.TryGetValue(chunk.Coord, out GameObject wreck))
            wreck.SetActive(false);
    }

    // Стоянка ставится сбоку от дороги, чтобы её можно было заметить из машины.
    static Camp GetCamp(Vector2Int coord)
    {
        if (CampCache.TryGetValue(coord, out Camp camp))
            return camp;

        var random = new System.Random(ChunkSeed(coord, 4));
        camp = new Camp();
        // На первой просеке стоянка есть всегда: туда ведёт пролог.
        bool firstCamp = coord.x == 0 && coord.y == FirstCampRow;
        if ((random.NextDouble() < CampChance || firstCamp) && Mathf.Max(Mathf.Abs(coord.x), Mathf.Abs(coord.y)) >= CampMinDistanceFromStart)
        {
            float centerX = coord.x * ChunkSize;
            float room = ChunkSize * 0.5f - CampBlendRadius - 0.5f;
            camp.Z = coord.y * ChunkSize + ((float)random.NextDouble() * 2f - 1f) * room;

            float road = RoadCenterX(coord.x, camp.Z);
            float side = road > centerX ? -1f : 1f;
            camp.X = Mathf.Clamp(road + side * (RoadHalfWidth + CampRadius - 1f), centerX - room, centerX + room);
            camp.Exists = true;
        }

        CampCache[coord] = camp;
        return camp;
    }

    // Ближайшая к кордону стоянка на первой просеке: туда ведёт пролог.
    public static bool FindFirstCampNorth(out Vector3 center)
    {
        for (int z = 1; z < 64; z++)
        {
            Camp camp = GetCamp(new Vector2Int(0, z));
            if (camp.Exists)
            {
                center = new Vector3(camp.X, TerrainHeight(camp.X, camp.Z), camp.Z);
                return true;
            }
        }
        center = default;
        return false;
    }

    // 1 — внутри поляны, 0 — за полосой сглаживания. Положительна только у той стоянки,
    // что лежит в этом же чанке, поэтому границы чанков никогда не рвут рельеф.
    static float CampInfluence(float x, float z)
    {
        Camp camp = GetCamp(new Vector2Int(Mathf.RoundToInt(x / ChunkSize), Mathf.RoundToInt(z / ChunkSize)));
        if (!camp.Exists)
            return 0f;

        float dx = x - camp.X;
        float dz = z - camp.Z;
        float squared = dx * dx + dz * dz;
        if (squared >= CampBlendRadius * CampBlendRadius)
            return 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CampBlendRadius, CampRadius, Mathf.Sqrt(squared)));
    }

    // 0 — сухо, 1 — самая глубокая яма. Глубина плавает вдоль дороги: на мелких участках
    // машина едет почти свободно, а в ямах заметно теряет скорость.
    public static float MudDepth(float x, float z)
    {
        int column = Mathf.RoundToInt(x / ChunkSize);
        if (Mathf.Abs(x - RoadCenterX(column, z)) >= RoadHalfWidth || IsInStartClearing(x, z))
            return 0f;

        float noise = Mathf.PerlinNoise(x * MudFrequency + NoiseOffset, z * MudFrequency + NoiseOffset * 2f);
        return Mathf.Lerp(MudDepthMin, 1f, Mathf.InverseLerp(MudNoiseMin, MudNoiseMax, noise));
    }

    // Холмы одинаковы везде, чтобы дорога плавно шла по склонам, а кочки на ней сглажены.
    // Стартовая поляна остаётся ровной: на ней стоит тестовый полигон.
    public static float TerrainHeight(float x, float z)
    {
        float clearing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StartClearingRadius, StartClearingRadius + StartClearingBlend, Mathf.Sqrt(x * x + z * z)));
        if (clearing <= 0f)
            return 0f;

        float hills = (Mathf.PerlinNoise(x * HillFrequency + NoiseOffset, z * HillFrequency + NoiseOffset) - 0.5f) * HillHeight;
        float bumps = (Mathf.PerlinNoise(x * BumpFrequency + NoiseOffset * 3f, z * BumpFrequency + NoiseOffset) - 0.5f) * BumpHeight
                      + (Mathf.PerlinNoise(x * RippleFrequency + NoiseOffset, z * RippleFrequency + NoiseOffset * 3f) - 0.5f) * RippleHeight;

        // Переход шириной в половину дороги: на границе столбцов чанков рельеф всегда лесной и стыки не рвутся.
        int column = Mathf.RoundToInt(x / ChunkSize);
        float roadDistance = Mathf.Abs(x - RoadCenterX(column, z));
        float roughness = Mathf.Lerp(RoadRoughness, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadHalfWidth, RoadHalfWidth * 2f, roadDistance)));
        roughness = Mathf.Lerp(roughness, RoadRoughness, CampInfluence(x, z));

        return clearing * (hills + bumps * roughness);
    }

    void RefreshChunks()
    {
        chunksToRemove.Clear();
        foreach (KeyValuePair<Vector2Int, Chunk> pair in chunks)
        {
            if (ChunkDistance(pair.Key, centerChunk) > ViewRadius)
                chunksToRemove.Add(pair.Key);
        }

        foreach (Vector2Int coord in chunksToRemove)
        {
            Chunk chunk = chunks[coord];
            ReleaseNearContent(chunk);
            ReleaseTerrain(chunk);
            chunks.Remove(coord);
        }

        for (int z = -ViewRadius; z <= ViewRadius; z++)
        {
            for (int x = -ViewRadius; x <= ViewRadius; x++)
            {
                Vector2Int coord = centerChunk + new Vector2Int(x, z);
                if (!chunks.ContainsKey(coord))
                    chunks.Add(coord, CreateChunk(coord));
            }
        }

        foreach (Chunk chunk in chunks.Values)
        {
            if (ChunkDistance(chunk.Coord, centerChunk) <= NearRadius)
            {
                // Землю под ногами откладывать нельзя: игрок провалится ещё до следующего кадра.
                if (chunk.Terrain == null)
                {
                    if (chunk.TerrainPending)
                    {
                        chunk.TerrainPending = false;
                        pendingTerrain.Remove(chunk);
                    }
                    BuildTerrain(chunk);
                }
                AcquireNearContent(chunk);
            }
            else
            {
                ReleaseNearContent(chunk);
                if (chunk.Terrain == null && !chunk.TerrainPending)
                {
                    chunk.TerrainPending = true;
                    pendingTerrain.Add(chunk);
                }
            }
        }
    }

    // Коллайдеры земли есть только рядом с игроком, поэтому всё, что осталось дальше,
    // замораживается на месте и оживает, когда земля под ним появится снова.
    void UpdateTrackedBodies()
    {
        for (int i = trackedBodies.Count - 1; i >= 0; i--)
        {
            Rigidbody body = trackedBodies[i];
            if (body == null)
            {
                trackedBodies.RemoveAt(i);
                continue;
            }

            // Тело, которое сейчас ведёт другой игрок, движется по сети, а не физикой.
            if (NetworkFollowed.Contains(body))
                continue;

            bool frozen = ChunkDistance(WorldToChunk(body.position), centerChunk) > NearRadius;
            if (body.isKinematic != frozen)
                body.isKinematic = frozen;
        }
    }

    static int ChunkSeed(Vector2Int coord, int salt)
    {
        return unchecked(coord.x * 73856093 ^ coord.y * 19349663 ^ WorldSeed ^ salt);
    }

    Chunk CreateChunk(Vector2Int coord)
    {
        var chunk = new Chunk
        {
            Coord = coord,
            Trees = new Matrix4x4[MaxSolidPerChunk],
            Species = new Matrix4x4[SpeciesCount][],
            SpeciesCounts = new int[SpeciesCount],
            LodClose = new Matrix4x4[SpeciesCount][],
            LodFar = new Matrix4x4[SpeciesCount][],
            LodCloseCounts = new int[SpeciesCount],
            LodFarCounts = new int[SpeciesCount],
            Saplings = new Matrix4x4[SaplingsPerChunk],
            Bushes = new Matrix4x4[BushesPerChunk],
            Stumps = new Matrix4x4[StumpsPerChunk],
            Logs = new Matrix4x4[LogsPerChunk],
            Rocks = new Matrix4x4[RocksPerChunk],
            Ferns = new Matrix4x4[FernsPerChunk]
        };
        GenerateHeights(chunk, out float minHeight, out float maxHeight);

        // Запас у деревьев: дерево за границей кадра всё ещё отбрасывает тень в кадр.
        Vector3 center = new Vector3(coord.x * ChunkSize, 0f, coord.y * ChunkSize);
        float treeTop = maxHeight + TreeMaxHeight + 2f;
        chunk.TreeBounds = new Bounds(center + Vector3.up * (minHeight + treeTop) * 0.5f,
            new Vector3(ChunkSize + ShadowCullMargin * 2f, treeTop - minHeight, ChunkSize + ShadowCullMargin * 2f));
        float grassTop = maxHeight + GrassMaxHeight;
        chunk.GrassBounds = new Bounds(center + Vector3.up * (minHeight + grassTop) * 0.5f,
            new Vector3(ChunkSize, grassTop - minHeight, ChunkSize));

        for (int species = 0; species < SpeciesCount; species++)
        {
            chunk.Species[species] = new Matrix4x4[MaxTreesPerChunk];
            chunk.LodClose[species] = new Matrix4x4[MaxTreesPerChunk];
            chunk.LodFar[species] = new Matrix4x4[MaxTreesPerChunk];
        }

        GenerateTrees(chunk);
        GenerateGroundCover(chunk);
        return chunk;
    }

    static void GenerateHeights(Chunk chunk, out float minHeight, out float maxHeight)
    {
        chunk.Heights = new float[TerrainVertices * TerrainVertices];
        float cell = ChunkSize / TerrainCells;
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;
        minHeight = float.MaxValue;
        maxHeight = float.MinValue;

        for (int iz = 0; iz < TerrainVertices; iz++)
        {
            for (int ix = 0; ix < TerrainVertices; ix++)
            {
                float height = TerrainHeight(minX + ix * cell, minZ + iz * cell);
                chunk.Heights[iz * TerrainVertices + ix] = height;
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }
    }

    // Высота по сетке чанка, а не по шуму: растения стоят на той же поверхности, что нарисована.
    static float SampleHeight(Chunk chunk, float x, float z)
    {
        float cell = ChunkSize / TerrainCells;
        float fx = Mathf.Clamp((x - (chunk.Coord.x - 0.5f) * ChunkSize) / cell, 0f, TerrainCells - 0.001f);
        float fz = Mathf.Clamp((z - (chunk.Coord.y - 0.5f) * ChunkSize) / cell, 0f, TerrainCells - 0.001f);
        int ix = (int)fx;
        int iz = (int)fz;
        float tx = fx - ix;
        float tz = fz - iz;
        float[] heights = chunk.Heights;
        int row = iz * TerrainVertices + ix;
        float near = Mathf.Lerp(heights[row], heights[row + 1], tx);
        float far = Mathf.Lerp(heights[row + TerrainVertices], heights[row + TerrainVertices + 1], tx);
        return Mathf.Lerp(near, far, tz);
    }

    static int[] BuildTerrainTriangles()
    {
        var triangles = new int[TerrainCells * TerrainCells * 6];
        int index = 0;
        for (int iz = 0; iz < TerrainCells; iz++)
        {
            for (int ix = 0; ix < TerrainCells; ix++)
            {
                int corner = iz * TerrainVertices + ix;
                triangles[index++] = corner;
                triangles[index++] = corner + TerrainVertices;
                triangles[index++] = corner + 1;
                triangles[index++] = corner + 1;
                triangles[index++] = corner + TerrainVertices;
                triangles[index++] = corner + TerrainVertices + 1;
            }
        }
        return triangles;
    }

    void GenerateNextTerrain()
    {
        if (pendingTerrain.Count == 0)
            return;

        Chunk chunk = pendingTerrain[pendingTerrain.Count - 1];
        pendingTerrain.RemoveAt(pendingTerrain.Count - 1);
        chunk.TerrainPending = false;
        BuildTerrain(chunk);
    }

    void BuildTerrain(Chunk chunk)
    {
        TerrainPiece piece = terrainPool.Count > 0 ? terrainPool.Pop() : CreateTerrainPiece();
        piece.Object.transform.position = new Vector3(chunk.Coord.x * ChunkSize, 0f, chunk.Coord.y * ChunkSize);

        float cell = ChunkSize / TerrainCells;
        float halfSize = ChunkSize * 0.5f;
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        for (int iz = 0; iz < TerrainVertices; iz++)
        {
            for (int ix = 0; ix < TerrainVertices; ix++)
            {
                int index = iz * TerrainVertices + ix;
                float x = minX + ix * cell;
                float z = minZ + iz * cell;

                terrainVertices[index] = new Vector3(ix * cell - halfSize, chunk.Heights[index], iz * cell - halfSize);

                // Нормаль от самой функции высоты, а не от сетки: так на стыках чанков нет швов света.
                float slopeX = TerrainHeight(x + NormalSampleStep, z) - TerrainHeight(x - NormalSampleStep, z);
                float slopeZ = TerrainHeight(x, z + NormalSampleStep) - TerrainHeight(x, z - NormalSampleStep);
                terrainNormals[index] = new Vector3(-slopeX, NormalSampleStep * 2f, -slopeZ).normalized;

                // Смещение считается от дороги своего столбца: на границе чанка оно не меняет знак,
                // и при сглаживании между вершинами не возникает ложная дорога.
                float bare = Mathf.Max(CampInfluence(x, z), IsInStartClearing(x, z) ? Kordon.YardBare(x, z) : 0f);
                terrainColors[index] = new Color32((byte)(MudDepth(x, z) * 255f), (byte)(bare * 255f), 0, 255);
                terrainRoadOffsets[index] = new Vector2(x - RoadCenterX(chunk.Coord.x, z), 0f);
            }
        }

        Mesh mesh = piece.Mesh;
        mesh.vertices = terrainVertices;
        mesh.normals = terrainNormals;
        mesh.colors32 = terrainColors;
        mesh.uv = terrainRoadOffsets;
        mesh.tangents = terrainTangents;
        if (mesh.GetIndexCount(0) == 0)
            mesh.triangles = terrainTriangles;
        mesh.RecalculateBounds();

        // Повторное присваивание заново строит физическую форму из новых вершин.
        // Объект к этому моменту включён: у выключенного физика не перестраивается.
        piece.Object.SetActive(true);
        piece.Collider.sharedMesh = null;
        piece.Collider.sharedMesh = mesh;

        chunk.Terrain = piece;
        ActiveTerrainPieces++;
    }

    TerrainPiece CreateTerrainPiece()
    {
        var terrainObject = new GameObject("TerrainChunk") { tag = TerrainTag };
        terrainObject.transform.SetParent(transform, false);
        terrainObject.AddComponent<MeshFilter>();
        terrainObject.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;

        var mesh = new Mesh { name = "Terrain" };
        terrainObject.GetComponent<MeshFilter>().sharedMesh = mesh;

        return new TerrainPiece { Object = terrainObject, Mesh = mesh, Collider = terrainObject.AddComponent<MeshCollider>() };
    }

    void ReleaseTerrain(Chunk chunk)
    {
        if (chunk.TerrainPending)
        {
            chunk.TerrainPending = false;
            pendingTerrain.Remove(chunk);
        }

        if (chunk.Terrain == null)
            return;

        chunk.Terrain.Object.SetActive(false);
        terrainPool.Push(chunk.Terrain);
        chunk.Terrain = null;
        ActiveTerrainPieces--;
    }

    void GenerateTrees(Chunk chunk)
    {
        var random = new System.Random(ChunkSeed(chunk.Coord, 1));
        var deadRoll = new System.Random(ChunkSeed(chunk.Coord, 7));
        float cell = ChunkSize / TreeGridSide;
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        for (int iz = 0; iz < TreeGridSide; iz++)
        {
            for (int ix = 0; ix < TreeGridSide; ix++)
            {
                float x = minX + (ix + (float)random.NextDouble()) * cell;
                float z = minZ + (iz + (float)random.NextDouble()) * cell;
                double roll = random.NextDouble();
                float heightRoll = (float)random.NextDouble();
                float widthRoll = (float)random.NextDouble();
                float yaw = (float)random.NextDouble() * 360f;

                float density = Mathf.PerlinNoise(x * TreeDensityFrequency + NoiseOffset, z * TreeDensityFrequency + NoiseOffset);
                if (roll >= Mathf.InverseLerp(TreeNoiseMin, TreeNoiseMax, density))
                    continue;
                if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                    continue;

                int species = ChooseSpecies(x, z, deadRoll);
                float height, width;
                switch (species)
                {
                    case SpeciesPine:
                        height = Mathf.Lerp(15f, TreeMaxHeight, heightRoll);
                        width = Mathf.Lerp(1.4f, 2.3f, widthRoll);
                        break;
                    case SpeciesBirch:
                        height = Mathf.Lerp(9f, 15f, heightRoll);
                        width = Mathf.Lerp(0.75f, 1.25f, widthRoll);
                        break;
                    default:
                        height = Mathf.Lerp(10f, 19f, heightRoll);
                        width = Mathf.Lerp(1.8f, 3.2f, widthRoll);
                        break;
                }

                // Ствол немного утоплен: на склоне его основание не должно висеть над землёй.
                Matrix4x4 tree = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - TreeSink, z),
                    Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width));
                chunk.Trees[chunk.TreeCount++] = tree;
                chunk.Species[species][chunk.SpeciesCounts[species]++] = tree;
            }
        }
    }

    // Шум делит лес на участки: ельник, сосновые гривы на сухом и светлые березняки.
    static int ChooseSpecies(float x, float z, System.Random roll)
    {
        double chance = roll.NextDouble();
        if (chance < DeadTreeChance)
            return SpeciesDead;

        float biome = Mathf.PerlinNoise(x * BiomeFrequency + NoiseOffset * 2f, z * BiomeFrequency + NoiseOffset);
        double spread = roll.NextDouble();
        if (biome > 0.58f)
            return spread < 0.72 ? SpeciesPine : SpeciesSpruce;
        if (biome < 0.4f)
            return spread < 0.62 ? SpeciesBirch : SpeciesSpruce;
        return spread < 0.8 ? SpeciesSpruce : spread < 0.9 ? SpeciesPine : SpeciesBirch;
    }

    void GenerateUndergrowth(Chunk chunk, System.Random random)
    {
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        // Подрост: маленькие ёлки сквозь них можно пройти, поэтому у них нет коллайдера.
        for (int i = 0; i < SaplingsPerChunk; i++)
        {
            float x = minX + (float)random.NextDouble() * ChunkSize;
            float z = minZ + (float)random.NextDouble() * ChunkSize;
            float height = Mathf.Lerp(1.5f, 5f, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                continue;
            float width = height * Mathf.Lerp(0.2f, 0.3f, (float)random.NextDouble());
            chunk.Saplings[chunk.SaplingCount++] = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - 0.05f, z),
                Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width));
        }

        for (int i = 0; i < BushesPerChunk; i++)
        {
            float x = minX + (float)random.NextDouble() * ChunkSize;
            float z = minZ + (float)random.NextDouble() * ChunkSize;
            float size = Mathf.Lerp(0.8f, 2f, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                continue;
            chunk.Bushes[chunk.BushCount++] = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - 0.1f, z),
                Quaternion.Euler(0f, yaw, 0f), new Vector3(size, size * 0.8f, size));
        }

        // Пни и упавшие стволы твёрдые: их коллайдеры идут вместе с деревьями.
        for (int i = 0; i < StumpsPerChunk; i++)
        {
            float x = minX + (float)random.NextDouble() * ChunkSize;
            float z = minZ + (float)random.NextDouble() * ChunkSize;
            float width = Mathf.Lerp(1.6f, 3f, (float)random.NextDouble());
            float height = Mathf.Lerp(0.4f, 0.9f, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                continue;
            Matrix4x4 stump = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - 0.05f, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width));
            chunk.Stumps[chunk.StumpCount++] = stump;
            chunk.Trees[chunk.TreeCount++] = stump;
        }

        for (int i = 0; i < LogsPerChunk; i++)
        {
            float x = minX + (float)random.NextDouble() * ChunkSize;
            float z = minZ + (float)random.NextDouble() * ChunkSize;
            float length = Mathf.Lerp(5f, 11f, (float)random.NextDouble());
            float width = Mathf.Lerp(1.5f, 2.6f, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;

            // Оба конца должны лежать на месте для растений, иначе бревно перегородит дорогу.
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(88f, 0f, 0f);
            Vector3 along = rotation * Vector3.up;
            float endX = x + along.x * length;
            float endZ = z + along.z * length;
            bool insideChunk = endX > minX && endX < minX + ChunkSize && endZ > minZ && endZ < minZ + ChunkSize;
            if (!insideChunk || IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)) || IsBlockedForPlants(endX, endZ, RoadCenterX(chunk.Coord.x, endZ)))
                continue;

            float lift = TrunkModelRadius * width * 0.6f;
            Matrix4x4 log = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) + lift, z), rotation, new Vector3(width, length, width));
            chunk.Logs[chunk.LogCount++] = log;
            chunk.Trees[chunk.TreeCount++] = log;
        }
    }

    // Камни и папоротник без коллайдеров: они слишком малы, чтобы мешать, а машина их просто подминает.
    void GenerateGroundCover(Chunk chunk)
    {
        var random = new System.Random(ChunkSeed(chunk.Coord, 3));
        GenerateUndergrowth(chunk, random);
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        for (int i = 0; i < RocksPerChunk; i++)
        {
            float x = minX + (float)random.NextDouble() * ChunkSize;
            float z = minZ + (float)random.NextDouble() * ChunkSize;
            float size = Mathf.Lerp(0.2f, 0.75f, Mathf.Pow((float)random.NextDouble(), 2f));
            float yaw = (float)random.NextDouble() * 360f;
            if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                continue;
            chunk.Rocks[chunk.RockCount++] = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - size * 0.12f, z),
                Quaternion.Euler(0f, yaw, 0f), new Vector3(size * 1.2f, size * 0.8f, size));
        }

        for (int i = 0; i < FernsPerChunk; i++)
        {
            float x = minX + (float)random.NextDouble() * ChunkSize;
            float z = minZ + (float)random.NextDouble() * ChunkSize;
            float width = Mathf.Lerp(0.8f, 1.5f, (float)random.NextDouble());
            float height = Mathf.Lerp(0.4f, 0.7f, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            if (Mathf.PerlinNoise(x * TreeDensityFrequency * 2f + NoiseOffset, z * TreeDensityFrequency * 2f) < FernNoiseMin)
                continue;
            if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                continue;
            chunk.Ferns[chunk.FernCount++] = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - 0.03f, z),
                Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width));
        }
    }

    public void Track(Rigidbody body)
    {
        trackedBodies.Add(body);
    }

    // Стоянка собирается при первом приближении, а предметы рассыпаются один раз за игру:
    // унесённое не должно появляться снова, когда игрок вернётся.
    void AcquireCamp(Chunk chunk)
    {
        Camp camp = GetCamp(chunk.Coord);
        if (!camp.Exists)
            return;

        float ground = TerrainHeight(camp.X, camp.Z);
        var center = new Vector3(camp.X, ground, camp.Z);

        if (campObjects.TryGetValue(chunk.Coord, out GameObject campObject))
        {
            campObject.SetActive(true);
        }
        else
        {
            campObject = ProceduralModels.BuildCamp(center, ground, new System.Random(ChunkSeed(chunk.Coord, 5)));
            campObject.transform.SetParent(transform, true);
            campObjects[chunk.Coord] = campObject;
        }

        if (!VisitedCamps.Contains(chunk.Coord))
        {
            VisitedCamps.Add(chunk.Coord);
            SpawnCampLoot(chunk.Coord, center);
        }
    }

    void SpawnCampLoot(Vector2Int coord, Vector3 center)
    {
        var random = new System.Random(ChunkSeed(coord, 6));
        int count = random.Next(CampLootMin, CampLootMax + 1);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(CampLootMinDistance, CampLootMaxDistance, (float)random.NextDouble());
            float x = center.x + Mathf.Cos(angle) * distance;
            float z = center.z + Mathf.Sin(angle) * distance;
            double roll = random.NextDouble();
            string kind = roll < 0.4 ? ProceduralModels.JerrycanKind : roll < 0.7 ? ProceduralModels.CellsKind : roll < 0.85 ? ProceduralModels.KitKind : ProceduralModels.BatteryKind;
            string lootName = ProceduralModels.LootPrefix + kind + "_" + coord.x + "_" + coord.y + "_" + i;

            // Канистра лежит на боку или стоит: разбросано как попало, а не выставлено.
            Quaternion rotation = Quaternion.Euler(random.Next(2) * 90f, (float)random.NextDouble() * 360f, 0f);
            var position = new Vector3(x, TerrainHeight(x, z) + CampLootDropHeight, z);
            GameObject loot = ProceduralModels.CreateLoot(lootName, position, rotation);
            Track(loot.GetComponent<Rigidbody>());

            if (loot.TryGetComponent(out CarBatteryItem battery))
                battery.Charge = Mathf.Lerp(0.25f, 1f, (float)random.NextDouble());

            // Брошенные канистры редко бывают полными.
            if (loot.TryGetComponent(out FuelCan can) && random.NextDouble() < CampCanFillChance)
                can.SetLiters(Mathf.Lerp(CampCanMinLiters, CampCanMaxLiters, (float)random.NextDouble()));
        }
    }

    void ReleaseCamp(Chunk chunk)
    {
        if (campObjects.TryGetValue(chunk.Coord, out GameObject campObject))
            campObject.SetActive(false);
    }

    void AcquireNearContent(Chunk chunk)
    {
        if (chunk.Colliders.Count == 0)
        {
            for (int i = 0; i < chunk.TreeCount; i++)
                chunk.Colliders.Add(PlaceTreeCollider(chunk.Trees[i]));
            ActivePhysicalTrees += chunk.TreeCount;
            AcquireCamp(chunk);
            AcquireWreck(chunk);
            AcquireRoadBlock(chunk);
        }

        if (chunk.GrassCount == 0 && !chunk.GrassPending)
        {
            chunk.GrassPending = true;
            pendingGrass.Add(chunk);
        }
    }

    void ReleaseNearContent(Chunk chunk)
    {
        if (chunk.Colliders.Count > 0)
        {
            ReleaseCamp(chunk);
            ReleaseWreck(chunk);
        }

        foreach (CapsuleCollider tree in chunk.Colliders)
        {
            tree.gameObject.SetActive(false);
            colliderPool.Push(tree);
        }
        ActivePhysicalTrees -= chunk.Colliders.Count;
        chunk.Colliders.Clear();

        foreach (Matrix4x4[] batch in chunk.GrassBatches)
            batchPool.Push(batch);
        chunk.GrassBatches.Clear();
        ActiveGrassBlades -= chunk.GrassCount;
        chunk.GrassCount = 0;

        if (chunk.GrassPending)
        {
            chunk.GrassPending = false;
            pendingGrass.Remove(chunk);
        }
    }

    CapsuleCollider PlaceTreeCollider(Matrix4x4 tree)
    {
        CapsuleCollider capsule;
        if (colliderPool.Count > 0)
        {
            capsule = colliderPool.Pop();
            capsule.gameObject.SetActive(true);
        }
        else
        {
            var treeObject = new GameObject("TreeCollider");
            treeObject.transform.SetParent(transform, false);
            capsule = treeObject.AddComponent<CapsuleCollider>();
        }

        Vector3 scale = tree.lossyScale;
        capsule.transform.SetPositionAndRotation(tree.GetPosition(), tree.rotation);
        capsule.radius = TrunkModelRadius * scale.x;
        capsule.height = scale.y;
        capsule.center = Vector3.up * scale.y * 0.5f;
        return capsule;
    }

    // Качество поменяли на ходу: уже выросшая трава пересаживается с новой густотой, ближние чанки первыми.
    void RegrowGrass()
    {
        grassDensity = GameSettings.GrassDensity;
        foreach (Chunk chunk in chunks.Values)
        {
            if (chunk.GrassCount == 0)
                continue;
            foreach (Matrix4x4[] batch in chunk.GrassBatches)
                batchPool.Push(batch);
            chunk.GrassBatches.Clear();
            ActiveGrassBlades -= chunk.GrassCount;
            chunk.GrassCount = 0;
            if (!chunk.GrassPending)
            {
                chunk.GrassPending = true;
                pendingGrass.Add(chunk);
            }
        }
    }

    void GenerateNextGrass()
    {
        if (pendingGrass.Count == 0)
            return;

        // Первым заполняется ближайший чанк: он раньше всех окажется на виду.
        int nearest = 0;
        for (int i = 1; i < pendingGrass.Count; i++)
        {
            if (ChunkDistance(pendingGrass[i].Coord, centerChunk) < ChunkDistance(pendingGrass[nearest].Coord, centerChunk))
                nearest = i;
        }

        Chunk chunk = pendingGrass[nearest];
        pendingGrass.RemoveAt(nearest);
        chunk.GrassPending = false;
        GenerateGrass(chunk);
    }

    void GenerateGrass(Chunk chunk)
    {
        var random = new System.Random(ChunkSeed(chunk.Coord, 2));
        float cell = ChunkSize / GrassGridSide;
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        for (int iz = 0; iz < GrassGridSide; iz++)
        {
            float rowZ = minZ + (iz + 0.5f) * cell;
            float roadCenter = RoadCenterX(chunk.Coord.x, rowZ);

            for (int ix = 0; ix < GrassGridSide; ix++)
            {
                float x = minX + (ix + (float)random.NextDouble()) * cell;
                float z = minZ + (iz + (float)random.NextDouble()) * cell;
                double roll = random.NextDouble();

                // Шум задаёт вероятность, а не порог: края куч получаются рваными.
                float clump = Mathf.PerlinNoise(x * GrassClumpFrequency + NoiseOffset, z * GrassClumpFrequency + NoiseOffset);
                if (roll >= Mathf.InverseLerp(GrassNoiseMin, GrassNoiseMax, clump) * grassDensity)
                    continue;
                if (IsBlockedForGrass(x, z, roadCenter))
                    continue;

                float height = Mathf.Lerp(0.35f, 0.85f, clump) * Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble());
                float width = Mathf.Lerp(0.45f, 0.8f, (float)random.NextDouble());
                float yaw = (float)random.NextDouble() * 360f;
                AddGrassBlade(chunk, Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - GrassSink, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width)));
            }
        }
        ActiveGrassBlades += chunk.GrassCount;
    }

    void AddGrassBlade(Chunk chunk, Matrix4x4 blade)
    {
        int slot = chunk.GrassCount % MaxBatchSize;
        if (slot == 0)
            chunk.GrassBatches.Add(batchPool.Count > 0 ? batchPool.Pop() : new Matrix4x4[MaxBatchSize]);

        chunk.GrassBatches[chunk.GrassBatches.Count - 1][slot] = blade;
        chunk.GrassCount++;
    }

    // Разбивка крон по расстоянию: близкие деревья с настоящими лапами, остальные проще.
    // Считается, только когда камера заметно сдвинулась, а не каждый кадр.
    void RefreshCrownLod(Chunk chunk, Vector3 eye)
    {
        if ((eye - chunk.LodEye).sqrMagnitude < LodRefreshDistance * LodRefreshDistance)
            return;
        chunk.LodEye = eye;

        // Полосы перекрываются с запасом на шаг обновления: в зоне растворения дерево есть в обоих списках.
        float closeLimit = (CrownFadeEnd + LodRefreshDistance) * (CrownFadeEnd + LodRefreshDistance);
        float farLimit = (CrownFadeStart - LodRefreshDistance) * (CrownFadeStart - LodRefreshDistance);
        for (int species = 0; species < SpeciesCount; species++)
        {
            int close = 0, far = 0;
            Matrix4x4[] trees = chunk.Species[species];
            for (int i = 0; i < chunk.SpeciesCounts[species]; i++)
            {
                float dx = trees[i].m03 - eye.x;
                float dz = trees[i].m23 - eye.z;
                float distanceSq = dx * dx + dz * dz;
                if (distanceSq < closeLimit)
                    chunk.LodClose[species][close++] = trees[i];
                if (distanceSq > farLimit)
                    chunk.LodFar[species][far++] = trees[i];
            }
            chunk.LodCloseCounts[species] = close;
            chunk.LodFarCounts[species] = far;
        }
    }

    // Дальше ближней сетки всё скрыто туманом: крона проще, а теней туда не дотянется.
    // Тень близкого дерева рисует простая крона: мелкие лапы в карте теней всё равно не видны.
    void DrawTrees(Chunk chunk, Vector3 eye)
    {
        bool near = ChunkDistance(chunk.Coord, centerChunk) <= NearRadius;
        ShadowCastingMode shadows = near ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (near)
            RefreshCrownLod(chunk, eye);

        for (int species = 0; species < SpeciesCount; species++)
        {
            int count = chunk.SpeciesCounts[species];
            if (count == 0)
                continue;
            TreeKind kind = treeKinds[species];
            Graphics.DrawMeshInstanced(trunkMesh, 0, kind.Trunk, chunk.Species[species], count, null, shadows, near);
            if (!near)
            {
                Graphics.DrawMeshInstanced(kind.CrownFar, 0, kind.Crown, chunk.Species[species], count, null, ShadowCastingMode.Off, false);
                continue;
            }

            // Тень у всех одна, от дальней модели: она не меняется при смене, и никаких скачков на земле нет.
            Graphics.DrawMeshInstanced(kind.CrownFar, 0, kind.Crown, chunk.Species[species], count, null, ShadowCastingMode.ShadowsOnly, false);
            if (chunk.LodCloseCounts[species] > 0)
                Graphics.DrawMeshInstanced(kind.CrownNear, 0, kind.CrownFadeNear, chunk.LodClose[species], chunk.LodCloseCounts[species], null, ShadowCastingMode.Off, true);
            if (chunk.LodFarCounts[species] > 0)
                Graphics.DrawMeshInstanced(kind.CrownFar, 0, kind.CrownFadeFar, chunk.LodFar[species], chunk.LodFarCounts[species], null, ShadowCastingMode.Off, true);
        }

        if (chunk.RockCount > 0)
            Graphics.DrawMeshInstanced(Variant(rockMeshes, chunk), 0, rockMaterial, chunk.Rocks, chunk.RockCount, null, shadows, near);

        if (chunk.SaplingCount > 0)
        {
            TreeKind spruce = treeKinds[SpeciesSpruce];
            Graphics.DrawMeshInstanced(near ? saplingMesh : spruce.CrownFar, 0, spruce.Crown, chunk.Saplings, chunk.SaplingCount, null, shadows, near);
        }

        if (!near)
            return;

        if (chunk.FernCount > 0)
            Graphics.DrawMeshInstanced(fernMesh, 0, fernMaterial, chunk.Ferns, chunk.FernCount, null, ShadowCastingMode.Off, true);
        if (chunk.BushCount > 0)
            Graphics.DrawMeshInstanced(Variant(bushMeshes, chunk), 0, bushMaterial, chunk.Bushes, chunk.BushCount, null, ShadowCastingMode.On, true);
        if (chunk.StumpCount > 0)
            Graphics.DrawMeshInstanced(stumpMesh, 0, stumpMaterial, chunk.Stumps, chunk.StumpCount, null, ShadowCastingMode.On, true);
        if (chunk.LogCount > 0)
            Graphics.DrawMeshInstanced(trunkMesh, 0, logMaterial, chunk.Logs, chunk.LogCount, null, ShadowCastingMode.On, true);
    }

    // Один вид камней и кустов на участок: так они рисуются одной пачкой, а соседние участки отличаются.
    static Mesh Variant(Mesh[] meshes, Chunk chunk)
    {
        int index = chunk.Coord.x * 7 + chunk.Coord.y * 13;
        return meshes[((index % meshes.Length) + meshes.Length) % meshes.Length];
    }

    void Draw(Camera view)
    {
        GeometryUtility.CalculateFrustumPlanes(view, frustumPlanes);

        int grassTotal = 0;
        int grassDrawn = 0;
        int treeChunksDrawn = 0;

        foreach (Chunk chunk in chunks.Values)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.TreeBounds))
            {
                DrawTrees(chunk, view.transform.position);
                treeChunksDrawn++;
            }

            int batches = chunk.GrassBatches.Count;
            grassTotal += batches;
            if (batches == 0 || !GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.GrassBounds))
                continue;

            for (int i = 0; i < batches; i++)
            {
                int count = i < batches - 1 ? MaxBatchSize : chunk.GrassCount - i * MaxBatchSize;
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, chunk.GrassBatches[i], count, null, ShadowCastingMode.Off, true);
                grassDrawn++;
            }
        }

        LastGrassBatchesTotal = grassTotal;
        LastGrassBatchesDrawn = grassDrawn;
        LastTreeChunksDrawn = treeChunksDrawn;
    }
}
