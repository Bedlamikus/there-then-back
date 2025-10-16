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
    const int WOOD = 3;      // Дерево (ствол и ветки)
    const int LEAVES = 4;    // Листва
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
    
    // Флаги состояния мира
    public bool IsGenerating { get; private set; } = false;
    public bool IsWorldReady { get; private set; } = false;
    
    // Точка спавна игрока
    private Vector3 playerSpawnPoint = Vector3.zero;
    public Vector3 PlayerSpawnPoint => playerSpawnPoint;
    
    // Ссылка на игрока для кулинга чанков
    private Transform cachedPlayerTransform;
    private PlayerController cachedPlayerController; // Кеш для избежания GetComponent каждый кадр
    public Transform PlayerTransform => cachedPlayerTransform;
    
    // Централизованная система кулинга
    private List<VoxelChunk16> chunkList = new List<VoxelChunk16>(); // Список для итерации
    private int currentCullingIndex = 0;
    private int chunksToCheckPerFrame = 10; // Проверяем 10 чанков за кадр (быстрее реакция кулинга)
    private Camera mainCamera; // Кеш главной камеры
    
    // Статистика для отладки
    #if UNITY_EDITOR
    private float nextStatsLogTime = 0f;
    #endif

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
    
    void Update()
    {
        // Кешируем камеру если нужно
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        // Централизованная проверка кулинга чанков
        if (cachedPlayerController != null && chunkList.Count > 0 && mainCamera != null)
        {
            Vector3 playerPos = cachedPlayerTransform.position;
            float viewDistanceSqr = cachedPlayerController.viewDistance * cachedPlayerController.viewDistance * 2.25f; // 1.5^2 = 2.25 (буфер)
            
            // Вычисляем frustum planes ОДИН РАЗ за кадр
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            
            // Проверяем N чанков за кадр
            for (int i = 0; i < chunksToCheckPerFrame && chunkList.Count > 0; i++)
            {
                if (currentCullingIndex >= chunkList.Count)
                    currentCullingIndex = 0;
                
                VoxelChunk16 chunk = chunkList[currentCullingIndex];
                if (chunk != null)
                {
                    chunk.CheckCullingCentralized(playerPos, viewDistanceSqr, frustumPlanes);
                }
                
                currentCullingIndex++;
            }
            
            // Debug статистика раз в 3 секунды
            #if UNITY_EDITOR
            if (Time.time >= nextStatsLogTime)
            {
                int activeChunks = 0;
                foreach (var chunk in chunkList)
                {
                    if (chunk != null && chunk.GetComponent<MeshRenderer>().enabled)
                        activeChunks++;
                }
                Debug.Log($"VoxelWorld Stats: {activeChunks}/{chunkList.Count} чанков видимы");
                nextStatsLogTime = Time.time + 3f;
            }
            #endif
        }
    }

    /// <summary>
    /// Инициализация мира - проверка первого запуска и загрузка/генерация
    /// </summary>
    System.Collections.IEnumerator InitializeWorld()
    {
        IsGenerating = true;
        
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
            
            // Сохраняем сгенерированные чанки
            Debug.Log("VoxelWorld: Сохранение сгенерированных чанков...");
            ForceSaveAllChunks();
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
        
        IsGenerating = false;
        IsWorldReady = true;
        
        // Вычисляем точку спавна игрока
        CalculatePlayerSpawnPoint();
        
        Debug.Log($"VoxelWorld: Инициализация завершена ({_chunks.Count} чанков), мир готов! Точка спавна: {playerSpawnPoint}");
        
        // Уведомляем всех о готовности мира
        GlobalEvents.WorldReady.Invoke();
    }
    
    /// <summary>
    /// Постепенная генерация мира (оптимизированная: сначала данные, потом визуализация)
    /// </summary>
    System.Collections.IEnumerator GenerateWorldProgressive()
    {
        Debug.Log($"VoxelWorld: Начинаем генерацию ({chunksX}x{chunksZ} чанков)");
        
        // Очистка
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        chunkList.Clear();
        
        if (generator == null)
            generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();
        
        int totalChunks = chunksX * chunksZ;
        int processedChunks = 0;
        
        // ЭТАП 1: Генерируем ДАННЫЕ всех чанков (без визуализации)
        Debug.Log("VoxelWorld: Этап 1 - Генерация данных чанков (без мешей)");
        GlobalEvents.WorldGenerationProgress.Invoke(0f);
        
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                CreateChunkDataOnly(cx, cz); // Только данные
                processedChunks++;
                
                // Обновляем прогресс (0.0 - 0.5 для генерации данных)
                float progress = (float)processedChunks / totalChunks * 0.5f;
                GlobalEvents.WorldGenerationProgress.Invoke(progress);
                
                // Периодически отдаем управление
                if (processedChunks % 5 == 0)
                {
                    yield return null;
                }
            }
        }
        
        Debug.Log("VoxelWorld: Данные всех чанков сгенерированы");
        GlobalEvents.WorldGenerationProgress.Invoke(0.5f);
        
        // ЭТАП 2: Терраформирование (каньоны)
        Debug.Log("VoxelWorld: Этап 2 - Терраформирование (каньоны)");
        if (generator != null)
        {
            generator.GenerateCanyonsInWorld(this);
        }
        GlobalEvents.WorldGenerationProgress.Invoke(0.6f);
        yield return null;
        
        // ЭТАП 3: Растительность (деревья)
        Debug.Log("VoxelWorld: Этап 3 - Генерация деревьев");
        if (generator != null)
        {
            generator.GenerateTreesInWorld(this);
        }
        GlobalEvents.WorldGenerationProgress.Invoke(0.7f);
        yield return null;
        
        // ЭТАП 4: Вычисляем точку спавна
        Debug.Log("VoxelWorld: Этап 4 - Расчет точки спавна");
        CalculatePlayerSpawnPoint();
        GlobalEvents.WorldGenerationProgress.Invoke(0.75f);
        
        // ЭТАП 5: Строим меши для чанков вокруг точки спавна (3x3)
        Debug.Log("VoxelWorld: Этап 5 - Визуализация стартовой зоны");
        int spawnChunkX = Mathf.FloorToInt(playerSpawnPoint.x / VoxelChunk16.WIDTH);
        int spawnChunkZ = Mathf.FloorToInt(playerSpawnPoint.z / VoxelChunk16.DEPTH);
        
        BuildMeshesAroundSpawn(spawnChunkX, spawnChunkZ, 1); // Радиус 1 = 3x3 чанка
        GlobalEvents.WorldGenerationProgress.Invoke(0.8f);
        yield return null;
        
        Debug.Log($"VoxelWorld: Стартовая зона визуализирована, точка спавна: {playerSpawnPoint}");
        
        // ЭТАП 6: Мир готов для игры! (игрок может спавниться)
        IsGenerating = false;
        IsWorldReady = true;
        
        Debug.Log("VoxelWorld: Мир готов! Игрок может появиться. Фоновая визуализация продолжается...");
        GlobalEvents.WorldReady.Invoke();
        
        // Запускаем фоновую визуализацию остальных чанков
        StartCoroutine(BuildRemainingChunksInBackground(spawnChunkX, spawnChunkZ));
    }
    
    /// <summary>
    /// Создает только данные чанка без построения меша
    /// </summary>
    void CreateChunkDataOnly(int cx, int cz)
    {
        // Генерируем данные
        int[,,] data = generator.BuildChunkData(cx, cz);
        short[,,] hp = AllocateHP(data);
        
        // Создаем GameObject
        var go = new GameObject($"Chunk({cx},{cz})");
        go.transform.parent = transform;
        go.transform.position = new Vector3(cx * VoxelChunk16.WIDTH, 0, cz * VoxelChunk16.DEPTH);
        
        // Добавляем VoxelChunk16 но НЕ строим меш
        var builder = go.AddComponent<VoxelChunk16>();
        builder.atlasMaterial = atlasMaterial;
        builder.generateCollider = generateColliders;
        
        // Настройка HP
        builder.hpData = hp;
        builder.useDamageTiles = true;
        builder.typeMaxHpLut = new int[256];
        builder.typeMaxHpLut[0] = 5;   // Трава
        builder.typeMaxHpLut[1] = 5;   // Земля
        builder.typeMaxHpLut[2] = 8;   // Камень
        builder.typeMaxHpLut[3] = 6;   // Дерево
        builder.typeMaxHpLut[4] = 2;   // Листва
        builder.typeMaxHpLut[6] = 12;  // Уголь
        builder.typeMaxHpLut[7] = 12;  // Золото
        
        // Настройка типов
        builder.typeToTileIndex = new int[256];
        builder.typeToTileIndex[0] = 0;  // Трава
        builder.typeToTileIndex[1] = 1;  // Земля
        builder.typeToTileIndex[2] = 2;  // Камень
        builder.typeToTileIndex[3] = 3;  // Дерево
        builder.typeToTileIndex[4] = 4;  // Листва
        builder.typeToTileIndex[6] = 6;  // Уголь
        builder.typeToTileIndex[7] = 7;  // Золото
        for (int t = 0; t < builder.typeToTileIndex.Length; t++)
            if (t != 0 && t != 1 && t != 2 && t != 3 && t != 4 && t != 6 && t != 7)
                builder.typeToTileIndex[t] = 2;
        
        // Инициализируем автосохранение
        builder.Initialize(cx, cz, data);
        
        // НЕ строим меш! Это будет сделано позже
        // builder.Build(data); ← НЕ вызываем
        
        // Регистрируем чанк
        _chunks[(cx, cz)] = new ChunkEntry
        {
            cx = cx,
            cz = cz,
            data = data,
            hp = hp,
            builder = builder,
            go = go
        };
        
        // Добавляем в список для централизованного кулинга
        if (!chunkList.Contains(builder))
        {
            chunkList.Add(builder);
        }
    }
    
    /// <summary>
    /// Строит меши для чанков вокруг точки спавна
    /// </summary>
    void BuildMeshesAroundSpawn(int centerCx, int centerCz, int radius)
    {
        int built = 0;
        
        for (int cz = centerCz - radius; cz <= centerCz + radius; cz++)
        {
            for (int cx = centerCx - radius; cx <= centerCx + radius; cx++)
            {
                if (cx < 0 || cx >= chunksX || cz < 0 || cz >= chunksZ)
                    continue;
                
                if (_chunks.TryGetValue((cx, cz), out var entry))
                {
                    if (entry.builder != null && entry.data != null)
                    {
                        entry.builder.Build(entry.data);
                        built++;
                    }
                }
            }
        }
        
        Debug.Log($"VoxelWorld: Построено {built} мешей в стартовой зоне");
    }
    
    /// <summary>
    /// Строит меши оставшихся чанков по спирали в фоновом режиме
    /// </summary>
    System.Collections.IEnumerator BuildRemainingChunksInBackground(int centerCx, int centerCz)
    {
        Debug.Log("VoxelWorld: Начинаем фоновую визуализацию остальных чанков");
        
        HashSet<(int, int)> alreadyBuilt = new HashSet<(int, int)>();
        
        // Помечаем чанки вокруг спавна как уже построенные
        for (int cz = centerCz - 1; cz <= centerCz + 1; cz++)
        {
            for (int cx = centerCx - 1; cx <= centerCx + 1; cx++)
            {
                if (cx >= 0 && cx < chunksX && cz >= 0 && cz < chunksZ)
                {
                    alreadyBuilt.Add((cx, cz));
                }
            }
        }
        
        int totalToBuild = chunksX * chunksZ - alreadyBuilt.Count;
        int built = 0;
        
        // Строим по спирали от центра
        int maxRadius = Mathf.Max(chunksX, chunksZ);
        
        for (int radius = 2; radius <= maxRadius; radius++)
        {
            for (int cz = centerCz - radius; cz <= centerCz + radius; cz++)
            {
                for (int cx = centerCx - radius; cx <= centerCx + radius; cx++)
                {
                    // Проверяем что это граница текущего радиуса
                    if (Mathf.Abs(cx - centerCx) != radius && Mathf.Abs(cz - centerCz) != radius)
                        continue;
                    
                    if (cx < 0 || cx >= chunksX || cz < 0 || cz >= chunksZ)
                        continue;
                    
                    if (alreadyBuilt.Contains((cx, cz)))
                        continue;
                    
                    if (_chunks.TryGetValue((cx, cz), out var entry))
                    {
                        if (entry.builder != null && entry.data != null)
                        {
                            entry.builder.Build(entry.data);
                            built++;
                            alreadyBuilt.Add((cx, cz));
                            
                            // Обновляем прогресс (0.8 - 1.0 для фоновой визуализации)
                            float bgProgress = 0.8f + (float)built / totalToBuild * 0.2f;
                            GlobalEvents.WorldGenerationProgress.Invoke(bgProgress);
                            
                            // Отдаем управление ПОСЛЕ КАЖДОГО чанка для плавности
                            yield return null;
                        }
                    }
                }
            }
        }
        
        GlobalEvents.WorldGenerationProgress.Invoke(1f);
        Debug.Log($"VoxelWorld: Фоновая визуализация завершена ({built} чанков)");
    }
    
    /// <summary>
    /// Прогрессивная загрузка мира (сначала данные, потом стартовая зона, затем остальное в фоне)
    /// </summary>
    System.Collections.IEnumerator LoadWorldProgressive()
    {
        Debug.Log($"VoxelWorld: Начинаем прогрессивную загрузку ({chunksX}x{chunksZ} чанков)");
        
        // Очистка
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        chunkList.Clear();
        
        int totalChunks = chunksX * chunksZ;
        int processedChunks = 0;
        int loadedCount = 0;
        int generatedCount = 0;
        
        // ЭТАП 1: Загружаем ДАННЫЕ всех чанков (без визуализации)
        Debug.Log("VoxelWorld: Этап 1 - Загрузка данных чанков (без мешей)");
        GlobalEvents.WorldGenerationProgress.Invoke(0f);
        
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                string chunkName = $"Chunk_{cx}_{cz}";
                var chunkSave = new SaveData<SingleChunkData>(chunkName);
                
                if (chunkSave.Exists())
                {
                    // Загружаем из сохранения
                    var chunkData = chunkSave.Load();
                    chunkData.UnpackData(out int[,,] data, out short[,,] hp);
                    
                    // Создаем GameObject и данные, но НЕ строим меш
                    var go = new GameObject($"Chunk({cx},{cz})");
                    go.transform.parent = transform;
                    go.transform.position = new Vector3(cx * VoxelChunk16.WIDTH, 0, cz * VoxelChunk16.DEPTH);
                    
                    var builder = go.AddComponent<VoxelChunk16>();
                    builder.atlasMaterial = atlasMaterial;
                    builder.generateCollider = generateColliders;
                    builder.hpData = hp;
                    
                    for (int t = 0; t < 5; t++)
                        builder.typeToTileIndex[t] = t == 0 ? 0 : (t == 3 ? 1 : (t == 4 ? 3 : 2));
                    
                    builder.Initialize(cx, cz, data);
                    
                    _chunks[(cx, cz)] = new ChunkEntry { cx = cx, cz = cz, data = data, hp = hp, builder = builder, go = go };
                    loadedCount++;
                }
                else
                {
                    // Генерируем недостающий чанк
                    CreateChunkDataOnly(cx, cz);
                    generatedCount++;
                }
                
                processedChunks++;
                
                // Обновляем прогресс (0.0 - 0.7 для загрузки данных)
                float progress = (float)processedChunks / totalChunks * 0.7f;
                GlobalEvents.WorldGenerationProgress.Invoke(progress);
                
                // Периодически отдаем управление
                if (processedChunks % 5 == 0)
                {
                    yield return null;
                }
            }
        }
        
        Debug.Log($"VoxelWorld: Данные загружены (загружено: {loadedCount}, создано: {generatedCount})");
        GlobalEvents.WorldGenerationProgress.Invoke(0.7f);
        
        // ЭТАП 2: Вычисляем точку спавна
        Debug.Log("VoxelWorld: Этап 2 - Расчет точки спавна");
        CalculatePlayerSpawnPoint();
        GlobalEvents.WorldGenerationProgress.Invoke(0.75f);
        
        // ЭТАП 3: Строим меши для чанков вокруг точки спавна (3x3)
        Debug.Log("VoxelWorld: Этап 3 - Визуализация стартовой зоны");
        int spawnChunkX = Mathf.FloorToInt(playerSpawnPoint.x / VoxelChunk16.WIDTH);
        int spawnChunkZ = Mathf.FloorToInt(playerSpawnPoint.z / VoxelChunk16.DEPTH);
        
        BuildMeshesAroundSpawn(spawnChunkX, spawnChunkZ, 1); // Радиус 1 = 3x3 чанка
        GlobalEvents.WorldGenerationProgress.Invoke(0.8f);
        yield return null;
        
        Debug.Log($"VoxelWorld: Стартовая зона визуализирована, точка спавна: {playerSpawnPoint}");
        
        // ЭТАП 4: Мир готов для игры!
        IsGenerating = false;
        IsWorldReady = true;
        
        Debug.Log("VoxelWorld: Мир готов! Игрок может появиться. Фоновая визуализация продолжается...");
        GlobalEvents.WorldReady.Invoke();
        
        // Запускаем фоновую визуализацию остальных чанков
        StartCoroutine(BuildRemainingChunksInBackground(spawnChunkX, spawnChunkZ));
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
        // Мгновенная генерация (для отладки) - с визуализацией всех чанков
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        _chunks.Clear();

        if (generator == null) generator = GetComponent<VoxelWorldGenerator>() ?? gameObject.AddComponent<VoxelWorldGenerator>();

        // Генерируем все чанки сразу (с мешами)
        for (int cz = 0; cz < chunksZ; cz++)
            for (int cx = 0; cx < chunksX; cx++)
            {
                CreateChunk(cx, cz, null, null);
            }
        
        // Генерируем каньоны (терраформирование)
        if (generator != null)
        {
            generator.GenerateCanyonsInWorld(this);
        }
        
        // Генерируем деревья после каньонов
        if (generator != null)
        {
            generator.GenerateTreesInWorld(this);
        }
        
        // Вычисляем точку спавна после генерации
        CalculatePlayerSpawnPoint();
        Debug.Log($"VoxelWorld: Мгновенная генерация завершена. Точка спавна: {playerSpawnPoint}");
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
        builder.typeMaxHpLut[0] = 5;   // Трава
        builder.typeMaxHpLut[1] = 5;   // Земля
        builder.typeMaxHpLut[2] = 8;   // Камень
        builder.typeMaxHpLut[3] = 6;   // Дерево (ствол)
        builder.typeMaxHpLut[4] = 2;   // Листва (легко ломается)
        builder.typeMaxHpLut[6] = 12;  // Уголь
        builder.typeMaxHpLut[7] = 12;  // Золото
        
        // Настройка типов
        builder.typeToTileIndex = new int[256];
        builder.typeToTileIndex[0] = 0;  // Трава
        builder.typeToTileIndex[1] = 1;  // Земля
        builder.typeToTileIndex[2] = 2;  // Камень
        builder.typeToTileIndex[3] = 3;  // Дерево
        builder.typeToTileIndex[4] = 4;  // Листва
        builder.typeToTileIndex[6] = 6;  // Уголь
        builder.typeToTileIndex[7] = 7;  // Золото
        for (int t = 0; t < builder.typeToTileIndex.Length; t++)
            if (t != 0 && t != 1 && t != 2 && t != 3 && t != 4 && t != 6 && t != 7)
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
        
        // Добавляем в список для централизованного кулинга
        if (!chunkList.Contains(builder))
        {
            chunkList.Add(builder);
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
            WOOD => 6,
            LEAVES => 2,
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
            
            // Обновляем соседние чанки если блок на границе
            UpdateNeighborChunksIfOnEdge(lx, lz, cxi, czi);
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
        entry.hp[lx, wy, lz] = (short)GetMaxHP(blockType);

        if (rebuildChunk)
        {
            entry.builder.Build(entry.data);
            entry.builder.MarkDirty();
            
            // Обновляем соседние чанки если блок на границе
            UpdateNeighborChunksIfOnEdge(lx, lz, cxi, czi);
        }
        return true;
    }
    
    // === ПУБЛИЧНО: установить блок принудительно (перезаписывает существующие) ===
    public bool SetBlockForced(int wx, int wy, int wz, int blockType, bool rebuildChunk = true)
    {
        if (wx < 0 || wz < 0 || wy < 0 || wy >= VoxelChunk16.HEIGHT) return false;
        if (wx >= chunksX * VoxelChunk16.WIDTH || wz >= chunksZ * VoxelChunk16.DEPTH) return false;

        int cxi = wx / VoxelChunk16.WIDTH;
        int czi = wz / VoxelChunk16.DEPTH;
        int lx = wx % VoxelChunk16.WIDTH;
        int lz = wz % VoxelChunk16.DEPTH;

        if (!_chunks.TryGetValue((cxi, czi), out var entry)) return false;

        // Устанавливаем блок независимо от того, что там было
        entry.data[lx, wy, lz] = blockType;
        entry.hp[lx, wy, lz] = (short)(blockType == -1 ? 0 : GetMaxHP(blockType));

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
    
    // === Публичные методы для получения размеров мира ===
    public int GetWorldWidth()
    {
        return chunksX * VoxelChunk16.WIDTH;
    }
    
    public int GetWorldDepth()
    {
        return chunksZ * VoxelChunk16.DEPTH;
    }
    
    public int GetWorldHeight()
    {
        return VoxelChunk16.HEIGHT;
    }
    
    public Vector3 GetWorldSize()
    {
        return new Vector3(GetWorldWidth(), GetWorldHeight(), GetWorldDepth());
    }
    
    /// <summary>
    /// Конвертирует мировые координаты в воксельные координаты
    /// </summary>
    public static Vector3Int WorldToVoxel(Vector3 worldPosition)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPosition.x),
            Mathf.FloorToInt(worldPosition.y),
            Mathf.FloorToInt(worldPosition.z)
        );
    }
    
    /// <summary>
    /// Конвертирует воксельные координаты в мировые координаты
    /// </summary>
    public static Vector3 VoxelToWorld(Vector3Int voxelPosition)
    {
        return new Vector3(
            voxelPosition.x + 0.5f,
            voxelPosition.y + 0.5f,
            voxelPosition.z + 0.5f
        );
    }
    
    /// <summary>
    /// Проверяет есть ли блок в указанных воксельных координатах
    /// </summary>
    public static bool IsVoxelSolid(Vector3Int voxelPosition)
    {
        if (Instance == null) return false;
        
        // Вычисляем координаты чанка
        int chunkX = Mathf.FloorToInt(voxelPosition.x / VoxelChunk16.WIDTH);
        int chunkZ = Mathf.FloorToInt(voxelPosition.z / VoxelChunk16.DEPTH);
        
        // Проверяем есть ли чанк
        if (!Instance._chunks.TryGetValue((chunkX, chunkZ), out ChunkEntry chunk))
        {
            return false; // Чанк не существует
        }
        
        // Вычисляем локальные координаты в чанке
        int localX = voxelPosition.x - chunkX * VoxelChunk16.WIDTH;
        int localY = voxelPosition.y;
        int localZ = voxelPosition.z - chunkZ * VoxelChunk16.DEPTH;
        
        // Проверяем границы
        if (localX < 0 || localX >= VoxelChunk16.WIDTH ||
            localY < 0 || localY >= VoxelChunk16.HEIGHT ||
            localZ < 0 || localZ >= VoxelChunk16.DEPTH)
        {
            return false;
        }
        
        // Проверяем тип блока
        return chunk.data[localX, localY, localZ] != AIR;
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
    
    // === Публичный метод для установки игрока (для кулинга чанков) ===
    public void SetPlayer(Transform playerTransform)
    {
        cachedPlayerTransform = playerTransform;
        cachedPlayerController = playerTransform?.GetComponent<PlayerController>(); // Кешируем PlayerController
        Debug.Log($"VoxelWorld: Игрок установлен. Централизованный кулинг активирован для {chunkList.Count} чанков");
    }
    
    // === Приватный метод для обновления соседних чанков если блок на границе ===
    private void UpdateNeighborChunksIfOnEdge(int lx, int lz, int chunkX, int chunkZ)
    {
        // ОПТИМИЗАЦИЯ: Вместо немедленного Build() помечаем соседей для отложенного rebuild
        // Debounce 300ms - если много изменений, перестроится только раз
        
        // X = 0 (левая граница) - помечаем чанк слева
        if (lx == 0 && chunkX > 0)
        {
            if (_chunks.TryGetValue((chunkX - 1, chunkZ), out var leftChunk))
            {
                leftChunk.builder.MarkNeedsRebuild();
            }
        }
        // X = WIDTH-1 (правая граница) - помечаем чанк справа
        if (lx == VoxelChunk16.WIDTH - 1 && chunkX < chunksX - 1)
        {
            if (_chunks.TryGetValue((chunkX + 1, chunkZ), out var rightChunk))
            {
                rightChunk.builder.MarkNeedsRebuild();
            }
        }
        
        // Z = 0 (нижняя граница) - помечаем чанк снизу
        if (lz == 0 && chunkZ > 0)
        {
            if (_chunks.TryGetValue((chunkX, chunkZ - 1), out var bottomChunk))
            {
                bottomChunk.builder.MarkNeedsRebuild();
            }
        }
        // Z = DEPTH-1 (верхняя граница) - помечаем чанк сверху
        if (lz == VoxelChunk16.DEPTH - 1 && chunkZ < chunksZ - 1)
        {
            if (_chunks.TryGetValue((chunkX, chunkZ + 1), out var topChunk))
            {
                topChunk.builder.MarkNeedsRebuild();
            }
        }
    }
    
    // === Публичный метод для получения типа блока ===
    public int GetBlockType(int wx, int wy, int wz)
    {
        if (wx < 0 || wz < 0 || wy < 0 || wy >= VoxelChunk16.HEIGHT) return AIR;
        if (wx >= chunksX * VoxelChunk16.WIDTH || wz >= chunksZ * VoxelChunk16.DEPTH) return AIR;

        int cxi = wx / VoxelChunk16.WIDTH;
        int czi = wz / VoxelChunk16.DEPTH;
        int lx = wx % VoxelChunk16.WIDTH;
        int lz = wz % VoxelChunk16.DEPTH;

        if (!_chunks.TryGetValue((cxi, czi), out var entry)) return AIR;
        
        return entry.data[lx, wy, lz];
    }
    
    // === Публичный метод для перестройки чанка ===
    public void RebuildChunk(int cx, int cz)
    {
        if (!_chunks.TryGetValue((cx, cz), out var entry)) return;
        entry.builder.Build(entry.data);
    }
    
    // === Метод для размещения дерева ===
    public bool PlaceTree(Vector3Int position, List<(Vector3Int pos, int type)> treeBlocks)
    {
        if (treeBlocks == null || treeBlocks.Count == 0)
            return false;
        
        HashSet<(int cx, int cz)> affectedChunks = new HashSet<(int, int)>();
        
        // Размещаем все блоки дерева
        foreach (var block in treeBlocks)
        {
            int wx = block.pos.x;
            int wy = block.pos.y;
            int wz = block.pos.z;
            int blockType = block.type;
            
            // Проверка границ
            if (wx < 0 || wz < 0 || wy < 0 || wy >= VoxelChunk16.HEIGHT) continue;
            if (wx >= chunksX * VoxelChunk16.WIDTH || wz >= chunksZ * VoxelChunk16.DEPTH) continue;
            
            int cxi = wx / VoxelChunk16.WIDTH;
            int czi = wz / VoxelChunk16.DEPTH;
            int lx = wx % VoxelChunk16.WIDTH;
            int lz = wz % VoxelChunk16.DEPTH;
            
            if (!_chunks.TryGetValue((cxi, czi), out var entry)) continue;
            
            // Ставим блок только в воздух
            if (entry.data[lx, wy, lz] == AIR)
            {
                entry.data[lx, wy, lz] = blockType;
                entry.hp[lx, wy, lz] = (short)GetMaxHP(blockType);
                affectedChunks.Add((cxi, czi));
            }
        }
        
        // Перестраиваем затронутые чанки и помечаем для сохранения
        foreach (var chunkKey in affectedChunks)
        {
            if (_chunks.TryGetValue(chunkKey, out var entry))
            {
                entry.builder.Build(entry.data);
                entry.builder.MarkDirty(); // Помечаем для автосохранения
            }
        }
        
        return affectedChunks.Count > 0;
    }
    
    // ========== УТИЛИТЫ ДЛЯ СПАВНА ==========
    
    /// <summary>
    /// Рассчитать точку спавна игрока при генерации мира
    /// Точка находится в центре мира, на один блок выше поверхности
    /// </summary>
    private void CalculatePlayerSpawnPoint()
    {
        if (_chunks.Count == 0)
        {
            Debug.LogWarning("VoxelWorld: Нет чанков для расчета точки спавна, используем центр мира на высоте 80");
            playerSpawnPoint = new Vector3(chunksX * VoxelChunk16.WIDTH / 2f, 80f, chunksZ * VoxelChunk16.DEPTH / 2f);
            return;
        }
        
        // Вычисляем центр мира
        int centerX = (chunksX * VoxelChunk16.WIDTH) / 2;
        int centerZ = (chunksZ * VoxelChunk16.DEPTH) / 2;
        
        // Ищем поверхность в центре мира (сверху вниз)
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
                    // Нашли поверхность - устанавливаем точку спавна на 1 блок выше (y + 1)
                    playerSpawnPoint = new Vector3(centerX + 0.5f, y + 1f, centerZ + 0.5f);
                    Debug.Log($"VoxelWorld: Точка спавна рассчитана - центр мира ({centerX}, {centerZ}), высота поверхности: {y}");
                    return;
                }
            }
        }
        
        // Если не нашли поверхность - используем высоту по умолчанию
        Debug.LogWarning($"VoxelWorld: Не найдена поверхность в центре мира, используем высоту 80");
        playerSpawnPoint = new Vector3(centerX + 0.5f, 80f, centerZ + 0.5f);
    }
    
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
    
    /// <summary>
    /// Корутина полной регенерации мира (вызывается из GameBootstrap)
    /// </summary>
    public System.Collections.IEnumerator RegenerateWorldCoroutine()
    {
        IsGenerating = true;
        IsWorldReady = false;
        
        Debug.Log("VoxelWorld: Начинаем регенерацию...");
        
        // 1. Удаляем все сохранения чанков
        Debug.Log("VoxelWorld: Удаление сохранений...");
        for (int cz = 0; cz < chunksZ; cz++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                string chunkName = $"Chunk_{cx}_{cz}";
                SaveManager.DeleteSave(chunkName);
            }
        }
        
        // 2. Сбрасываем флаг первого запуска для перегенерации
        var settings = new GameSettingsData();
        settings.isFirstRun = true;
        settings.worldChunksX = chunksX;
        settings.worldChunksZ = chunksZ;
        gameSettings.Data = settings;
        gameSettings.Save();
        
        Debug.Log("VoxelWorld: Сохранения удалены");
        yield return null;
        
        // 3. Очищаем текущие чанки
        Debug.Log("VoxelWorld: Очистка старых чанков...");
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        _chunks.Clear();
        yield return new WaitForSeconds(0.1f); // Даем время на уничтожение объектов
        
        // 4. Рандомизируем параметры генератора для нового уникального мира
        Debug.Log("VoxelWorld: Рандомизация параметров генератора...");
        if (generator != null)
        {
            generator.RandomizeParameters();
        }
        yield return null;
        
        // 5. Генерируем новый мир
        Debug.Log("VoxelWorld: Генерация нового мира...");
        if (useProgressiveGeneration)
        {
            yield return StartCoroutine(GenerateWorldProgressive());
        }
        else
        {
            Generate();
        }
        
        // Помечаем что уже не первый запуск (для следующих загрузок)
        settings.isFirstRun = false;
        gameSettings.Data = settings;
        gameSettings.Save();
        
        IsGenerating = false;
        IsWorldReady = true;
        
        // 6. Принудительно сохраняем все чанки нового мира
        Debug.Log("VoxelWorld: Сохранение новых чанков...");
        ForceSaveAllChunks();
        
        Debug.Log("VoxelWorld: Регенерация мира завершена!");
        
        // 7. Уведомляем о готовности мира
        GlobalEvents.WorldReady.Invoke();
    }
    
}
