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
    
    // Система сохранения
    private SaveData<GameSettingsData> gameSettings;
    
    [Header("Generation Settings")]
    public bool useProgressiveGeneration = true;   // Постепенная генерация
    public int minFramesPerChunk = 1;              // Минимум кадров на чанк
    public int maxFramesPerChunk = 5;              // Максимум кадров на чанк
    private bool isGenerating = false;
    
    // Флаг готовности мира
    public bool IsWorldReady { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (generator == null) generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();
        
        // Инициализируем систему настроек игры
        gameSettings = new SaveData<GameSettingsData>("GameSettings");
    }

    private void Start()
    {
        StartCoroutine(InitializeWorld());
    }

    /// <summary>
    /// Инициализация мира - проверка первого запуска и загрузка/генерация
    /// </summary>
    System.Collections.IEnumerator InitializeWorld()
    {
        isGenerating = true;
        
        var settings = gameSettings.Load();
        
        // Проверяем первый запуск
        if (settings.isFirstRun)
        {
            Debug.Log("VoxelWorld: Первый запуск игры - генерируем новый мир");
            
            // Генерируем мир постепенно
            if (useProgressiveGeneration)
            {
                yield return StartCoroutine(GenerateWorldProgressive());
            }
            else
            {
                Generate();
            }
            
            // Помечаем что уже не первый запуск
            settings.isFirstRun = false;
            settings.worldChunksX = chunksX;
            settings.worldChunksZ = chunksZ;
            gameSettings.Data = settings;
            gameSettings.Save();
        }
        else
        {
            Debug.Log("VoxelWorld: Загружаем мир из сохранений");
            
            // Загружаем мир из чанков постепенно
            if (useProgressiveGeneration)
            {
                yield return StartCoroutine(LoadWorldProgressive());
            }
            else
            {
                LoadWorldImmediate();
            }
        }
        
        isGenerating = false;
        IsWorldReady = true;
        Debug.Log($"VoxelWorld: Инициализация завершена ({_chunks.Count} чанков), мир готов!");
    }
    
    /// <summary>
    /// Постепенная генерация мира (по 1 чанку за несколько кадров)
    /// </summary>
    System.Collections.IEnumerator GenerateWorldProgressive()
    {
        Debug.Log($"VoxelWorld: Начинаем постепенную генерацию ({chunksX}x{chunksZ} чанков)");
        
        // Очистка
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        
        if (generator == null)
            generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();
        
        // Генерируем чанки с задержкой
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                CreateChunk(cx, cz, null, null); // Генерируем новый
                
                // Случайная задержка от 1 до 5 кадров
                int framesToWait = Random.Range(minFramesPerChunk, maxFramesPerChunk + 1);
                for (int i = 0; i < framesToWait; i++)
                {
                    yield return null;
                }
            }
        }
        
        Debug.Log("VoxelWorld: Постепенная генерация завершена");
    }
    
    /// <summary>
    /// Постепенная загрузка мира из сохранений
    /// </summary>
    System.Collections.IEnumerator LoadWorldProgressive()
    {
        Debug.Log($"VoxelWorld: Начинаем постепенную загрузку ({chunksX}x{chunksZ} чанков)");
        
        // Очистка
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        
        int loadedCount = 0;
        int generatedCount = 0;
        
        // Загружаем чанки с задержкой
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                // Пытаемся загрузить чанк
                string chunkName = $"Chunk_{cx}_{cz}";
                var chunkSave = new SaveData<SingleChunkData>(chunkName);
                
                if (chunkSave.Exists())
                {
                    var chunkData = chunkSave.Load();
                    chunkData.UnpackData(out int[,,] data, out short[,,] hp);
                    CreateChunk(cx, cz, data, hp);
                    loadedCount++;
                }
                else
                {
                    // Если чанк не сохранен, генерируем новый
                    CreateChunk(cx, cz, null, null);
                    generatedCount++;
                }
                
                // Случайная задержка
                int framesToWait = Random.Range(minFramesPerChunk, maxFramesPerChunk + 1);
                for (int i = 0; i < framesToWait; i++)
                {
                    yield return null;
                }
            }
        }
        
        Debug.Log($"VoxelWorld: Загрузка завершена (загружено: {loadedCount}, создано: {generatedCount})");
    }
    
    /// <summary>
    /// Мгновенная загрузка (без задержек)
    /// </summary>
    void LoadWorldImmediate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                string chunkName = $"Chunk_{cx}_{cz}";
                var chunkSave = new SaveData<SingleChunkData>(chunkName);
                
                if (chunkSave.Exists())
                {
                    var chunkData = chunkSave.Load();
                    chunkData.UnpackData(out int[,,] data, out short[,,] hp);
                    CreateChunk(cx, cz, data, hp);
                }
                else
                {
                    CreateChunk(cx, cz, null, null);
                }
            }
        }
    }
    
    [ContextMenu("Generate world")]
    public void Generate()
    {
        // Мгновенная генерация (для отладки)
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        _chunks.Clear();

        if (generator == null) generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();

        for (int cz = 0; cz < chunksZ; cz++)
            for (int cx = 0; cx < chunksX; cx++)
            {
                CreateChunk(cx, cz, null, null);
            }
    }
    
    /// <summary>
    /// Создать или загрузить чанк
    /// </summary>
    void CreateChunk(int cx, int cz, int[,,] data, short[,,] hp)
    {
        // Генерируем данные если не переданы
        if (data == null)
        {
            data = generator.BuildChunkData(cx, cz);
            hp = AllocateHP(data);
        }
        
        // Создаем GameObject
        var go = new GameObject($"Chunk({cx},{cz})");
        go.transform.parent = transform;
        go.transform.position = new Vector3(cx * VoxelChunk16.WIDTH, 0, cz * VoxelChunk16.DEPTH);
        
        // Добавляем VoxelChunk16
        var builder = go.AddComponent<VoxelChunk16>();
        builder.atlasMaterial = atlasMaterial;
        builder.generateCollider = generateColliders;
        
        // Настройка HP
        builder.hpData = hp;
        builder.useDamageTiles = true;
        builder.typeMaxHpLut = new int[256];
        builder.typeMaxHpLut[0] = 5;
        builder.typeMaxHpLut[1] = 5;
        builder.typeMaxHpLut[2] = 8;
        builder.typeMaxHpLut[6] = 12;
        builder.typeMaxHpLut[7] = 12;
        
        // Настройка типов
        builder.typeToTileIndex = new int[256];
        builder.typeToTileIndex[0] = 0;
        builder.typeToTileIndex[1] = 1;
        builder.typeToTileIndex[2] = 2;
        builder.typeToTileIndex[6] = 6;
        builder.typeToTileIndex[7] = 7;
        for (int t = 0; t < builder.typeToTileIndex.Length; t++)
            if (t != 0 && t != 1 && t != 2 && t != 6 && t != 7)
                builder.typeToTileIndex[t] = 2;
        
        // Инициализируем автосохранение
        builder.Initialize(cx, cz, data);
        
        // Строим меш
        builder.Build(data);
        
        // Регистрируем
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
            
            // Помечаем чанк как измененный для автосохранения
            entry.builder.MarkDirty();
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
            entry.builder.MarkDirty();
        }
        else
        {
            entry.hp[lx, y, lz] = (short)newHP;
            entry.builder.MarkDirty();
        }
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

        if (rebuildChunk)
        {
            entry.builder.Build(entry.data);
            entry.builder.MarkDirty();
        }
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
    
    // === Публичный метод для проверки наличия блока ===
    public bool HasBlockAt(int wx, int wy, int wz)
    {
        if (wx < 0 || wz < 0 || wy < 0 || wy >= VoxelChunk16.HEIGHT) return false;
        if (wx >= chunksX * VoxelChunk16.WIDTH || wz >= chunksZ * VoxelChunk16.DEPTH) return false;

        int cxi = wx / VoxelChunk16.WIDTH;
        int czi = wz / VoxelChunk16.DEPTH;
        int lx = wx % VoxelChunk16.WIDTH;
        int lz = wz % VoxelChunk16.DEPTH;

        if (!_chunks.TryGetValue((cxi, czi), out var entry)) return false;
        
        int blockType = entry.data[lx, wy, lz];
        return blockType != AIR; // Возвращаем true если блок не воздух
    }
    
    // === Публичный метод для перестройки чанка ===
    public void RebuildChunk(int cx, int cz)
    {
        if (!_chunks.TryGetValue((cx, cz), out var entry)) return;
        entry.builder.Build(entry.data);
    }
    
    // ========== УТИЛИТЫ ДЛЯ СПАВНА ==========
    
    /// <summary>
    /// Получить безопасную позицию для спавна игрока
    /// </summary>
    public Vector3 GetSafeSpawnPosition()
    {
        if (!IsWorldReady || _chunks.Count == 0)
        {
            Debug.LogWarning("VoxelWorld: Мир не готов, возвращаем центр");
            return new Vector3(chunksX * VoxelChunk16.WIDTH / 2f, 100f, chunksZ * VoxelChunk16.DEPTH / 2f);
        }
        
        // Ищем безопасную позицию сверху вниз в центре мира
        int centerX = (chunksX * VoxelChunk16.WIDTH) / 2;
        int centerZ = (chunksZ * VoxelChunk16.DEPTH) / 2;
        
        // Проверяем сверху вниз
        for (int y = VoxelChunk16.HEIGHT - 1; y >= 10; y--)
        {
            // Проверяем, есть ли твердый блок
            if (HasBlockAt(centerX, y, centerZ))
            {
                // Проверяем, что над блоком свободно (минимум 3 блока для игрока)
                bool isClearAbove = true;
                for (int checkY = y + 1; checkY <= y + 3 && checkY < VoxelChunk16.HEIGHT; checkY++)
                {
                    if (HasBlockAt(centerX, checkY, centerZ))
                    {
                        isClearAbove = false;
                        break;
                    }
                }
                
                if (isClearAbove)
                {
                    // Нашли безопасную позицию - на 1 блок выше поверхности
                    return new Vector3(centerX + 0.5f, y + 1.5f, centerZ + 0.5f);
                }
            }
        }
        
        // Если не нашли - возвращаем высокую позицию в центре
        Debug.LogWarning("VoxelWorld: Не найдена безопасная позиция, используем высоту 80");
        return new Vector3(centerX + 0.5f, 80f, centerZ + 0.5f);
    }
    
    // ========== УТИЛИТЫ УПРАВЛЕНИЯ СОХРАНЕНИЯМИ ==========
    
    /// <summary>
    /// Принудительно сохранить все чанки
    /// </summary>
    [ContextMenu("Force Save All Chunks")]
    public void ForceSaveAllChunks()
    {
        int savedCount = 0;
        foreach (var entry in _chunks.Values)
        {
            if (entry.builder != null)
            {
                entry.builder.SaveChunk();
                savedCount++;
            }
        }
        Debug.Log($"VoxelWorld: Принудительно сохранено {savedCount} чанков");
    }
    
    /// <summary>
    /// Удалить все сохранения чанков и сбросить игру в первый запуск
    /// </summary>
    [ContextMenu("Delete All Saves (Reset to First Run)")]
    public void DeleteAllSaves()
    {
        // Удаляем все сохранения чанков
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                string chunkName = $"Chunk_{cx}_{cz}";
                SaveManager.DeleteSave(chunkName);
            }
        }
        
        // Сбрасываем настройки игры
        var settings = new GameSettingsData();
        settings.isFirstRun = true;
        gameSettings.Data = settings;
        gameSettings.Save();
        
        Debug.Log("VoxelWorld: Все сохранения удалены, игра сброшена в первый запуск");
    }
    
    /// <summary>
    /// Сбросить мир (удалить сохранения и перегенерировать)
    /// </summary>
    [ContextMenu("Reset World")]
    public void ResetWorld()
    {
        DeleteAllSaves();
        
        // Перезагружаем сцену или перегенерируем
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        
        StartCoroutine(InitializeWorld());
    }
}
