using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Бесконечный лес на чанках. Деревья видны в сетке 5x5 вокруг камеры, а трава и коллайдеры
// существуют только в ближней сетке 3x3. Содержимое чанка зависит лишь от его координат и сида,
// поэтому при возврате лес тот же, а у игроков в кооперативе он совпадёт без передачи данных.
public class OptimizedForestGenerator : MonoBehaviour
{
    const float ChunkSize = 50f;
    const int ViewRadius = 2;
    const int NearRadius = 1;
    const int WorldSeed = 1337;

    const int MaxBatchSize = 1023;
    const int GrassGridSide = 110;
    const float GrassClumpFrequency = 0.09f;
    const float GrassNoiseMin = 0.36f;
    const float GrassNoiseMax = 0.6f;
    const float GrassMaxHeight = 1f;

    const int TreeGridSide = 9;
    const int MaxTreesPerChunk = 60;
    const float TreeDensityFrequency = 0.035f;
    const float TreeNoiseMin = 0.2f;
    const float TreeNoiseMax = 0.5f;
    const float TreeMaxHeight = 19f;
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
    const float MudSegmentLength = 2.5f;
    const float MudSegmentOverlap = 0.6f;
    const float MudHeight = 0.02f;

    const float StartClearingRadius = 30f;
    const float GroundRecenterDistance = 5000f;

    static readonly Color TrunkColor = new Color(0.2f, 0.14f, 0.09f);
    static readonly Color CrownColor = new Color(0.1f, 0.2f, 0.11f);
    static readonly Color GrassColor = new Color(0.2f, 0.3f, 0.11f);
    static readonly Color MudColor = new Color(0.11f, 0.08f, 0.055f);

    public Transform Ground { get; set; }

    public int ActiveChunks => chunks.Count;
    public int ActivePhysicalTrees { get; private set; }
    public int ActiveGrassBlades { get; private set; }
    public int LastGrassBatchesTotal { get; private set; }
    public int LastGrassBatchesDrawn { get; private set; }
    public int LastTreeChunksDrawn { get; private set; }

    class Chunk
    {
        public Vector2Int Coord;
        public Bounds TreeBounds;
        public Bounds GrassBounds;
        public Matrix4x4[] Trees;
        public int TreeCount;
        public Matrix4x4[] Mud;
        public int MudCount;
        public readonly List<CapsuleCollider> Colliders = new List<CapsuleCollider>();
        public readonly List<Matrix4x4[]> GrassBatches = new List<Matrix4x4[]>();
        public int GrassCount;
        public bool GrassPending;
    }

    readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    readonly List<Vector2Int> chunksToRemove = new List<Vector2Int>();
    readonly List<Chunk> pendingGrass = new List<Chunk>();
    readonly Stack<CapsuleCollider> colliderPool = new Stack<CapsuleCollider>();
    readonly Stack<Matrix4x4[]> batchPool = new Stack<Matrix4x4[]>();
    readonly Plane[] frustumPlanes = new Plane[6];

    Mesh trunkMesh, crownMesh, grassMesh, mudMesh;
    Material trunkMaterial, crownMaterial, grassMaterial, mudMaterial;
    Vector2Int centerChunk;

    void Start()
    {
        trunkMesh = ForestMeshes.BuildTrunk(TrunkModelRadius);
        crownMesh = ForestMeshes.BuildCrown();
        grassMesh = ForestMeshes.BuildGrass();
        mudMesh = ForestMeshes.BuildGroundQuad();

        trunkMaterial = CreateFoliageMaterial(TrunkColor, 0.3f, 0f);
        crownMaterial = CreateFoliageMaterial(CrownColor, 0.3f, 0.04f);
        grassMaterial = CreateFoliageMaterial(GrassColor, 0.08f, 0.03f);
        mudMaterial = CreateFoliageMaterial(MudColor, 0f, 0f);
        mudMaterial.SetFloat("_Smoothness", 0.45f);

        centerChunk = WorldToChunk(Camera.main.transform.position);
        RefreshChunks();

        // На старте лес должен быть готов сразу; потом трава догружается по одному чанку за кадр.
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

        Vector3 position = view.transform.position;
        Vector2Int center = WorldToChunk(position);
        if (center != centerChunk)
        {
            centerChunk = center;
            RefreshChunks();
        }

        GenerateNextGrass();
        RecenterGround(position);
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
                AcquireNearContent(chunk);
            else
                ReleaseNearContent(chunk);
        }
    }

    static int ChunkSeed(Vector2Int coord, int salt)
    {
        return unchecked(coord.x * 73856093 ^ coord.y * 19349663 ^ WorldSeed ^ salt);
    }

    Chunk CreateChunk(Vector2Int coord)
    {
        Vector3 center = new Vector3(coord.x * ChunkSize, 0f, coord.y * ChunkSize);
        var chunk = new Chunk
        {
            Coord = coord,
            Trees = new Matrix4x4[MaxTreesPerChunk],
            // Запас у деревьев: дерево за границей кадра всё ещё отбрасывает тень в кадр.
            TreeBounds = new Bounds(center + Vector3.up * TreeMaxHeight * 0.5f,
                new Vector3(ChunkSize + ShadowCullMargin * 2f, TreeMaxHeight + 2f, ChunkSize + ShadowCullMargin * 2f)),
            GrassBounds = new Bounds(center + Vector3.up * GrassMaxHeight * 0.5f,
                new Vector3(ChunkSize, GrassMaxHeight, ChunkSize))
        };
        GenerateTrees(chunk);
        GenerateMud(chunk);
        return chunk;
    }

    static void GenerateMud(Chunk chunk)
    {
        int segments = Mathf.CeilToInt(ChunkSize / MudSegmentLength);
        chunk.Mud = new Matrix4x4[segments];
        float minZ = (chunk.Coord.y - 0.5f) * ChunkSize;

        for (int i = 0; i < segments; i++)
        {
            float z = minZ + (i + 0.5f) * MudSegmentLength;
            float x = RoadCenterX(chunk.Coord.x, z);
            if (IsInStartClearing(x, z))
                continue;

            // Ширина по X совпадает с физической зоной грязи и на наклонных участках.
            float slope = (RoadCenterX(chunk.Coord.x, z + 0.5f) - RoadCenterX(chunk.Coord.x, z - 0.5f));
            float angle = Mathf.Atan(slope);
            float width = RoadHalfWidth * 2f * Mathf.Cos(angle);
            float length = MudSegmentLength / Mathf.Cos(angle) + MudSegmentOverlap;
            chunk.Mud[chunk.MudCount++] = Matrix4x4.TRS(new Vector3(x, MudHeight, z),
                Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f), new Vector3(width, 1f, length));
        }
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

                chunk.Trees[chunk.TreeCount++] = Matrix4x4.TRS(new Vector3(x, 0f, z),
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
                AddGrassBlade(chunk, Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(width, height, width)));
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

    void RecenterGround(Vector3 position)
    {
        if (Ground == null)
            return;

        // Двигать землю за игроком нельзя: оставленная машина и предметы провалились бы.
        Vector3 offset = position - Ground.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > GroundRecenterDistance * GroundRecenterDistance)
            Ground.position = new Vector3(position.x, Ground.position.y, position.z);
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
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.GrassBounds))
                continue;

            if (chunk.MudCount > 0)
                Graphics.DrawMeshInstanced(mudMesh, 0, mudMaterial, chunk.Mud, chunk.MudCount, null, ShadowCastingMode.Off, true);

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
