using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    [Header("Chunk grid")]
    public int chunksX = 5;
    public int chunksZ = 5;

    [Header("Render/Physics")]
    public Material atlasMaterial;
    public bool generateColliders = true;

    [Header("Data generator")]
    public VoxelWorldGenerator generator;

    const int AIR = -1;
    const int GRASS = 0;
    const int DIRT = 1;
    const int STONE = 2;
    const int COAL = 6;
    const int GOLD = 7;

    public class ChunkEntry
    {
        public int cx, cz;
        public int[,,] data;
        public short[,,] hp;
        public VoxelChunk16 builder;
        public GameObject go;
    }

    readonly Dictionary<(int cx, int cz), ChunkEntry> _chunks = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (generator == null) generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();
    }

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate world")]
    public void Generate()
    {
        // 0) очистить старые чанки
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        _chunks.Clear();

        // 1) убедимся, что есть генератор данных
        if (generator == null) generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();

        // 2) создать и заполнить чанки
        for (int cz = 0; cz < chunksZ; cz++)
            for (int cx = 0; cx < chunksX; cx++)
            {
                // данные чанка
                var data = generator.BuildChunkData(cx, cz);
                var hp = AllocateHP(data);

                // объект
                var go = new GameObject($"Chunk({cx},{cz})");
                go.transform.parent = transform;
                go.transform.position = new Vector3(cx * VoxelChunk16.WIDTH, 0, cz * VoxelChunk16.DEPTH);

                // билдер
                var builder = go.AddComponent<VoxelChunk16>();
                builder.atlasMaterial = atlasMaterial;
                builder.generateCollider = generateColliders;

                // ======= повреждения / HP =======
                builder.hpData = hp;
                builder.useDamageTiles = true;
                builder.typeMaxHpLut = new int[256];
                builder.typeMaxHpLut[0] = 5;   // трава/пыльный блок
                builder.typeMaxHpLut[1] = 5;   // земля
                builder.typeMaxHpLut[2] = 8;   // камень
                builder.typeMaxHpLut[6] = 12;  // уголь
                builder.typeMaxHpLut[7] = 12;  // золото

                // ======= КАРТА ТИП → ТАЙЛ =======
                // ВАЖНО: подставь индексы под свой атлас (10 на ряд, нумерация снизу-вверх).
                builder.typeToTileIndex = new int[256];
                builder.typeToTileIndex[0] = 0;  // пыльный блок (целый)
                builder.typeToTileIndex[1] = 1;  // земля
                builder.typeToTileIndex[2] = 2;  // камень (лунный)
                builder.typeToTileIndex[6] = 6;  // уголь
                builder.typeToTileIndex[7] = 7;  // золото
                                                 // дефолт: пусть рендерится камнем, чтобы не было «всё травой»
                for (int t = 0; t < builder.typeToTileIndex.Length; t++)
                    if (t != 0 && t != 1 && t != 2 && t != 6 && t != 7)
                        builder.typeToTileIndex[t] = 2;

                // построить меш
                builder.Build(data);

                // регистрация
                _chunks[(cx, cz)] = new ChunkEntry
                {
                    cx = cx,
                    cz = cz,
                    data = data,
                    hp = hp,
                    builder = builder,
                    go = go
                };
            }
    }

    short[,,] AllocateHP(int[,,] data)
    {
        int W = VoxelChunk16.WIDTH, H = VoxelChunk16.HEIGHT, D = VoxelChunk16.DEPTH;
        var hp = new short[W, H, D];
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                for (int z = 0; z < D; z++)
                {
                    int t = data[x, y, z];
                    hp[x, y, z] = (short)(t == AIR ? 0 : GetMaxHP(t));
                }
        return hp;
    }

    int GetMaxHP(int type)
    {
        return type switch
        {
            GRASS => 5,
            DIRT => 5,
            STONE => 8,
            COAL => 12,
            GOLD => 12,
            _ => 6
        };
    }

    // ====== урон сферой со спадом к краю ======
    // falloff: в центре 1.0, на границе 0.2
    public void DamageSphere(Vector3 worldPos, float radius, float maxDamage)
    {
        if (_chunks.Count == 0) return;

        float r2 = radius * radius;
        float invR = radius > 0f ? 1f / radius : 0f;

        int minX = Mathf.FloorToInt(worldPos.x - radius);
        int maxX = Mathf.CeilToInt(worldPos.x + radius);
        int minY = Mathf.Max(0, Mathf.FloorToInt(worldPos.y - radius));
        int maxY = Mathf.Min(VoxelChunk16.HEIGHT - 1, Mathf.CeilToInt(worldPos.y + radius));
        int minZ = Mathf.FloorToInt(worldPos.z - radius);
        int maxZ = Mathf.CeilToInt(worldPos.z + radius);

        var touched = new HashSet<(int cx, int cz)>();

        for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || z < 0 || x >= chunksX * VoxelChunk16.WIDTH || z >= chunksZ * VoxelChunk16.DEPTH) continue;

                    // расстояние по центрам вокселей
                    float dx = (x + 0.5f) - worldPos.x;
                    float dy = (y + 0.5f) - worldPos.y;
                    float dz = (z + 0.5f) - worldPos.z;
                    float d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 > r2) continue;

                    int cxi = x / VoxelChunk16.WIDTH;
                    int czi = z / VoxelChunk16.DEPTH;
                    int lx = x % VoxelChunk16.WIDTH;
                    int lz = z % VoxelChunk16.DEPTH;

                    if (!_chunks.TryGetValue((cxi, czi), out var entry)) continue;

                    int type = entry.data[lx, y, lz];
                    if (type == AIR) continue;

                    // радиальный спад урона: 1 → 0.2 на краю (можно SmoothStep для плавности)
                    float dist = Mathf.Sqrt(d2);
                    float t = Mathf.Clamp01(dist * invR);
                    float falloff = Mathf.Lerp(1f, 0.2f, t);               // линейный
                                                                           // float falloff = Mathf.Lerp(1f, 0.2f, t*t*(3-2*t));  // сглаженный

                    float dmg = maxDamage * falloff;

                    // применяем урон
                    short curHP = entry.hp[lx, y, lz];
                    if (curHP <= 0) continue;

                    int newHP = curHP - Mathf.CeilToInt(dmg);
                    if (newHP <= 0)
                    {
                        entry.hp[lx, y, lz] = 0;
                        entry.data[lx, y, lz] = AIR;
                        touched.Add((cxi, czi));
                    }
                    else
                    {
                        entry.hp[lx, y, lz] = (short)newHP;
                        // блок не умер — чанк перестраивать не нужно
                    }
                }

        foreach (var key in touched)
        {
            var entry = _chunks[key];
            entry.builder.Build(entry.data);
        }
    }

    // (по желанию) прямой удар по одному блоку:
    public void DamageBlock(Vector3Int worldBlock, int damage)
    {
        int x = worldBlock.x, y = worldBlock.y, z = worldBlock.z;
        if (x < 0 || z < 0 || y < 0 || y >= VoxelChunk16.HEIGHT) return;
        if (x >= chunksX * VoxelChunk16.WIDTH || z >= chunksZ * VoxelChunk16.DEPTH) return;

        int cxi = x / VoxelChunk16.WIDTH;
        int czi = z / VoxelChunk16.DEPTH;
        int lx = x % VoxelChunk16.WIDTH;
        int lz = z % VoxelChunk16.DEPTH;

        if (!_chunks.TryGetValue((cxi, czi), out var entry)) return;
        if (entry.data[lx, y, lz] == AIR) return;

        int newHP = entry.hp[lx, y, lz] - damage;
        if (newHP <= 0)
        {
            entry.hp[lx, y, lz] = 0;
            entry.data[lx, y, lz] = AIR;
            entry.builder.Build(entry.data);
        }
        else entry.hp[lx, y, lz] = (short)newHP;
    }

    // === ПУБЛИЧНО: поставить блок в мире (по мировым индексам) ===
    public bool SetBlock(int wx, int wy, int wz, int blockType, bool rebuildChunk = true)
    {
        if (wx < 0 || wz < 0 || wy < 0 || wy >= VoxelChunk16.HEIGHT) return false;
        if (wx >= chunksX * VoxelChunk16.WIDTH || wz >= chunksZ * VoxelChunk16.DEPTH) return false;

        int cxi = wx / VoxelChunk16.WIDTH;
        int czi = wz / VoxelChunk16.DEPTH;
        int lx = wx % VoxelChunk16.WIDTH;
        int lz = wz % VoxelChunk16.DEPTH;

        if (!_chunks.TryGetValue((cxi, czi), out var entry)) return false;

        // ставим только в пустоту
        if (entry.data[lx, wy, lz] != -1) return false;

        entry.data[lx, wy, lz] = blockType;
        // hp: максимум для типа
        int maxHp = blockType switch { 0 => 5, 1 => 5, 2 => 8, 6 => 12, 7 => 12, _ => 6 };
        entry.hp[lx, wy, lz] = (short)maxHp;

        if (rebuildChunk) entry.builder.Build(entry.data);
        return true;
    }

    // === Утилита: поставить блок рядом с ударенной поверхностью ===
    public bool PlaceAdjacent(RaycastHit hit, int blockType, float epsilon = 0.001f)
    {
        // 1) немного смещаем точку внутрь ударенного блока
        Vector3 pInside = hit.point - hit.normal * epsilon;

        // 2) индекс ударенного блока
        int bx = Mathf.FloorToInt(pInside.x);
        int by = Mathf.FloorToInt(pInside.y);
        int bz = Mathf.FloorToInt(pInside.z);

        // 3) снэп нормали к оси (исключаем косые значения и шум float)
        Vector3 n = hit.normal;
        if (Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z))
            n = new Vector3(Mathf.Sign(n.x), 0, 0);
        else if (Mathf.Abs(n.y) >= Mathf.Abs(n.x) && Mathf.Abs(n.y) >= Mathf.Abs(n.z))
            n = new Vector3(0, Mathf.Sign(n.y), 0);
        else
            n = new Vector3(0, 0, Mathf.Sign(n.z));

        int nx = (int)n.x, ny = (int)n.y, nz = (int)n.z;

        // 4) целевой (соседний) воксель со стороны нормали
        int tx = bx + nx;
        int ty = by + ny;
        int tz = bz + nz;

        return SetBlock(tx, ty, tz, blockType, true);
    }
}
