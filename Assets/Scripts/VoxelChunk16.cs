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
    
    // ===== система кулинга =====
    private Transform playerTransform;         // Ссылка на игрока
    private PlayerController playerController; // Для получения viewDistance
    private float nextCullingCheckTime = 0f;   // Время следующей проверки
    private bool isVisualizationActive = true; // Текущее состояние визуализации
    private MeshRenderer meshRenderer;         // Для включения/выключения

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
    }
    
    /// <summary>
    /// Установить игрока для системы кулинга
    /// </summary>
    public void SetPlayer(Transform player)
    {
        if (player != null)
        {
            playerTransform = player;
            playerController = player.GetComponent<PlayerController>();
            
            // Первая проверка дистанции со случайной задержкой
            nextCullingCheckTime = Time.time + Random.Range(0f, 2f);
        }
    }
    
    /// <summary>
    /// Проверка дистанции и управление визуализацией
    /// </summary>
    void CheckCulling()
    {
        if (playerTransform == null || playerController == null)
            return;
        
        // Вычисляем расстояние только по XZ (горизонтали)
        Vector3 chunkCenter = transform.position + new Vector3(WIDTH * 0.5f, 0, DEPTH * 0.5f);
        Vector3 playerPos = playerTransform.position;
        
        float distanceXZ = Vector2.Distance(
            new Vector2(chunkCenter.x, chunkCenter.z),
            new Vector2(playerPos.x, playerPos.z)
        );
        
        // Определяем нужно ли показывать чанк
        // Добавляем буфер +50% чтобы чанки исчезали позже чем враги деспавнятся
        float chunkCullingDistance = playerController.viewDistance * 1.5f;
        bool shouldBeVisible = distanceXZ <= chunkCullingDistance;
        
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
        
        // Включаем/выключаем коллайдер
        if (_collider != null)
        {
            _collider.enabled = active;
        }
    }
    
    void Update()
    {
        // Автосохранение: проверяем прошло ли 5 секунд с момента изменения
        if (isDirty && Time.time - dirtyMarkTime >= SAVE_DELAY)
        {
            SaveChunk();
        }
        
        // Кулинг: проверяем дистанцию до игрока раз в ~10 секунд
        if (Time.time >= nextCullingCheckTime)
        {
            CheckCulling();
            // Следующая проверка через 10 +- 2 секунды (рандом для распределения нагрузки)
            nextCullingCheckTime = Time.time + Random.Range(8f, 12f);
        }
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

    public void Build(int[,,] data)
    {
        // Сохраняем данные для автосохранения
        cachedData = data;
        
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            GetComponent<MeshFilter>().mesh = _mesh;
            GetComponent<MeshRenderer>().material = atlasMaterial;
        }

        if (_collider == null && generateCollider)
        {
            _collider = gameObject.GetComponent<MeshCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
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

                    // --- здесь решаем индекс тайла (с учётом повреждений) ---
                    int tileIndex = GetTileIndexWithDamage(type, x, y, z);

                    // соседние блоки
                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int dir = VoxelData.dirs[face];
                        int nx = x + dir.x;
                        int ny = y + dir.y;
                        int nz = z + dir.z;

                        bool neighborSolid =
                            nx >= 0 && nx < WIDTH &&
                            ny >= 0 && ny < HEIGHT &&
                            nz >= 0 && nz < DEPTH &&
                            data[nx, ny, nz] != -1;

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

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0);
        _mesh.SetUVs(0, uvs);
        _mesh.RecalculateNormals();

        if (generateCollider && _collider != null)
        {
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
        }
    }

    // ===== Helpers =====

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
