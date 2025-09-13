using UnityEngine;

public class VoxelWorldGenerator : MonoBehaviour
{
    [Header("Chunk grid")]
    public int chunksX = 5;
    public int chunksZ = 5;

    [Header("Noise (height)")]
    public int seed = 12345;
    [Range(1f, 512f)] public float heightScale = 96f;
    [Range(1, 8)] public int octaves = 4;
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Range(1.5f, 4f)] public float lacunarity = 2.0f;
    public float heightMultiplier = 40f;
    public float baseHeight = 64f;

    [Header("Noise (layers thickness)")]
    public float grassThicknessScale = 24f;
    public float dirtThicknessScale = 36f;

    [Header("Visuals/Physics")]
    public Material atlasMaterial;
    public bool generateColliders = true;

    // === CAVES ===
    [Header("Caves")]
    public bool enableCaves = true;
    [Tooltip("Чем меньше, тем шире ‘извивы’ пещер.")]
    public float caveScale = 40f;                // масштаб пещерного шума (в мире)
    [Range(1, 6)] public int caveOctaves = 3;    // октавы fBm для 3D шума
    [Range(0f, 1f)] public float cavePersistence = 0.55f;
    [Range(1.5f, 4f)] public float caveLacunarity = 2.15f;
    [Tooltip("Порог вырезания внизу (тонкие пещеры). Выше порога — воздух.")]
    [Range(0.45f, 0.9f)] public float caveThresholdBottom = 0.72f;
    [Tooltip("Порог вырезания на середине высоты (толще пещеры).")]
    [Range(0.3f, 0.9f)] public float caveThresholdMid = 0.58f;
    [Tooltip("Порог вырезания у верха.")]
    [Range(0.45f, 0.9f)] public float caveThresholdTop = 0.62f;
    [Tooltip("Смещает ширину пещер вдоль высоты (0..1). 0.5 = максимум толщины по центру.")]
    [Range(0f, 1f)] public float caveMidHeight01 = 0.5f;
    [Tooltip("Насколько пещеры ‘ветвятся’ (добавляет завитушки).")]
    [Range(0f, 1f)] public float caveTwist = 0.35f;

    // типы блоков
    const int Block_Grass = 0;
    const int Block_Dirt = 1;
    const int Block_Stone = 2;
    const int Block_Air = -1;
    const int Block_Coal = 6;
    const int Block_Gold = 7;

    // смещения шума
    Vector2 heightOffset;
    Vector2 grassOffset;
    Vector2 dirtOffset;

    // === CAVES (offsets) ===
    Vector3 caveOffsetA;
    Vector3 caveOffsetB;

    void Awake()
    {
        var rng = new System.Random(seed);
        heightOffset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));
        grassOffset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));
        dirtOffset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));

        // caves offsets — чтобы пещеры были детерминированные и не повторяли рельеф
        caveOffsetA = new Vector3(
            rng.Next(-100000, 100000),
            rng.Next(-100000, 100000),
            rng.Next(-100000, 100000)
        );
        caveOffsetB = new Vector3(
            rng.Next(-100000, 100000),
            rng.Next(-100000, 100000),
            rng.Next(-100000, 100000)
        );
    }

    [ContextMenu("Generate world")]
    public void Generate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        for (int cz = 0; cz < chunksZ; cz++)
            for (int cx = 0; cx < chunksX; cx++)
                GenerateChunk(cx, cz);
    }

    void GenerateChunk(int cx, int cz)
    {
        var go = new GameObject($"Chunk({cx},{cz})");
        go.transform.parent = transform;
        go.transform.position = new Vector3(cx * VoxelChunk16.WIDTH, 0, cz * VoxelChunk16.DEPTH);

        var chunk = go.AddComponent<VoxelChunk16>();
        chunk.atlasMaterial = atlasMaterial;
        chunk.generateCollider = generateColliders;

        var data = new int[VoxelChunk16.WIDTH, VoxelChunk16.HEIGHT, VoxelChunk16.DEPTH];

        // 1) базовая порода + слои
        for (int z = 0; z < VoxelChunk16.DEPTH; z++)
            for (int x = 0; x < VoxelChunk16.WIDTH; x++)
            {
                int worldX = cx * VoxelChunk16.WIDTH + x;
                int worldZ = cz * VoxelChunk16.DEPTH + z;

                float h01 = FBm(worldX, worldZ, heightScale, octaves, persistence, lacunarity, heightOffset);
                int surfaceY = Mathf.Clamp(Mathf.FloorToInt(baseHeight + h01 * heightMultiplier), 0, VoxelChunk16.HEIGHT - 1);

                float g01 = Perlin01(worldX, worldZ, grassThicknessScale, grassOffset);
                int grassThickness = Mathf.Clamp(Mathf.FloorToInt(g01 * 4f), 0, 3);

                float d01 = Perlin01(worldX, worldZ, dirtThicknessScale, dirtOffset);
                int dirtThickness = Mathf.Clamp(Mathf.FloorToInt(d01 * 8f), 0, 7);

                int grassStartY = Mathf.Max(0, surfaceY - grassThickness + 1);
                int dirtStartY = Mathf.Max(0, grassStartY - dirtThickness);

                for (int y = 0; y < VoxelChunk16.HEIGHT; y++)
                {
                    if (y > surfaceY) data[x, y, z] = Block_Air;
                    else if (y >= grassStartY && y <= surfaceY
                             && grassThickness > 0) data[x, y, z] = Block_Grass;
                    else if (y >= dirtStartY && y < grassStartY
                             && dirtThickness > 0) data[x, y, z] = Block_Dirt;
                    else data[x, y, z] = Block_Stone;
                }

                // гарантируем «неприкосновенный» самый нижний слой
                data[x, 0, z] = Block_Stone;
            }

        // 2) руда (жилы)
        GenerateOresInChunk(data, cx, cz);

        // 3) пещеры (вырезаем воздухом)
        if (enableCaves)
            CarveCavesInChunk(data, cx, cz);

        // 4) меш
        chunk.Build(data);
    }

    // ======= Ores =======

    void GenerateOresInChunk(int[,,] data, int cx, int cz)
    {
        int chSeed = seed ^ (cx * 73856093) ^ (cz * 19349663);
        var rng = new System.Random(chSeed);

        Vector3Int sizeCoal = new Vector3Int(5, 4, 3);
        Vector3Int sizeGold = new Vector3Int(5, 4, 3);

        int H = VoxelChunk16.HEIGHT;
        int coalYMin = Mathf.RoundToInt(H * 0.10f);
        int coalYMax = Mathf.RoundToInt(H * 0.70f);
        int goldYMin = 0;
        int goldYMax = Mathf.RoundToInt(H * 0.30f);

        int coalVeins = rng.Next(2, 5);             // 2..4
        int goldVeins = (rng.NextDouble() < 0.6) ? 1 : 0;

        float coalDensity = 0.45f;
        float goldDensity = 0.30f;

        for (int i = 0; i < coalVeins; i++)
            PlaceVein(data, rng, Block_Coal, sizeCoal, coalYMin, coalYMax, coalDensity);

        for (int i = 0; i < goldVeins; i++)
            PlaceVein(data, rng, Block_Gold, sizeGold, goldYMin, goldYMax, goldDensity);
    }

    void PlaceVein(int[,,] data, System.Random rng, int oreBlock,
                   Vector3Int size, int yMin, int yMax, float density)
    {
        int maxX = VoxelChunk16.WIDTH - size.x;
        int maxY = Mathf.Clamp(VoxelChunk16.HEIGHT - size.y, 0, VoxelChunk16.HEIGHT - 1);
        int maxZ = VoxelChunk16.DEPTH - size.z;
        if (maxX < 0 || maxY < 0 || maxZ < 0) return;

        int ox = rng.Next(0, maxX + 1);
        int oy = rng.Next(Mathf.Max(1, yMin), Mathf.Min(maxY, Mathf.Max(yMin, yMax - size.y + 1))); // не трогаем y=0
        int oz = rng.Next(0, maxZ + 1);

        float rx = (size.x - 1) * 0.5f;
        float ry = (size.y - 1) * 0.5f;
        float rz = (size.z - 1) * 0.5f;
        float cx = ox + rx;
        float cy = oy + ry;
        float cz = oz + rz;

        for (int z = oz; z < oz + size.z; z++)
            for (int y = oy; y < oy + size.y; y++)
                for (int x = ox; x < ox + size.x; x++)
                {
                    float dx = (x - cx) / Mathf.Max(0.001f, rx);
                    float dy = (y - cy) / Mathf.Max(0.001f, ry);
                    float dz = (z - cz) / Mathf.Max(0.001f, rz);
                    float e = dx * dx + dy * dy + dz * dz;

                    if (e <= 1f && Random.value < density)
                    {
                        if (data[x, y, z] == Block_Stone)
                            data[x, y, z] = oreBlock;
                    }
                }
    }

    // ======= Caves =======

    void CarveCavesInChunk(int[,,] data, int cx, int cz)
    {
        int H = VoxelChunk16.HEIGHT;

        for (int z = 0; z < VoxelChunk16.DEPTH; z++)
            for (int y = 1; y < H; y++) // y=0 оставляем нетронутым
                for (int x = 0; x < VoxelChunk16.WIDTH; x++)
                {
                    if (data[x, y, z] == Block_Air) continue;

                    int wx = cx * VoxelChunk16.WIDTH + x;
                    int wy = y;
                    int wz = cz * VoxelChunk16.DEPTH + z;

                    // 3D fBm шум (0..1)
                    float n = FBm3D01(wx, wy, wz, caveScale, caveOctaves, cavePersistence, caveLacunarity, caveOffsetA, caveOffsetB);

                    // чуть «закручиваем» тоннели
                    if (caveTwist > 0f)
                    {
                        float twist = Mathf.PerlinNoise((wx + caveOffsetB.x) / caveScale, (wz + caveOffsetB.z) / caveScale);
                        n = Mathf.Lerp(n, (n * 0.7f + twist * 0.3f), caveTwist);
                    }

                    // порог меняется по высоте: толще у середины, тоньше внизу
                    float t = (float)wy / (H - 1); // 0..1
                    float threshold = CaveThresholdByHeight(t);

                    // вырезаем воздухом
                    if (n > threshold)
                        data[x, y, z] = Block_Air;
                }
    }

    float CaveThresholdByHeight(float t01)
    {
        // делаем колокол вокруг caveMidHeight01 (минимальный порог => толще пещеры)
        float d = Mathf.Abs(t01 - caveMidHeight01) / Mathf.Max(0.001f, caveMidHeight01); // 0 в центре, ~2 у краёв
        d = Mathf.Clamp01(d * 0.75f); // сжимаем
        // интерполируем порог между серединой и краями (ниж/верх разные)
        float edge = t01 < caveMidHeight01
            ? Mathf.Lerp(caveThresholdBottom, caveThresholdMid, 1f - d)
            : Mathf.Lerp(caveThresholdTop, caveThresholdMid, 1f - d);

        // сгладим
        return Mathf.SmoothStep(edge, edge, 0.5f);
    }

    // ======= helpers (height noise) =======

    float FBm(int x, int z, float scale, int octs, float pers, float lac, Vector2 offset)
    {
        float amp = 1f, freq = 1f, sum = 0f, norm = 0f;
        for (int i = 0; i < octs; i++)
        {
            float nx = (x + offset.x) / Mathf.Max(1f, scale) * freq;
            float nz = (z + offset.y) / Mathf.Max(1f, scale) * freq;
            float v = Mathf.PerlinNoise(nx, nz);
            sum += v * amp;
            norm += amp;
            amp *= pers;
            freq *= lac;
        }
        if (norm <= 0f) return 0f;
        return Mathf.InverseLerp(-1f, 1f, (sum / norm) * 2f - 1f);
    }

    float Perlin01(int x, int z, float scale, Vector2 offset)
    {
        float nx = (x + offset.x) / Mathf.Max(1f, scale);
        float nz = (z + offset.y) / Mathf.Max(1f, scale);
        return Mathf.PerlinNoise(nx, nz);
    }

    // === 3D fBm из 2D PerlinNoise (быстро и достаточно для пещер) ===
    float FBm3D01(int x, int y, int z, float scale, int octs, float pers, float lac, Vector3 offA, Vector3 offB)
    {
        float amp = 1f, freq = 1f, sum = 0f, norm = 0f;

        for (int i = 0; i < octs; i++)
        {
            // комбинируем несколько 2D проекций, чтобы получить 3D-подобный шум
            float nx1 = (x + offA.x) / Mathf.Max(1f, scale) * freq;
            float ny1 = (y + offA.y) / Mathf.Max(1f, scale) * freq;
            float nz1 = (z + offA.z) / Mathf.Max(1f, scale) * freq;

            float nx2 = (x + offB.x) / Mathf.Max(1f, scale * 0.85f) * freq;
            float ny2 = (y + offB.y) / Mathf.Max(1f, scale * 0.85f) * freq;
            float nz2 = (z + offB.z) / Mathf.Max(1f, scale * 0.85f) * freq;

            float p1 = Mathf.PerlinNoise(nx1, ny1);
            float p2 = Mathf.PerlinNoise(ny1, nz1);
            float p3 = Mathf.PerlinNoise(nz2, nx2);

            float v = (p1 + p2 + p3) / 3f; // 0..1

            sum += v * amp;
            norm += amp;

            amp *= pers;
            freq *= lac;
        }

        if (norm <= 0f) return 0f;
        return sum / norm; // 0..1
    }
}
