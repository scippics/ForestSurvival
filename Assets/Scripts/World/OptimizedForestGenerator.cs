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

    const int TreeGridSide = 9;
    const int MaxTreesPerChunk = 60;
    const float TreeDensityFrequency = 0.035f;
    const float TreeNoiseMin = 0.2f;
    const float TreeNoiseMax = 0.5f;
    const float TreeMaxHeight = 19f;
    const float TreeSink = 0.3f;
    const float TrunkModelRadius = 0.13f;
    const float ShadowCullMargin = 12f;

    const float RoadHalfWidth = 5.5f;
    const float RoadSwing = 14f;
    const float RoadFrequency = 0.015f;
    const float RoadColumnStep = 13.37f;
    const float NoiseOffset = 97.3f;

    const float MudFrequency = 0.07f;
    const float MudDepthMin = 0.4f;
    const float MudNoiseMin = 0.3f;
    const float MudNoiseMax = 0.7f;

    const float StartClearingRadius = 30f;

    public const string TerrainTag = "Terrain";

    static readonly Color TrunkColor = new Color(0.2f, 0.14f, 0.09f);
    static readonly Color CrownColor = new Color(0.1f, 0.2f, 0.11f);
    static readonly Color GrassColor = new Color(0.17f, 0.19f, 0.11f);

    public int ActiveChunks => chunks.Count;
    public int ActivePhysicalTrees { get; private set; }
    public int ActiveGrassBlades { get; private set; }
    public int ActiveTerrainPieces { get; private set; }
    public int LastGrassBatchesTotal { get; private set; }
    public int LastGrassBatchesDrawn { get; private set; }
    public int LastTreeChunksDrawn { get; private set; }

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
        public readonly List<CapsuleCollider> Colliders = new List<CapsuleCollider>();
        public readonly List<Matrix4x4[]> GrassBatches = new List<Matrix4x4[]>();
        public int GrassCount;
        public bool GrassPending;
    }

    readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    readonly List<Vector2Int> chunksToRemove = new List<Vector2Int>();
    readonly List<Chunk> pendingGrass = new List<Chunk>();
    readonly List<Chunk> pendingTerrain = new List<Chunk>();
    readonly Stack<CapsuleCollider> colliderPool = new Stack<CapsuleCollider>();
    readonly Stack<Matrix4x4[]> batchPool = new Stack<Matrix4x4[]>();
    readonly Stack<TerrainPiece> terrainPool = new Stack<TerrainPiece>();
    readonly List<Rigidbody> trackedBodies = new List<Rigidbody>();
    readonly Plane[] frustumPlanes = new Plane[6];

    readonly Vector3[] terrainVertices = new Vector3[TerrainVertices * TerrainVertices];
    readonly Vector3[] terrainNormals = new Vector3[TerrainVertices * TerrainVertices];
    readonly Color32[] terrainColors = new Color32[TerrainVertices * TerrainVertices];
    int[] terrainTriangles;

    Mesh trunkMesh, crownMesh, grassMesh;
    Material trunkMaterial, crownMaterial, grassMaterial, groundMaterial;
    Vector2Int centerChunk;

    // В Awake: земля должна появиться раньше первого шага физики, иначе машина и предметы провалятся.
    void Awake()
    {
        trunkMesh = ForestMeshes.BuildTrunk(TrunkModelRadius);
        crownMesh = ForestMeshes.BuildCrown();
        grassMesh = ForestMeshes.BuildGrass();

        trunkMaterial = CreateFoliageMaterial(TrunkColor, 0.3f, 0f);
        crownMaterial = CreateFoliageMaterial(CrownColor, 0.3f, 0.04f);
        grassMaterial = CreateFoliageMaterial(GrassColor, 0.08f, 0.03f);
        groundMaterial = new Material(Resources.Load<Shader>("ForestGround"));
        terrainTriangles = BuildTerrainTriangles();

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

        GenerateNextTerrain();
        GenerateNextGrass();
        UpdateTrackedBodies();
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
        return Mathf.Abs(x - roadCenterX) < RoadHalfWidth || IsInStartClearing(x, z);
    }

    // 0 — сухо, 1 — самая глубокая яма. Глубина плавает вдоль дороги: мелкие участки
    // проходятся на обычной передаче, а из ям выбирается только пониженная.
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
        var chunk = new Chunk { Coord = coord, Trees = new Matrix4x4[MaxTreesPerChunk] };
        GenerateHeights(chunk, out float minHeight, out float maxHeight);

        // Запас у деревьев: дерево за границей кадра всё ещё отбрасывает тень в кадр.
        Vector3 center = new Vector3(coord.x * ChunkSize, 0f, coord.y * ChunkSize);
        float treeTop = maxHeight + TreeMaxHeight + 2f;
        chunk.TreeBounds = new Bounds(center + Vector3.up * (minHeight + treeTop) * 0.5f,
            new Vector3(ChunkSize + ShadowCullMargin * 2f, treeTop - minHeight, ChunkSize + ShadowCullMargin * 2f));
        float grassTop = maxHeight + GrassMaxHeight;
        chunk.GrassBounds = new Bounds(center + Vector3.up * (minHeight + grassTop) * 0.5f,
            new Vector3(ChunkSize, grassTop - minHeight, ChunkSize));

        GenerateTrees(chunk);
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

                terrainColors[index] = new Color32((byte)(MudDepth(x, z) * 255f), 0, 0, 255);
            }
        }

        Mesh mesh = piece.Mesh;
        mesh.vertices = terrainVertices;
        mesh.normals = terrainNormals;
        mesh.colors32 = terrainColors;
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
        float cell = ChunkSize / TreeGridSide;
        float minX = (chunk.Coord.x - 0.5f) * ChunkSize;
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        for (int iz = 0; iz < TreeGridSide; iz++)
        {
            for (int ix = 0; ix < TreeGridSide && chunk.TreeCount < MaxTreesPerChunk; ix++)
            {
                float x = minX + (ix + (float)random.NextDouble()) * cell;
                float z = minZ + (iz + (float)random.NextDouble()) * cell;
                double roll = random.NextDouble();
                float height = Mathf.Lerp(10f, TreeMaxHeight, (float)random.NextDouble());
                float width = Mathf.Lerp(1.8f, 3.2f, (float)random.NextDouble());
                float yaw = (float)random.NextDouble() * 360f;

                float density = Mathf.PerlinNoise(x * TreeDensityFrequency + NoiseOffset, z * TreeDensityFrequency + NoiseOffset);
                if (roll >= Mathf.InverseLerp(TreeNoiseMin, TreeNoiseMax, density))
                    continue;
                if (IsBlockedForPlants(x, z, RoadCenterX(chunk.Coord.x, z)))
                    continue;

                // Ствол немного утоплен: на склоне его основание не должно висеть над землёй.
                chunk.Trees[chunk.TreeCount++] = Matrix4x4.TRS(new Vector3(x, SampleHeight(chunk, x, z) - TreeSink, z),
                    Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width));
            }
        }
    }

    void AcquireNearContent(Chunk chunk)
    {
        if (chunk.Colliders.Count == 0)
        {
            for (int i = 0; i < chunk.TreeCount; i++)
                chunk.Colliders.Add(PlaceTreeCollider(chunk.Trees[i]));
            ActivePhysicalTrees += chunk.TreeCount;
        }

        if (chunk.GrassCount == 0 && !chunk.GrassPending)
        {
            chunk.GrassPending = true;
            pendingGrass.Add(chunk);
        }
    }

    void ReleaseNearContent(Chunk chunk)
    {
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
        capsule.transform.position = tree.GetPosition();
        capsule.radius = TrunkModelRadius * scale.x;
        capsule.height = scale.y;
        capsule.center = Vector3.up * scale.y * 0.5f;
        return capsule;
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
                if (roll >= Mathf.InverseLerp(GrassNoiseMin, GrassNoiseMax, clump))
                    continue;
                if (IsBlockedForPlants(x, z, roadCenter))
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

    void Draw(Camera view)
    {
        GeometryUtility.CalculateFrustumPlanes(view, frustumPlanes);

        int grassTotal = 0;
        int grassDrawn = 0;
        int treeChunksDrawn = 0;

        foreach (Chunk chunk in chunks.Values)
        {
            if (chunk.TreeCount > 0 && GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.TreeBounds))
            {
                Graphics.DrawMeshInstanced(trunkMesh, 0, trunkMaterial, chunk.Trees, chunk.TreeCount, null, ShadowCastingMode.On, true);
                Graphics.DrawMeshInstanced(crownMesh, 0, crownMaterial, chunk.Trees, chunk.TreeCount, null, ShadowCastingMode.On, true);
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
