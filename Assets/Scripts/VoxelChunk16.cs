using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelChunk16 : MonoBehaviour
{
    public const int WIDTH = 16;
    public const int HEIGHT = 128;
    public const int DEPTH = 16;

    public Material atlasMaterial;
    public bool generateCollider = true;

    // карта соответствия типов → базовый индекс тайла в атласе
    public int[] typeToTileIndex = new int[256];

    // ===== для повреждённых тайлов =====
    public short[,,] hpData;                   // ссылка на HP-массив из VoxelWorld
    public bool useDamageTiles = true;
    public int damageStates = 5;               // 5 состояний (0..4)
    public int[] typeMaxHpLut = new int[256];  // max HP по типам

    // ===== приватное =====
    Mesh _mesh;
    MeshCollider _collider;
    
    // ===== система автосохранения =====
    private SaveData<SingleChunkData> saveData;
    private int[,,] cachedData;                // Кешированные данные блоков
    private bool isDirty = false;              // Были ли изменения
    private float dirtyMarkTime = 0f;          // Время когда чанк был помечен как dirty
    private const float SAVE_DELAY = 5f;       // Задержка перед сохранением (5 секунд после изменения)
    public int chunkX { get; private set; }
    public int chunkZ { get; private set; }
    
    // ===== система отложенного rebuild =====
    private bool needsRebuild = false;         // Нужно ли перестроить меш
    private float rebuildMarkTime = 0f;        // Время когда помечен для rebuild
    private const float REBUILD_DELAY = 0.3f;  // Задержка перед rebuild (300ms debounce)
    
    // ===== система кулинга =====
    private bool isVisualizationActive = true; // Текущее состояние визуализации
    private MeshRenderer meshRenderer;         // Для включения/выключения
    
    // ===== для проверки соседних чанков =====
    private VoxelWorld voxelWorld;

    // подготовленные UV-координаты (кеш на каждый tileIndex)
    static readonly Dictionary<int, Vector2[]> uvCache = new();
    const int atlasCols = 10; // у тебя атлас 10x10

    /// <summary>
    /// Инициализация чанка для автосохранения
    /// </summary>
    public void Initialize(int cx, int cz, int[,,] data)
    {
        chunkX = cx;
        chunkZ = cz;
        cachedData = data;
        
        // Создаем SaveData с уникальным именем чанка
        string chunkName = $"Chunk_{cx}_{cz}";
        saveData = new SaveData<SingleChunkData>(chunkName);
        
        // Кешируем MeshRenderer
        meshRenderer = GetComponent<MeshRenderer>();
        
        // Кешируем VoxelWorld для проверки соседних чанков
        voxelWorld = VoxelWorld.Instance;
    }
    
    /// <summary>
    /// Централизованная проверка дистанции и видимости камерой (вызывается из VoxelWorld)
    /// Использует КВАДРАТ расстояния для оптимизации (без Mathf.Sqrt)
    /// </summary>
    public void CheckCullingCentralized(Vector3 playerPos, float viewDistanceSqr, Plane[] frustumPlanes)
    {
        // 1. Вычисляем КВАДРАТ расстояния БЕЗ Mathf.Sqrt (быстрее!)
        Vector3 pos = transform.position;
        float dx = (pos.x + WIDTH * 0.5f) - playerPos.x;
        float dz = (pos.z + DEPTH * 0.5f) - playerPos.z;
        float distanceSqr = dx * dx + dz * dz;
        
        // 2. Проверка дистанции
        bool isInRange = distanceSqr <= viewDistanceSqr;
        
        // 3. Проверка видимости камерой (Frustum Culling)
        bool isInFrustum = false;
        if (isInRange && meshRenderer != null)
        {
            isInFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, meshRenderer.bounds);
        }
        
        // 4. Чанк видим только если в зоне видимости И в frustum камеры
        bool shouldBeVisible = isInRange && isInFrustum;
        
        // Если состояние изменилось - обновляем
        if (shouldBeVisible != isVisualizationActive)
        {
            SetVisualizationActive(shouldBeVisible);
        }
    }
    
    /// <summary>
    /// Включить/выключить визуализацию чанка
    /// </summary>
    void SetVisualizationActive(bool active)
    {
        isVisualizationActive = active;
        
        // Включаем/выключаем рендерер
        if (meshRenderer != null)
        {
            meshRenderer.enabled = active;
        }
        
        // Коллайдеры всегда активны (для физики игрока и снарядов)
    }
    
    void Update()
    {
        // Отложенный rebuild: проверяем прошло ли 300ms с момента пометки
        if (needsRebuild && Time.time - rebuildMarkTime >= REBUILD_DELAY)
        {
            if (cachedData != null)
            {
                Build(cachedData);
            }
            needsRebuild = false;
        }
        
        // Автосохранение: проверяем прошло ли 5 секунд с момента изменения
        if (isDirty && Time.time - dirtyMarkTime >= SAVE_DELAY)
        {
            SaveChunk();
        }
        
        // Кулинг теперь централизован в VoxelWorld.Update()
    }
    
    void OnDestroy()
    {
        // Сохраняем при уничтожении если были изменения
        if (isDirty && saveData != null)
        {
            SaveChunk();
        }
    }
    
    /// <summary>
    /// Сохранить чанк
    /// </summary>
    public void SaveChunk()
    {
        if (saveData == null || cachedData == null || hpData == null)
            return;
        
        var data = new SingleChunkData();
        data.PackData(chunkX, chunkZ, cachedData, hpData);
        
        saveData.Data = data;
        saveData.Save();
        
        isDirty = false;
        //Debug.Log($"Chunk ({chunkX}, {chunkZ}) сохранен");
    }
    
    /// <summary>
    /// Загрузить данные чанка из сохранения
    /// </summary>
    public bool LoadChunk(out int[,,] data, out short[,,] hp)
    {
        data = null;
        hp = null;
        
        if (saveData == null || !saveData.Exists())
            return false;
        
        var chunkData = saveData.Load();
        if (chunkData == null)
            return false;
        
        chunkData.UnpackData(out data, out hp);
        cachedData = data;
        isDirty = false;
        
        return true;
    }
    
    /// <summary>
    /// Пометить чанк как измененный (вызывается из VoxelWorld при повреждениях)
    /// </summary>
    public void MarkDirty()
    {
        if (!isDirty)
        {
            // Запоминаем время первого изменения
            dirtyMarkTime = Time.time;
            isDirty = true;
            //Debug.Log($"Chunk ({chunkX}, {chunkZ}) помечен как измененный, автосохранение через 5 секунд");
        }
    }
    
    /// <summary>
    /// Пометить чанк для отложенного rebuild (debounce)
    /// </summary>
    public void MarkNeedsRebuild()
    {
        if (!needsRebuild)
        {
            needsRebuild = true;
            rebuildMarkTime = Time.time;
        }
    }

    public void Build(int[,,] data)
    {
        // Сохраняем данные для автосохранения
        cachedData = data;
        
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = $"ChunkMesh_{chunkX}_{chunkZ}";
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.MarkDynamic(); // ОПТИМИЗАЦИЯ: помечаем меш как динамический для быстрых обновлений
            GetComponent<MeshFilter>().mesh = _mesh;
            
            // Кешируем MeshRenderer при первом создании меша
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
            meshRenderer.material = atlasMaterial;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int x = 0; x < WIDTH; x++)
            for (int y = 0; y < HEIGHT; y++)
                for (int z = 0; z < DEPTH; z++)
                {
                    int type = data[x, y, z];
                    if (type == -1) continue;
                    
                    // КРИТИЧЕСКАЯ ОПТИМИЗАЦИЯ: Проверяем есть ли хотя бы одна видимая грань
                    // Если блок полностью окружен - пропускаем его
                    if (IsFullyEnclosed(x, y, z, data))
                    {
                        continue; // Блок полностью скрыт - не рендерим
                    }

                    // --- здесь решаем индекс тайла (с учётом повреждений) ---
                    int tileIndex = GetTileIndexWithDamage(type, x, y, z);

                    // соседние блоки
                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int dir = VoxelData.dirs[face];
                        int nx = x + dir.x;
                        int ny = y + dir.y;
                        int nz = z + dir.z;

                        bool neighborSolid = false;
                        
                        // Проверяем соседа внутри чанка
                        if (nx >= 0 && nx < WIDTH && ny >= 0 && ny < HEIGHT && nz >= 0 && nz < DEPTH)
                        {
                            neighborSolid = data[nx, ny, nz] != -1;
                        }
                        // Сосед за границей чанка - проверяем соседний чанк
                        else if (voxelWorld != null)
                        {
                            // Мировые координаты соседнего блока
                            int worldX = chunkX * WIDTH + nx;
                            int worldY = ny;
                            int worldZ = chunkZ * DEPTH + nz;
                            
                            neighborSolid = voxelWorld.HasBlockAt(worldX, worldY, worldZ);
                        }

                        if (!neighborSolid)
                        {
                            int vIndex = verts.Count;

                            for (int i = 0; i < 4; i++)
                                verts.Add(new Vector3(x, y, z) + VoxelData.faceVerts[face, i]);

                            tris.Add(vIndex + 0);
                            tris.Add(vIndex + 1);
                            tris.Add(vIndex + 2);
                            tris.Add(vIndex + 2);
                            tris.Add(vIndex + 1);
                            tris.Add(vIndex + 3);

                            var faceUvs = GetTileUvs(tileIndex);
                            uvs.AddRange(faceUvs);
                        }
                    }
                }

        // КРИТИЧНО: Проверяем что есть вертексы ДО очистки меша
        if (verts.Count > 0)
        {
            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.SetUVs(0, uvs);
            
            // КРИТИЧЕСКАЯ ОПТИМИЗАЦИЯ: Не пересчитываем нормали каждый раз
            // Для воксельного мира с прямыми гранями нормали всегда одинаковые
            _mesh.RecalculateNormals(UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | 
                                     UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            
            // Debug статистика (можно закомментировать в продакшене)
            #if UNITY_EDITOR
            if (verts.Count > 20000)
            {
                Debug.LogWarning($"Chunk ({chunkX},{chunkZ}): {verts.Count} verts, {tris.Count/3} tris - критично много!");
            }
            #endif

            // Создаем/обновляем коллайдер только если есть вертексы
            if (generateCollider)
            {
                if (_collider == null)
                {
                    _collider = gameObject.GetComponent<MeshCollider>();
                    if (_collider == null)
                    {
                        _collider = gameObject.AddComponent<MeshCollider>();
                        _collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                                   MeshColliderCookingOptions.EnableMeshCleaning |
                                                   MeshColliderCookingOptions.WeldColocatedVertices;
                    }
                }
                
                _collider.sharedMesh = null;
                _collider.sharedMesh = _mesh;
                _collider.enabled = true;
            }
        }
        else
        {
            // Чанк полностью заполнен или пуст - нет видимых граней
            // Отключаем рендерер для экономии
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
            
            // Освобождаем коллайдер от пустого меша
            if (_collider != null)
            {
                _collider.sharedMesh = null;
                _collider.enabled = false;
            }
        }
    }

    // ===== Helpers =====
    
    /// <summary>
    /// КРИТИЧЕСКАЯ ОПТИМИЗАЦИЯ: Проверяет окружен ли блок со всех сторон
    /// Если блок полностью окружен - его грани не видны, можно не рендерить
    /// </summary>
    bool IsFullyEnclosed(int x, int y, int z, int[,,] data)
    {
        // Проверяем все 6 направлений
        for (int face = 0; face < 6; face++)
        {
            Vector3Int dir = VoxelData.dirs[face];
            int nx = x + dir.x;
            int ny = y + dir.y;
            int nz = z + dir.z;
            
            // Проверяем внутри чанка
            if (nx >= 0 && nx < WIDTH && ny >= 0 && ny < HEIGHT && nz >= 0 && nz < DEPTH)
            {
                if (data[nx, ny, nz] == -1) // Воздух рядом
                {
                    return false; // Есть видимая грань
                }
            }
            // На границе чанка - проверяем соседний чанк
            else if (voxelWorld != null)
            {
                int worldX = chunkX * WIDTH + nx;
                int worldY = ny;
                int worldZ = chunkZ * DEPTH + nz;
                
                if (!voxelWorld.HasBlockAt(worldX, worldY, worldZ))
                {
                    return false; // Есть видимая грань
                }
            }
            else
            {
                // Если нет VoxelWorld - считаем что за границей воздух
                return false;
            }
        }
        
        // Все 6 сторон закрыты - блок не виден
        return true;
    }

    int GetTileIndexWithDamage(int type, int x, int y, int z)
    {
        int baseIndex = (type >= 0 && type < typeToTileIndex.Length) ? typeToTileIndex[type] : 0;

        if (!useDamageTiles || hpData == null) return baseIndex;

        int maxHp = (type >= 0 && type < typeMaxHpLut.Length) ? typeMaxHpLut[type] : 0;
        if (maxHp <= 0) return baseIndex;

        int curHp = Mathf.Clamp(hpData[x, y, z], 0, maxHp);
        float ratio = (float)curHp / maxHp;

        int state;
        if (ratio >= 0.80f) state = 0;
        else if (ratio >= 0.50f) state = 1;
        else if (ratio >= 0.35f) state = 2;
        else if (ratio >= 0.20f) state = 3;
        else state = 4;

        return baseIndex + state * atlasCols;
    }

    Vector2[] GetTileUvs(int tileIndex)
    {
        if (uvCache.TryGetValue(tileIndex, out var cached)) return cached;

        int tx = tileIndex % atlasCols;
        int ty = tileIndex / atlasCols;

        float uvSize = 1f / atlasCols;
        float eps = 0.001f;

        float u0 = tx * uvSize + eps;
        float v0 = ty * uvSize + eps;
        float u1 = (tx + 1) * uvSize - eps;
        float v1 = (ty + 1) * uvSize - eps;

        var uv = new Vector2[4]
        {
            new Vector2(u0, v0),
            new Vector2(u1, v0),
            new Vector2(u0, v1),
            new Vector2(u1, v1)
        };

        uvCache[tileIndex] = uv;
        return uv;
    }
}

// Вспомогательные данные граней куба
public static class VoxelData
{
    public static readonly Vector3Int[] dirs = {
        new Vector3Int( 1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int( 0, 1, 0),
        new Vector3Int( 0,-1, 0),
        new Vector3Int( 0, 0, 1),
        new Vector3Int( 0, 0,-1)
    };

    public static readonly Vector3[,] faceVerts = {
        { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,0,1), new Vector3(1,1,1) }, // +X
        { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,0), new Vector3(0,1,1) }, // -X
        { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,0), new Vector3(1,1,1) }, // +Y
        { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(0,0,1), new Vector3(1,0,1) }, // -Y
        { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(0,1,1), new Vector3(1,1,1) }, // +Z
        { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,0,0), new Vector3(1,1,0) }  // -Z
    };
}
