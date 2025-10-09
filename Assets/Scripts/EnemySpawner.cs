using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Система спавна врагов с различными режимами
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefab")]
    [Tooltip("Префаб врага для спавна")]
    public GameObject enemyPrefab;
    
    [Header("Spawn Mode")]
    [Tooltip("Режим спавна врагов")]
    public SpawnMode spawnMode = SpawnMode.OnStart;
    
    [Header("Spawn Count")]
    [Tooltip("Количество врагов для спавна за раз")]
    public int enemiesPerSpawn = 3;
    
    [Tooltip("Максимальное количество врагов одновременно")]
    public int maxEnemiesAlive = 10;
    
    [Header("Spawn Area")]
    [Tooltip("Режим определения позиции спавна")]
    public SpawnAreaMode areaMode = SpawnAreaMode.RandomInRadius;
    
    [Tooltip("Радиус спавна вокруг спавнера")]
    public float spawnRadius = 20f;
    
    [Tooltip("Список конкретных точек спавна (если используется SpawnPoints режим)")]
    public Transform[] spawnPoints;
    
    [Header("Timing")]
    [Tooltip("Интервал между волнами спавна (секунды)")]
    public float spawnInterval = 30f;
    
    [Tooltip("Задержка перед первым спавном (секунды)")]
    public float initialDelay = 3f;
    
    [Header("Safe Spawn Check")]
    [Tooltip("Проверять безопасность позиции спавна через VoxelWorld")]
    public bool checkSafeSpawn = true;
    
    [Tooltip("Минимальная высота для спавна")]
    public int minSpawnHeight = 10;
    
    [Tooltip("Максимальная высота для спавна")]
    public int maxSpawnHeight = 100;
    
    [Tooltip("Минимальная дистанция от игрока для спавна")]
    public float minDistanceFromPlayer = 10f;
    
    [Tooltip("Максимальное количество попыток найти позицию")]
    public int maxSpawnAttempts = 50;
    
    [Header("Wave System")]
    [Tooltip("Увеличивать количество врагов с каждой волной")]
    public bool increasePerWave = false;
    
    [Tooltip("Количество врагов для добавления каждую волну")]
    public int enemiesIncreasePerWave = 1;
    
    [Tooltip("Максимальное количество врагов в волне")]
    public int maxEnemiesPerWave = 20;
    
    // Режимы спавна
    public enum SpawnMode
    {
        OnStart,        // Спавн при старте
        Continuous,     // Постоянный спавн с интервалом
        Wave,           // Волнами (после убийства всех)
        Manual          // Только по вызову SpawnWave()
    }
    
    public enum SpawnAreaMode
    {
        RandomInRadius,     // Случайно в радиусе
        SpawnPoints,        // На конкретных точках
        AroundTarget        // Вокруг цели (игрока)
    }
    
    // Приватные переменные
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private VoxelWorld voxelWorld;
    private Transform targetPlayer;
    private int currentWave = 0;
    private int currentEnemiesPerWave;
    private Coroutine spawnCoroutine;
    private bool isInitialized = false;
    
    /// <summary>
    /// Публичная инициализация спавнера (вызывается из GameBootstrap)
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
        {
            Debug.LogWarning($"EnemySpawner [{name}]: Уже инициализирован!");
            return;
        }
        
        Debug.Log($"EnemySpawner [{name}]: Инициализация...");
        
        // Поиск VoxelWorld для проверки безопасности
        if (checkSafeSpawn)
        {
            voxelWorld = VoxelWorld.Instance;
            if (voxelWorld == null)
            {
                Debug.LogWarning($"EnemySpawner [{name}]: VoxelWorld не найден, проверка безопасности отключена");
                checkSafeSpawn = false;
            }
        }
        
        // Поиск игрока - если не установлен через GameBootstrap
        if (targetPlayer == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                targetPlayer = player.transform;
                Debug.Log($"EnemySpawner [{name}]: Игрок найден автоматически: {targetPlayer.name}");
            }
            else
            {
                Debug.LogWarning($"EnemySpawner [{name}]: PlayerController не найден в сцене!");
                if (areaMode == SpawnAreaMode.AroundTarget)
                {
                    Debug.LogWarning($"EnemySpawner [{name}]: Игрок не найден, используется режим RandomInRadius");
                    areaMode = SpawnAreaMode.RandomInRadius;
                }
            }
        }
        
        currentEnemiesPerWave = enemiesPerSpawn;
        
        // Проверяем режим SpawnPoints - если точки не установлены, переключаемся на RandomInRadius
        if (areaMode == SpawnAreaMode.SpawnPoints && (spawnPoints == null || spawnPoints.Length == 0))
        {
            Debug.LogWarning($"EnemySpawner [{name}]: Режим SpawnPoints, но точки не установлены! Переключаемся на RandomInRadius");
            areaMode = SpawnAreaMode.RandomInRadius;
        }
        
        // Запуск спавна в зависимости от режима
        switch (spawnMode)
        {
            case SpawnMode.OnStart:
                StartCoroutine(SpawnWithDelay(initialDelay));
                break;
                
            case SpawnMode.Continuous:
                spawnCoroutine = StartCoroutine(ContinuousSpawnCoroutine());
                break;
                
            case SpawnMode.Wave:
                spawnCoroutine = StartCoroutine(WaveSpawnCoroutine());
                break;
                
            case SpawnMode.Manual:
                Debug.Log($"EnemySpawner [{name}]: Ручной режим. Вызовите SpawnWave() для спавна.");
                break;
        }
        
        isInitialized = true;
        Debug.Log($"EnemySpawner [{name}]: Инициализация завершена, режим: {spawnMode}");
    }
    
    void Update()
    {
        // Очищаем список от уничтоженных врагов
        spawnedEnemies.RemoveAll(enemy => enemy == null);
    }
    
    // ========== КОРУТИНЫ СПАВНА ==========
    
    IEnumerator SpawnWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnWave();
    }
    
    IEnumerator ContinuousSpawnCoroutine()
    {
        yield return new WaitForSeconds(initialDelay);
        
        while (true)
        {
            // Спавним только если не превышен лимит
            if (spawnedEnemies.Count < maxEnemiesAlive)
            {
                int toSpawn = Mathf.Min(currentEnemiesPerWave, maxEnemiesAlive - spawnedEnemies.Count);
                SpawnEnemies(toSpawn);
            }
            
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    
    IEnumerator WaveSpawnCoroutine()
    {
        yield return new WaitForSeconds(initialDelay);
        
        while (true)
        {
            // Спавним волну
            SpawnWave();
            
            // Ждем пока все враги не будут убиты
            while (spawnedEnemies.Count > 0)
            {
                yield return new WaitForSeconds(1f);
            }
            
            // Пауза между волнами
            yield return new WaitForSeconds(spawnInterval);
            
            // Увеличиваем сложность
            if (increasePerWave)
            {
                currentEnemiesPerWave = Mathf.Min(
                    currentEnemiesPerWave + enemiesIncreasePerWave, 
                    maxEnemiesPerWave
                );
            }
            
            currentWave++;
            Debug.Log($"EnemySpawner [{name}]: Волна {currentWave} начинается! Врагов: {currentEnemiesPerWave}");
        }
    }
    
    // ========== МЕТОДЫ СПАВНА ==========
    
    /// <summary>
    /// Спавнить волну врагов
    /// </summary>
    public void SpawnWave()
    {
        int toSpawn = Mathf.Min(currentEnemiesPerWave, maxEnemiesAlive - spawnedEnemies.Count);
        SpawnEnemies(toSpawn);
    }
    
    /// <summary>
    /// Спавнить конкретное количество врагов
    /// </summary>
    public void SpawnEnemies(int count)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError($"EnemySpawner [{name}]: Enemy Prefab не указан!");
            return;
        }
        
        int spawned = 0;
        int attempts = 0;
        
        while (spawned < count && attempts < maxSpawnAttempts)
        {
            attempts++;
            
            Vector3 spawnPosition = GetSpawnPosition();
            
            // Проверка что позиция не Vector3.zero (признак неудачи)
            if (spawnPosition == Vector3.zero)
            {
                continue; // Пробуем другую позицию
            }
            
            // Проверка безопасности позиции
            if (checkSafeSpawn && !IsSafeSpawnPosition(spawnPosition))
            {
                continue; // Пробуем другую позицию
            }
            
            // Проверка дистанции до игрока
            if (targetPlayer != null)
            {
                float distanceToPlayer = Vector3.Distance(spawnPosition, targetPlayer.position);
                if (distanceToPlayer < minDistanceFromPlayer)
                {
                    continue; // Слишком близко к игроку
                }
            }
            
            // Спавним врага
            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            enemy.name = $"Enemy_{currentWave}_{spawned + 1}";
            
            Debug.Log($"[EnemySpawner] Заспавнен враг '{enemy.name}' на позиции {spawnPosition}");
            
            // Инициализируем EnemyBot если есть
            EnemyBot bot = enemy.GetComponent<EnemyBot>();
            if (bot != null && targetPlayer != null)
            {
                bot.Init(targetPlayer);
            }
            else if (bot != null && targetPlayer == null)
            {
                Debug.LogWarning($"EnemySpawner [{name}]: targetPlayer is null, cannot initialize bot {bot.name}");
            }
            
            spawnedEnemies.Add(enemy);
            spawned++;
        }
        
        if (spawned < count)
        {
            Debug.LogWarning($"EnemySpawner [{name}]: Не удалось заспавнить всех врагов. Заспавнено: {spawned}/{count}, попыток: {attempts}");
        }
        else
        {
            Debug.Log($"EnemySpawner [{name}]: Заспавнено {spawned} врагов за {attempts} попыток");
        }
    }
    
    /// <summary>
    /// Получить позицию для спавна с умным поиском
    /// </summary>
    Vector3 GetSpawnPosition()
    {
        Vector3 basePosition = transform.position;
        
        // Если спавнер в позиции (0, 0, 0) и есть игрок - используем позицию игрока как базу
        if (basePosition == Vector3.zero && targetPlayer != null)
        {
            basePosition = targetPlayer.position;
            Debug.LogWarning($"[EnemySpawner] Спавнер находится в (0, 0, 0)! Используем позицию игрока как базу: {basePosition}");
        }
        
        Debug.Log($"[EnemySpawner] GetSpawnPosition: Базовая позиция спавнера = {basePosition}, режим = {areaMode}");
        
        switch (areaMode)
        {
            case SpawnAreaMode.RandomInRadius:
                // Случайная XZ позиция в радиусе
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                basePosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                
                Debug.Log($"[EnemySpawner] RandomInRadius: Случайная позиция = {basePosition}, радиус = {spawnRadius}");
                
                // Если включена проверка безопасности и есть VoxelWorld
                if (checkSafeSpawn && voxelWorld != null)
                {
                    // Ищем безопасную Y координату, поднимаясь вверх
                    Vector3 safePosition = FindSafeYPosition(basePosition);
                    
                    // Если не нашли (вернулся Vector3.zero) - возвращаем как признак неудачи
                    if (safePosition == Vector3.zero)
                    {
                        return Vector3.zero; // Попробуем другую XZ позицию
                    }
                    
                    return safePosition;
                }
                else
                {
                    return basePosition;
                }
                
            case SpawnAreaMode.SpawnPoints:
                // Случайная точка из списка
                if (spawnPoints != null && spawnPoints.Length > 0)
                {
                    Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    if (spawnPoint != null)
                    {
                        basePosition = spawnPoint.position;
                        Debug.Log($"[EnemySpawner] SpawnPoints: Выбрана точка спавна = {basePosition}");
                    }
                    else
                    {
                        Debug.LogWarning($"[EnemySpawner] SpawnPoints: Точка спавна == null, используем случайную позицию");
                        // Fallback на случайную позицию
                        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                        basePosition = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                    }
                }
                else
                {
                    Debug.LogWarning($"[EnemySpawner] SpawnPoints: Точки спавна не установлены! Используем случайную позицию в радиусе {spawnRadius}");
                    // Fallback на случайную позицию
                    Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                    basePosition = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                }
                
                // Проверяем безопасность для Spawn Points
                if (checkSafeSpawn && voxelWorld != null)
                {
                    Vector3 safePosition = FindSafeYPosition(basePosition);
                    return safePosition != Vector3.zero ? safePosition : basePosition;
                }
                return basePosition;
                
            case SpawnAreaMode.AroundTarget:
                // Вокруг цели (игрока)
                if (targetPlayer != null)
                {
                    Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                    basePosition = targetPlayer.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                    
                    // Проверяем безопасность
                    if (checkSafeSpawn && voxelWorld != null)
                    {
                        Vector3 safePosition = FindSafeYPosition(basePosition);
                        return safePosition != Vector3.zero ? safePosition : Vector3.zero;
                    }
                }
                return basePosition;
        }
        
        return basePosition;
    }
    
    /// <summary>
    /// Найти безопасную Y координату - ищем снизу вверх пока не найдем
    /// </summary>
    Vector3 FindSafeYPosition(Vector3 position)
    {
        int blockX = Mathf.FloorToInt(position.x);
        int blockZ = Mathf.FloorToInt(position.z);
        
        Debug.Log($"[EnemySpawner] FindSafeYPosition: Ищем безопасную высоту для ({blockX}, ?, {blockZ}), диапазон Y: [{minSpawnHeight}, {maxSpawnHeight}]");
        
        // Новая логика: ищем снизу ВВЕРХ пока не найдем безопасное место
        for (int y = minSpawnHeight; y <= maxSpawnHeight; y++)
        {
            // Проверяем что есть твердый блок под ногами и свободно сверху (2 блока)
            if (voxelWorld.HasBlockAt(blockX, y, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 1, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 2, blockZ) &&
                !voxelWorld.HasBlockAt(blockX, y + 3, blockZ))
            {
                Vector3 safePos = new Vector3(position.x, y + 1.5f, position.z);
                
                // Проверяем дистанцию до игрока
                if (targetPlayer != null)
                {
                    float distanceToPlayer = Vector3.Distance(safePos, targetPlayer.position);
                    if (distanceToPlayer < minDistanceFromPlayer)
                    {
                        Debug.Log($"[EnemySpawner] Позиция ({blockX}, {y}, {blockZ}) слишком близко к игроку ({distanceToPlayer:F1}m < {minDistanceFromPlayer}m), ищем дальше");
                        continue; // Слишком близко к игроку, ищем выше
                    }
                }
                
                Debug.Log($"[EnemySpawner] Найдена безопасная позиция: {safePos}");
                return safePos;
            }
        }
        
        // Если дошли до верха и не нашли - возвращаем Vector3.zero как признак неудачи
        Debug.LogWarning($"[EnemySpawner] Не найдена безопасная позиция для ({blockX}, ?, {blockZ}) в диапазоне Y: [{minSpawnHeight}, {maxSpawnHeight}]");
        return Vector3.zero;
    }
    
    /// <summary>
    /// Проверить безопасность позиции для спавна
    /// </summary>
    bool IsSafeSpawnPosition(Vector3 position)
    {
        if (!checkSafeSpawn || voxelWorld == null)
            return true;
        
        int blockX = Mathf.FloorToInt(position.x);
        int blockY = Mathf.FloorToInt(position.y);
        int blockZ = Mathf.FloorToInt(position.z);
        
        // Проверка границ мира
        if (blockY < minSpawnHeight || blockY > maxSpawnHeight)
            return false;
        
        // Проверка что позиция свободна (2 блока высоты)
        if (voxelWorld.HasBlockAt(blockX, blockY, blockZ) ||
            voxelWorld.HasBlockAt(blockX, blockY + 1, blockZ))
            return false;
        
        // Проверка что под ногами есть блок
        if (!voxelWorld.HasBlockAt(blockX, blockY - 1, blockZ))
            return false;
        
        return true;
    }
    
    // ========== УПРАВЛЕНИЕ ==========
    
    /// <summary>
    /// Установить цель (игрока) для врагов
    /// </summary>
    public void SetPlayerTarget(Transform player)
    {
        targetPlayer = player;
        
        // Обновляем цель для уже заспавненных врагов
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                EnemyBot bot = enemy.GetComponent<EnemyBot>();
                if (bot != null)
                {
                    bot.SetTarget(player);
                }
            }
        }
        
        Debug.Log($"EnemySpawner [{name}]: Установлена цель для врагов: {player.name}");
    }
    
    /// <summary>
    /// Остановить автоматический спавн
    /// </summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        Debug.Log($"EnemySpawner [{name}]: Спавн остановлен");
    }
    
    /// <summary>
    /// Возобновить автоматический спавн
    /// </summary>
    public void ResumeSpawning()
    {
        if (spawnCoroutine != null) return;
        
        switch (spawnMode)
        {
            case SpawnMode.Continuous:
                spawnCoroutine = StartCoroutine(ContinuousSpawnCoroutine());
                break;
                
            case SpawnMode.Wave:
                spawnCoroutine = StartCoroutine(WaveSpawnCoroutine());
                break;
        }
        
        Debug.Log($"EnemySpawner [{name}]: Спавн возобновлен");
    }
    
    /// <summary>
    /// Убить всех заспавненных врагов
    /// </summary>
    public void KillAllEnemies()
    {
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        spawnedEnemies.Clear();
        Debug.Log($"EnemySpawner [{name}]: Все враги уничтожены");
    }
    
    /// <summary>
    /// Получить количество живых врагов
    /// </summary>
    public int GetAliveEnemiesCount()
    {
        return spawnedEnemies.Count;
    }
    
    /// <summary>
    /// Получить текущую волну
    /// </summary>
    public int GetCurrentWave()
    {
        return currentWave;
    }
    
    // ========== ВИЗУАЛИЗАЦИЯ ==========
    
    void OnDrawGizmosSelected()
    {
        // Радиус спавна
        Gizmos.color = Color.yellow;
        
        if (areaMode == SpawnAreaMode.AroundTarget && Application.isPlaying && targetPlayer != null)
        {
            Gizmos.DrawWireSphere(targetPlayer.position, spawnRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
        
        // Точки спавна
        if (areaMode == SpawnAreaMode.SpawnPoints && spawnPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 1f);
                    Gizmos.DrawLine(transform.position, point.position);
                }
            }
        }
        
        // Позиция спавнера
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
    }
}

