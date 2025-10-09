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
        Debug.Log("=== GameBootstrap: Начало инициализации игры ===");
        
        // Шаг 1: Найти VoxelWorld если не указан
        if (voxelWorld == null)
        {
            Debug.Log("GameBootstrap: Поиск VoxelWorld...");
            voxelWorld = FindObjectOfType<VoxelWorld>();
            
            if (voxelWorld == null)
            {
                Debug.LogError("GameBootstrap: VoxelWorld не найден в сцене!");
                yield break;
            }
            
            Debug.Log($"GameBootstrap: VoxelWorld найден: {voxelWorld.name}");
        }
        
        // Шаг 2: Ждать готовности мира
        if (waitForWorldReady)
        {
            Debug.Log("GameBootstrap: Ожидание готовности мира...");
            
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
        Debug.Log("=== GameBootstrap: Инициализация завершена успешно ===");
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
        
        Debug.Log("[Player] Спавн игрока...");
        
        // Определяем позицию спавна
        Vector3 spawnPosition;
        Quaternion spawnRotation = Quaternion.identity;
        
        // Проверяем наличие сохраненной позиции
        if (savePlayerPosition && playerPositionSave != null && playerPositionSave.Exists())
        {
            var savedPosition = playerPositionSave.Load();
            spawnPosition = savedPosition.GetPosition();
            spawnRotation = savedPosition.GetRotation();
            
            Debug.Log($"[Player] Загружена позиция из файла: {spawnPosition}, Rotation: {spawnRotation.eulerAngles}");
            
            // Проверяем, что сохраненная позиция безопасна
            if (!IsPositionSafe(spawnPosition))
            {
                Debug.LogWarning($"[Player] Сохраненная позиция небезопасна (Y={spawnPosition.y:F2}), используем стандартный спавн");
                spawnPosition = voxelWorld.GetSafeSpawnPosition();
                spawnRotation = Quaternion.identity;
                Debug.Log($"[Player] Новая безопасная позиция: {spawnPosition}");
            }
            else
            {
                Debug.Log($"[Player] Сохраненная позиция безопасна, используем ее");
            }
        }
        else
        {
            // Получаем безопасную позицию спавна из VoxelWorld
            spawnPosition = voxelWorld.GetSafeSpawnPosition();
            Debug.Log($"[Player] Сохранение не найдено, используем безопасную позицию: {spawnPosition}");
        }
        
        // Создаем игрока
        spawnedPlayer = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        spawnedPlayer.name = "Player"; // Убираем (Clone) из имени
        
        Debug.Log($"[Player] Игрок создан: {spawnedPlayer.name} в позиции {spawnPosition}");
        
        // Если нужно, отключаем игрока пока мир не готов
        if (disablePlayerUntilReady && !voxelWorld.IsWorldReady)
        {
            var controller = spawnedPlayer.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                Debug.Log("GameBootstrap: CharacterController временно отключен");
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
                Debug.Log("GameBootstrap: CharacterController включен");
            }
        }
        
        Debug.Log("[Player] Игрок готов к игре!");
    }
    
    /// <summary>
    /// Инициализация камеры
    /// </summary>
    IEnumerator InitializeCamera()
    {
        Debug.Log("GameBootstrap: Инициализация камеры...");
        
        // Найти CameraController если не указан
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            
            if (cameraController == null)
            {
                Debug.LogWarning("GameBootstrap: CameraController не найден в сцене. Пропускаем инициализацию камеры.");
                yield break;
            }
            
            Debug.Log($"GameBootstrap: CameraController найден: {cameraController.name}");
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
            Debug.Log("GameBootstrap: CameraPivot создан для игрока");
        }
        
        // Сбрасываем камеру для инициализации позиции
        cameraController.ResetCamera();
        
        Debug.Log("GameBootstrap: Камера инициализирована успешно");
        
        yield return null;
    }
    
    /// <summary>
    /// Инициализация спавнеров врагов
    /// </summary>
    void InitializeEnemySpawners()
    {
        Debug.Log("GameBootstrap: Инициализация спавнеров врагов...");
        
        // Автопоиск спавнеров если массив пуст
        if ((enemySpawners == null || enemySpawners.Length == 0) && autoFindSpawners)
        {
            enemySpawners = FindObjectsOfType<EnemySpawner>();
            
            if (enemySpawners.Length > 0)
            {
                Debug.Log($"GameBootstrap: Найдено {enemySpawners.Length} спавнеров в сцене");
            }
        }
        
        if (enemySpawners == null || enemySpawners.Length == 0)
        {
            Debug.LogWarning("GameBootstrap: Спавнеры врагов не найдены");
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
        
        Debug.Log($"GameBootstrap: Инициализировано {initializedCount} спавнеров врагов");
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
            Debug.LogWarning("GameBootstrap: Игрок не создан, невозможно переспавнить");
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
        
        Debug.Log($"[Player] Игрок переспавнен в позиции {spawnPosition}");
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
            Debug.LogWarning($"[Player] Игрок не на земле (Y={spawnedPlayer.transform.position.y:F2}), пропускаем сохранение");
            return;
        }
        
        Vector3 currentPosition = spawnedPlayer.transform.position;
        
        var data = playerPositionSave.Data;
        data.SetPosition(currentPosition);
        data.SetRotation(spawnedPlayer.transform.rotation);
        playerPositionSave.Save();
        
        Debug.Log($"[Player] Позиция игрока сохранена в файл: {currentPosition} (IsGrounded: {playerController?.IsGrounded()})");
    }
    
    /// <summary>
    /// Проверить, безопасна ли позиция для спавна
    /// </summary>
    bool IsPositionSafe(Vector3 position)
    {
        if (voxelWorld == null)
        {
            Debug.LogWarning($"[Player Safety] VoxelWorld == null");
            return false;
        }
        
        // Проверяем границы мира
        if (position.x < 0 || position.z < 0 || position.y < 0 || position.y >= VoxelChunk16.HEIGHT)
        {
            Debug.LogWarning($"[Player Safety] Позиция {position} вне границ мира");
            return false;
        }
        
        int worldWidth = voxelWorld.chunksX * VoxelChunk16.WIDTH;
        int worldDepth = voxelWorld.chunksZ * VoxelChunk16.DEPTH;
        
        if (position.x >= worldWidth || position.z >= worldDepth)
        {
            Debug.LogWarning($"[Player Safety] Позиция {position} вне границ мира (ширина={worldWidth}, глубина={worldDepth})");
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
        
        Debug.Log($"[Player Safety] Проверка позиции {position}: blockX={blockX}, blockZ={blockZ}, blockYDirect={blockYDirect}, blockYBelow={blockYBelow}");
        
        // Проверяем есть ли земля под ногами (на -1 или -2 блока)
        bool hasGroundDirect = voxelWorld.HasBlockAt(blockX, blockYDirect - 1, blockZ);
        bool hasGroundBelow = voxelWorld.HasBlockAt(blockX, blockYBelow, blockZ);
        
        Debug.Log($"[Player Safety] Земля под ногами: blockY={blockYDirect-1} → {hasGroundDirect}, blockY={blockYBelow} → {hasGroundBelow}");
        
        if (!hasGroundDirect && !hasGroundBelow)
        {
            Debug.LogWarning($"[Player Safety] Нет земли под ногами на Y={blockYDirect-1} или Y={blockYBelow}");
            return false;
        }
        
        // Проверяем, что на уровне игрока и выше свободно (минимум 2 блока)
        for (int checkY = blockYDirect; checkY <= blockYDirect + 2; checkY++)
        {
            if (checkY >= VoxelChunk16.HEIGHT)
                break;
            
            bool hasBlock = voxelWorld.HasBlockAt(blockX, checkY, blockZ);
            Debug.Log($"[Player Safety] Проверка свободного пространства Y={checkY}: hasBlock={hasBlock}");
            
            if (hasBlock)
            {
                Debug.LogWarning($"[Player Safety] Есть блок на уровне игрока Y={checkY}");
                return false; // Есть блок на уровне игрока или выше
            }
        }
        
        Debug.Log($"[Player Safety] Позиция {position} БЕЗОПАСНА ✓");
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
            Debug.Log("[Player] Сохранение позиции игрока удалено");
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

