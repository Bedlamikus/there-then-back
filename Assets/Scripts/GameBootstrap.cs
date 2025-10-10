using UnityEngine;
using System.Collections;

/// <summary>
/// Точка входа в игру - управляет последовательностью инициализации
/// Последовательность: 1. Генерация мира → 2. Спавн игрока
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Префаб игрока для спавна")]
    public GameObject playerPrefab;
    
    [Tooltip("Ссылка на VoxelWorld (если не указана, будет найдена автоматически)")]
    public VoxelWorld voxelWorld;
    
    [Tooltip("Ссылка на CameraController (если не указана, будет найдена автоматически)")]
    public CameraController cameraController;
    
    [Tooltip("Массив спавнеров врагов для инициализации")]
    public EnemySpawner[] enemySpawners;
    
    [Tooltip("Автопоиск спавнеров в сцене")]
    public bool autoFindSpawners = true;
    
    [Header("Settings")]
    [Tooltip("Ждать готовности мира перед спавном игрока")]
    public bool waitForWorldReady = true;
    
    [Tooltip("Максимальное время ожидания мира (секунды)")]
    public float maxWaitTime = 60f;
    
    [Tooltip("Отключить игрока пока мир не готов")]
    public bool disablePlayerUntilReady = true;
    
    [Tooltip("Инициализировать камеру после спавна игрока")]
    public bool initializeCamera = true;
    
    [Header("Player Save")]
    [Tooltip("Сохранять позицию игрока")]
    public bool savePlayerPosition = true;
    
    [Tooltip("Интервал автосохранения позиции (секунды)")]
    public float playerSaveInterval = 5f;
    
    private GameObject spawnedPlayer;
    private bool isInitialized = false;
    private SaveData<PlayerPositionData> playerPositionSave;
    
    void Start()
    {
        // Инициализируем систему сохранения позиции
        if (savePlayerPosition)
        {
            playerPositionSave = new SaveData<PlayerPositionData>("PlayerPosition");
        }
        
        StartCoroutine(InitializeGame());
    }
    
    void Update()
    {
        // Автосохранение позиции игрока
        if (isInitialized && savePlayerPosition && spawnedPlayer != null)
        {
            SavePlayerPositionPeriodically();
        }
    }
    
    void OnApplicationQuit()
    {
        // Сохраняем позицию при выходе
        if (savePlayerPosition && spawnedPlayer != null)
        {
            SavePlayerPositionNow();
        }
    }
    
    /// <summary>
    /// Главная последовательность инициализации игры
    /// </summary>
    IEnumerator InitializeGame()
    {
        
        
        // Шаг 1: Найти VoxelWorld если не указан
        if (voxelWorld == null)
        {
            
            voxelWorld = FindObjectOfType<VoxelWorld>();
            
            if (voxelWorld == null)
            {
                Debug.LogError("GameBootstrap: VoxelWorld не найден в сцене!");
                yield break;
            }
            
            
        }
        
        // Шаг 2: Ждать готовности мира
        if (waitForWorldReady)
        {
            
            
            float startTime = Time.time;
            while (!voxelWorld.IsWorldReady)
            {
                // Проверка таймаута
                if (Time.time - startTime > maxWaitTime)
                {
                    Debug.LogError($"GameBootstrap: Превышено время ожидания мира ({maxWaitTime} сек)!");
                    yield break;
                }
                
                yield return null;
            }
            
            Debug.Log($"GameBootstrap: Мир готов! (время ожидания: {Time.time - startTime:F2} сек)");
        }
        
        // Шаг 3: Спавн игрока
        yield return StartCoroutine(SpawnPlayer());
        
        // Шаг 4: Инициализация камеры
        if (initializeCamera)
        {
            yield return StartCoroutine(InitializeCamera());
        }
        
        // Шаг 5: Инициализация спавнеров врагов
        InitializeEnemySpawners();
        
        isInitialized = true;
        
    }
    
    /// <summary>
    /// Спавн игрока из префаба
    /// </summary>
    IEnumerator SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("GameBootstrap: Префаб игрока не указан!");
            yield break;
        }
        
        
        
        // Определяем позицию спавна
        Vector3 spawnPosition;
        Quaternion spawnRotation = Quaternion.identity;
        
        // Проверяем наличие сохраненной позиции
        if (savePlayerPosition && playerPositionSave != null && playerPositionSave.Exists())
        {
            var savedPosition = playerPositionSave.Load();
            spawnPosition = savedPosition.GetPosition();
            spawnRotation = savedPosition.GetRotation();
            
            
            
            // Проверяем, что сохраненная позиция безопасна
            if (!IsPositionSafe(spawnPosition))
            {
                
                
                // Пытаемся найти безопасную позицию рядом с сохраненной
                Vector3 nearbyPosition = FindSafePositionNearby(spawnPosition, searchRadius: 10f, maxAttempts: 50);
                
                if (nearbyPosition != Vector3.zero)
                {
                    spawnPosition = nearbyPosition;
                    
                }
                else
                {
                    // Не нашли рядом - используем стандартный спавн
                    
                    spawnPosition = voxelWorld.GetSafeSpawnPosition();
                    spawnRotation = Quaternion.identity;
                    
                }
            }
            else
            {
                
            }
        }
        else
        {
            // Получаем безопасную позицию спавна из VoxelWorld
            spawnPosition = voxelWorld.GetSafeSpawnPosition();
            
        }
        
        // Создаем игрока
        spawnedPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        spawnedPlayer.name = "Player"; // Убираем (Clone) из имени
        
        
        
        // Если нужно, отключаем игрока пока мир не готов
        if (disablePlayerUntilReady && !voxelWorld.IsWorldReady)
        {
            var controller = spawnedPlayer.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                
            }
            
            // Ждем готовности
            while (!voxelWorld.IsWorldReady)
            {
                yield return null;
            }
            
            // Включаем обратно
            if (controller != null)
            {
                controller.enabled = true;
                
            }
        }
        
        
    }
    
    /// <summary>
    /// Инициализация камеры
    /// </summary>
    IEnumerator InitializeCamera()
    {
        
        
        // Найти CameraController если не указан
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            
            if (cameraController == null)
            {
                
                yield break;
            }
            
            
        }
        
        // Проверяем что игрок создан
        if (spawnedPlayer == null)
        {
            Debug.LogError("GameBootstrap: Игрок не создан, невозможно инициализировать камеру!");
            yield break;
        }
        
        // Получаем трансформ игрока
        Transform playerTransform = spawnedPlayer.transform;
        
        // Устанавливаем ссылку на игрока в CameraController
        cameraController.playerTransform = playerTransform;
        
        // Получаем PlayerController для настройки cameraPivot
        PlayerController playerController = spawnedPlayer.GetComponent<PlayerController>();
        if (playerController != null && playerController.cameraPivot == null)
        {
            // Создаем cameraPivot если его нет
            GameObject pivotGO = new GameObject("CameraPivot");
            pivotGO.transform.SetParent(playerTransform);
            pivotGO.transform.localPosition = Vector3.zero;
            pivotGO.transform.localRotation = Quaternion.identity;
            
            playerController.cameraPivot = pivotGO.transform;
            
        }
        
        // Сбрасываем камеру для инициализации позиции
        cameraController.ResetCamera();
        
        
        
        yield return null;
    }
    
    /// <summary>
    /// Инициализация спавнеров врагов
    /// </summary>
    void InitializeEnemySpawners()
    {
        
        
        // Автопоиск спавнеров если массив пуст
        if ((enemySpawners == null || enemySpawners.Length == 0) && autoFindSpawners)
        {
            enemySpawners = FindObjectsOfType<EnemySpawner>();
            
            if (enemySpawners.Length > 0)
            {
                
            }
        }
        
        if (enemySpawners == null || enemySpawners.Length == 0)
        {
            
            return;
        }
        
        // Проверяем что игрок создан
        if (spawnedPlayer == null)
        {
            Debug.LogError("GameBootstrap: Игрок не создан, невозможно инициализировать спавнеры!");
            return;
        }
        
        Transform playerTransform = spawnedPlayer.transform;
        
        // Инициализируем каждый спавнер
        int initializedCount = 0;
        foreach (var spawner in enemySpawners)
        {
            if (spawner != null)
            {
                // Передаем ссылку на игрока
                spawner.SetPlayerTarget(playerTransform);
                
                // Вызываем Initialize() для запуска спавнера
                spawner.Initialize();
                
                initializedCount++;
            }
        }
        
        
    }
    
    /// <summary>
    /// Получить заспавненного игрока
    /// </summary>
    public GameObject GetPlayer()
    {
        return spawnedPlayer;
    }
    
    /// <summary>
    /// Проверить, завершена ли инициализация
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }
    
    /// <summary>
    /// Переспавнить игрока в новой позиции
    /// </summary>
    public void RespawnPlayer()
    {
        if (spawnedPlayer == null)
        {
            
            return;
        }
        
        Vector3 spawnPosition = voxelWorld.GetSafeSpawnPosition();
        spawnedPlayer.transform.position = spawnPosition;
        
        // Сбрасываем физику
        var controller = spawnedPlayer.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            controller.enabled = true;
        }
        
        
    }
    
    // ========== СОХРАНЕНИЕ ПОЗИЦИИ ИГРОКА ==========
    
    private float lastSaveTime = 0f;
    
    /// <summary>
    /// Периодическое сохранение позиции игрока
    /// </summary>
    void SavePlayerPositionPeriodically()
    {
        if (Time.time - lastSaveTime >= playerSaveInterval)
        {
            SavePlayerPositionNow();
            lastSaveTime = Time.time;
        }
    }
    
    /// <summary>
    /// Сохранить позицию игрока прямо сейчас
    /// </summary>
    void SavePlayerPositionNow()
    {
        if (playerPositionSave == null || spawnedPlayer == null)
            return;
        
        // Проверяем что игрок на земле перед сохранением
        PlayerController playerController = spawnedPlayer.GetComponent<PlayerController>();
        if (playerController != null && !playerController.IsGrounded())
        {
            
            return;
        }
        
        Vector3 currentPosition = spawnedPlayer.transform.position;
        
        var data = playerPositionSave.Data;
        data.SetPosition(currentPosition);
        data.SetRotation(spawnedPlayer.transform.rotation);
        playerPositionSave.Save();
        
        //
    }
    
    /// <summary>
    /// Найти безопасную позицию рядом с указанной точкой
    /// </summary>
    Vector3 FindSafePositionNearby(Vector3 center, float searchRadius, int maxAttempts)
    {
        
        
        // Сначала пробуем поискать на той же высоте в горизонтальной плоскости
        for (int attempt = 0; attempt < maxAttempts / 2; attempt++)
        {
            // Случайная позиция в радиусе
            Vector2 randomOffset = Random.insideUnitCircle * searchRadius;
            Vector3 testPosition = center + new Vector3(randomOffset.x, 0, randomOffset.y);
            
            // Корректируем Y координату - ищем землю
            Vector3 safePosition = FindSafeYForPosition(testPosition);
            
            if (safePosition != Vector3.zero && IsPositionSafe(safePosition))
            {
                float distance = Vector3.Distance(center, safePosition);
                
                return safePosition;
            }
        }
        
        // Если не нашли - пробуем по спирали от центра
        for (int radius = 1; radius <= (int)searchRadius; radius++)
        {
            // Проверяем 8 направлений на каждом радиусе
            Vector3[] directions = new Vector3[]
            {
                new Vector3(radius, 0, 0),      // Восток
                new Vector3(-radius, 0, 0),     // Запад
                new Vector3(0, 0, radius),      // Север
                new Vector3(0, 0, -radius),     // Юг
                new Vector3(radius, 0, radius),    // Северо-восток
                new Vector3(-radius, 0, radius),   // Северо-запад
                new Vector3(radius, 0, -radius),   // Юго-восток
                new Vector3(-radius, 0, -radius)   // Юго-запад
            };
            
            foreach (Vector3 dir in directions)
            {
                Vector3 testPosition = center + dir;
                Vector3 safePosition = FindSafeYForPosition(testPosition);
                
                if (safePosition != Vector3.zero && IsPositionSafe(safePosition))
                {
                    float distance = Vector3.Distance(center, safePosition);
                    
                    return safePosition;
                }
            }
        }
        
        
        return Vector3.zero;
    }
    
    /// <summary>
    /// Найти безопасную Y координату для заданной XZ позиции
    /// </summary>
    Vector3 FindSafeYForPosition(Vector3 position)
    {
        if (voxelWorld == null) return Vector3.zero;
        
        int blockX = Mathf.FloorToInt(position.x);
        int blockZ = Mathf.FloorToInt(position.z);
        int startY = Mathf.FloorToInt(position.y);
        
        // Проверяем границы XZ
        int worldWidth = voxelWorld.chunksX * VoxelChunk16.WIDTH;
        int worldDepth = voxelWorld.chunksZ * VoxelChunk16.DEPTH;
        
        if (blockX < 0 || blockX >= worldWidth || blockZ < 0 || blockZ >= worldDepth)
            return Vector3.zero;
        
        // Ищем вниз от стартовой высоты (до 20 блоков)
        for (int y = startY; y >= Mathf.Max(0, startY - 20); y--)
        {
            // Проверяем что есть земля и свободно сверху
            if (voxelWorld.HasBlockAt(blockX, y, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 1, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 2, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 3, blockZ))
            {
                // Позиция на 1.5 блока выше земли
                return new Vector3(blockX + 0.5f, y + 1.5f, blockZ + 0.5f);
            }
        }
        
        // Ищем вверх от стартовой высоты (до 20 блоков)
        for (int y = startY + 1; y <= Mathf.Min(VoxelChunk16.HEIGHT - 3, startY + 20); y++)
        {
            if (voxelWorld.HasBlockAt(blockX, y, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 1, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 2, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 3, blockZ))
            {
                return new Vector3(blockX + 0.5f, y + 1.5f, blockZ + 0.5f);
            }
        }
        
        return Vector3.zero;
    }
    
    /// <summary>
    /// Проверить, безопасна ли позиция для спавна
    /// </summary>
    bool IsPositionSafe(Vector3 position)
    {
        if (voxelWorld == null)
        {
            
            return false;
        }
        
        // Проверяем границы мира
        if (position.x < 0 || position.z < 0 || position.y < 0 || position.y >= VoxelChunk16.HEIGHT)
        {
            
            return false;
        }
        
        int worldWidth = voxelWorld.chunksX * VoxelChunk16.WIDTH;
        int worldDepth = voxelWorld.chunksZ * VoxelChunk16.DEPTH;
        
        if (position.x >= worldWidth || position.z >= worldDepth)
        {
            
            return false;
        }
        
        // Проверяем, что под ногами есть блок
        // Игрок стоит на высоте Y, под ногами должен быть блок на Y-1 или Y-2
        int blockX = Mathf.FloorToInt(position.x);
        int blockZ = Mathf.FloorToInt(position.z);
        
        // Проверяем блок прямо под позицией игрока
        int blockYDirect = Mathf.FloorToInt(position.y);
        // Проверяем блок на 1 ниже
        int blockYBelow = Mathf.FloorToInt(position.y - 1f);
        
        
        
        // Проверяем есть ли земля под ногами (на -1 или -2 блока)
        bool hasGroundDirect = voxelWorld.HasBlockAt(blockX, blockYDirect - 1, blockZ);
        bool hasGroundBelow = voxelWorld.HasBlockAt(blockX, blockYBelow, blockZ);
        
        
        
        if (!hasGroundDirect && !hasGroundBelow)
        {
            
            return false;
        }
        
        // Проверяем, что на уровне игрока и выше свободно (минимум 2 блока)
        for (int checkY = blockYDirect; checkY <= blockYDirect + 2; checkY++)
        {
            if (checkY >= VoxelChunk16.HEIGHT)
                break;
            
            bool hasBlock = voxelWorld.HasBlockAt(blockX, checkY, blockZ);
            
            
            if (hasBlock)
            {
                
                return false; // Есть блок на уровне игрока или выше
            }
        }
        
        
        return true;
    }
    
    /// <summary>
    /// Удалить сохранение позиции игрока
    /// </summary>
    [ContextMenu("Delete Player Position Save")]
    public void DeletePlayerPositionSave()
    {
        if (playerPositionSave != null)
        {
            playerPositionSave.Delete();
            
        }
    }
    
    /// <summary>
    /// Отладочная информация
    /// </summary>
    void OnGUI()
    {
        if (!isInitialized)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            
            string message = "Загрузка мира...";
            if (voxelWorld != null)
            {
                if (voxelWorld.IsWorldReady)
                    message = "Спавн игрока...";
                else
                    message = "Генерация мира...";
            }
            
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100), message, style);
        }
    }
}


