using UnityEngine;
using System.Collections.Generic;

public class AutoSpawnService
{
    public static AutoSpawnService Instance { get; private set; }
    
    // Auto Spawn Settings
    private float saveInterval = 5f; // Интервал сохранения позиции в секундах
    
    // Данные для каждой сущности
    private class SpawnableData
    {
        public ISpawnable spawnable;
        public Vector3 lastSavedPosition;
        public Vector3 startPosition;
        public float lastSaveTime;
        public bool hasValidSavePosition;
    }
    
    private Dictionary<string, SpawnableData> spawnables = new Dictionary<string, SpawnableData>();
    private VoxelWorld voxelWorld;
    private bool isWaitingForVoxelWorld = false;
    private float lastVoxelWorldCheckTime = 0f;
    private float voxelWorldCheckInterval = 0.5f; // Проверяем каждые 0.5 секунды
    private bool hasMovedPlayerToSpawn = false; // Флаг перемещения игрока на точку спавна
    
    public AutoSpawnService()
    {
        if (Instance != null)
        {
            
        }
        Instance = this;
    }
    
    private static VoxelWorld FindVoxelWorldInScene()
    {
        // Ищем VoxelWorld среди всех объектов в сцене
        VoxelWorld[] voxelWorlds = Object.FindObjectsOfType<VoxelWorld>();
        if (voxelWorlds.Length > 0)
        {
            return voxelWorlds[0]; // Возвращаем первый найденный
        }
        return null;
    }
    
    public void Initialize(PlayerController playerController)
    {
        // Пытаемся найти VoxelWorld
        TryFindVoxelWorld();
        
        // Регистрируем игрока через новый API
        RegisterSpawnable(playerController);
        
        
    }
    
    /// <summary>
    /// Регистрация сущности в системе автоспавна
    /// </summary>
    public void RegisterSpawnable(ISpawnable spawnable)
    {
        string id = spawnable.GetSpawnableID();
        
        if (spawnables.ContainsKey(id))
        {
            
            return;
        }
        
        Vector3 currentPosition = spawnable.GetTransform().position;
        Vector3 startPosition = currentPosition;
        
        // Если регистрируем игрока и VoxelWorld готов, используем точку спавна из мира
        if (id == "Player" && voxelWorld != null && voxelWorld.IsWorldReady)
        {
            Vector3 worldSpawnPoint = voxelWorld.PlayerSpawnPoint;
            if (worldSpawnPoint != Vector3.zero)
            {
                startPosition = worldSpawnPoint;
                Debug.Log($"[AutoSpawn] Игрок зарегистрирован с точкой спавна из мира: {worldSpawnPoint}");
            }
            else
            {
                Debug.LogWarning($"[AutoSpawn] Точка спавна из мира не установлена, используем текущую позицию: {currentPosition}");
            }
        }
        
        var data = new SpawnableData
        {
            spawnable = spawnable,
            startPosition = startPosition,
            lastSavedPosition = startPosition,
            hasValidSavePosition = true,
            lastSaveTime = Time.time
        };
        
        spawnables[id] = data;
        
        string prefix = id == "Player" ? "[Player]" : "[Bot]";
        //
    }
    
    /// <summary>
    /// Отменить регистрацию сущности
    /// </summary>
    public void UnregisterSpawnable(ISpawnable spawnable)
    {
        string id = spawnable.GetSpawnableID();
        
        if (spawnables.Remove(id))
        {
            
        }
    }
    
    private void TryFindVoxelWorld()
    {
        voxelWorld = FindVoxelWorldInScene();
        
        if (voxelWorld == null)
        {
            
            isWaitingForVoxelWorld = true;
        }
        else
        {
            
            isWaitingForVoxelWorld = false;
        }
    }
    
    public void Tick(float deltaTime)
    {
        // Старый API - для совместимости с PlayerController
        // Теперь не используется, вместо него используется TickSpawnable
        
        // Если ждем VoxelWorld, проверяем его появление с интервалом
        if (isWaitingForVoxelWorld)
        {
            float currentTime = Time.time;
            if (currentTime - lastVoxelWorldCheckTime >= voxelWorldCheckInterval)
            {
                TryFindVoxelWorld();
                lastVoxelWorldCheckTime = currentTime;
            }
        }
    }
    
    /// <summary>
    /// Обновление для конкретной сущности
    /// </summary>
    public void TickSpawnable(ISpawnable spawnable, float deltaTime)
    {
        // Проверяем что объект не уничтожен
        if (spawnable == null || spawnable.GetGameObject() == null || spawnable.GetTransform() == null)
        {
            return; // Объект уничтожен, пропускаем
        }
        
        string id = spawnable.GetSpawnableID();
        
        if (!spawnables.TryGetValue(id, out var data))
        {
            
            return;
        }
        
        // Если ждем VoxelWorld, проверяем его появление с интервалом
        if (isWaitingForVoxelWorld)
        {
            float currentTime = Time.time;
            if (currentTime - lastVoxelWorldCheckTime >= voxelWorldCheckInterval)
            {
                TryFindVoxelWorld();
                lastVoxelWorldCheckTime = currentTime;
            }
        }
        
        // Если это игрок и мир готов, но мы еще не переместили игрока на точку спавна
        if (id == "Player" && !hasMovedPlayerToSpawn && voxelWorld != null && voxelWorld.IsWorldReady)
        {
            Vector3 worldSpawnPoint = voxelWorld.PlayerSpawnPoint;
            if (worldSpawnPoint != Vector3.zero)
            {
                // Отключаем CharacterController перед перемещением
                CharacterController controller = spawnable.GetGameObject().GetComponent<CharacterController>();
                bool wasControllerEnabled = false;
                if (controller != null)
                {
                    wasControllerEnabled = controller.enabled;
                    controller.enabled = false;
                }
                
                // Перемещаем игрока на точку спавна
                spawnable.GetTransform().position = worldSpawnPoint;
                
                // Включаем CharacterController обратно
                if (controller != null && wasControllerEnabled)
                {
                    controller.enabled = true;
                }
                
                // Обновляем данные спавна
                data.startPosition = worldSpawnPoint;
                data.lastSavedPosition = worldSpawnPoint;
                
                hasMovedPlayerToSpawn = true;
                Debug.Log($"[AutoSpawn] Игрок перемещен на точку спавна мира: {worldSpawnPoint}");
            }
        }
        
        // Проверяем, нужно ли сохранить позицию этой сущности
        CheckForPositionSave(data);
    }
    
    private void CheckForPositionSave(SpawnableData data)
    {
        // Проверяем что объект не уничтожен
        if (data.spawnable == null || data.spawnable.GetGameObject() == null)
        {
            return;
        }
        
        float currentTime = Time.time;
        
        // Если прошло достаточно времени с последнего сохранения
        if (currentTime - data.lastSaveTime >= saveInterval)
        {
            // Проверяем, находится ли сущность на земле
            if (data.spawnable.IsGrounded())
            {
                SavePosition(data);
            }
            // Если не на земле, ждем пока окажется на земле
        }
    }
    
    private void SavePosition(SpawnableData data)
    {
        // Дополнительная проверка перед доступом к Transform
        if (data.spawnable == null || data.spawnable.GetTransform() == null)
        {
            return;
        }
        
        Vector3 currentPosition = data.spawnable.GetTransform().position;
        data.lastSavedPosition = currentPosition;
        data.hasValidSavePosition = true;
        data.lastSaveTime = Time.time;
        
        string id = data.spawnable.GetSpawnableID();
        string prefix = id == "Player" ? "[Player]" : "[Bot]";
        
    }
    
    public void OnPlayerEnterDeadZone(PlayerController deadPlayer)
    {
        // Старый API для совместимости
        OnEnterDeadZone(deadPlayer);
    }
    
    /// <summary>
    /// Обработка попадания сущности в зону смерти
    /// </summary>
    public void OnEnterDeadZone(ISpawnable spawnable)
    {
        string id = spawnable.GetSpawnableID();
        
        if (!spawnables.TryGetValue(id, out var data))
        {
            
            return;
        }
        
        string prefix = id == "Player" ? "[Player]" : "[Bot]";
        
        Vector3 currentPos = spawnable.GetTransform().position;
        
        
        
        // Скрываем сущность
        spawnable.GetGameObject().SetActive(false);
        
        // Находим безопасную позицию для спавна
        Vector3 spawnPosition = FindSafeSpawnPosition(data);
        
        
        
        // Перемещаем сущность
        spawnable.GetTransform().position = spawnPosition;
        
        // Показываем сущность
        spawnable.GetGameObject().SetActive(true);
        
        
    }
    
    private Vector3 FindSafeSpawnPosition(SpawnableData data)
    {
        string id = data.spawnable.GetSpawnableID();
        string prefix = id == "Player" ? "[Player]" : "[Bot]";
        
        if (voxelWorld == null)
        {
            
            return data.startPosition;
        }
        
        // Сначала проверяем последнюю сохраненную позицию
        
        
        if (data.hasValidSavePosition)
        {
            bool isSafe = IsPositionSafe(data.lastSavedPosition);
            
            
            if (isSafe)
            {
                
                return data.lastSavedPosition;
            }
        }
        
        
        
        // Ищем точки ниже сохраненной позиции
        Vector3 safePosition = FindSafePositionBelow(data.lastSavedPosition);
        if (safePosition != Vector3.zero)
        {
            
            return safePosition;
        }
        
        // Если не найдено ниже, ищем по чанкам сверху вниз
        safePosition = FindSafePositionInChunks(data.lastSavedPosition);
        if (safePosition != Vector3.zero)
        {
            
            return safePosition;
        }
        
        // Если весь мир разрушен и это игрок - сбрасываем мир
        if (id == "Player")
        {
            
            ResetWorld();
        }
        
        
        return data.startPosition;
    }
    
    private bool IsPositionSafe(Vector3 position)
    {
        // ВАЖНО: Проверяем минимальную высоту (защита от зоны смерти)
        if (position.y < 10f)
        {
            return false;
        }
        
        if (voxelWorld == null) 
        {
            // Если VoxelWorld еще не найден, считаем позицию безопасной если она не слишком низко
            return position.y > 10f;
        }
        
        // Проверяем, что позиция в пределах мира (по горизонтали)
        int worldWidth = voxelWorld.chunksX * VoxelChunk16.WIDTH;
        int worldDepth = voxelWorld.chunksZ * VoxelChunk16.DEPTH;
        
        if (position.x < 0 || position.z < 0 || position.x >= worldWidth || position.z >= worldDepth)
        {
            
            return false;
        }
        
        // Проверяем, есть ли блок под ногами игрока
        Vector3 checkPosition = position + Vector3.down * 1.0f;
        
        // Проверяем несколько точек вокруг позиции игрока
        Vector3[] checkPoints = {
            checkPosition,
            checkPosition + Vector3.left * 0.4f,
            checkPosition + Vector3.right * 0.4f,
            checkPosition + Vector3.forward * 0.4f,
            checkPosition + Vector3.back * 0.4f
        };
        
        bool hasGround = false;
        foreach (Vector3 point in checkPoints)
        {
            if (HasSolidBlockAt(point))
            {
                hasGround = true;
                break;
            }
        }
        
        if (!hasGround)
        {
            
            return false;
        }
        
        // Проверяем, что над головой игрока нет блоков (игрок высотой 2.3)
        Vector3 headPosition = position + Vector3.up * 2.5f;
        if (HasSolidBlockAt(headPosition))
        {
            
            return false; // Есть блок над головой
        }
        
        
        return true;
    }
    
    private bool HasSolidBlockAt(Vector3 worldPosition)
    {
        if (voxelWorld == null) return false;
        
        // Конвертируем мировую позицию в координаты блока
        int blockX = Mathf.FloorToInt(worldPosition.x);
        int blockY = Mathf.FloorToInt(worldPosition.y);
        int blockZ = Mathf.FloorToInt(worldPosition.z);
        
        // Проверяем, что координаты в пределах мира
        if (blockX < 0 || blockZ < 0 || blockY < 0 || blockY >= VoxelChunk16.HEIGHT) 
            return false;
        if (blockX >= voxelWorld.chunksX * VoxelChunk16.WIDTH || blockZ >= voxelWorld.chunksZ * VoxelChunk16.DEPTH) 
            return false;
        
        // Проверяем через прямой доступ к данным чанка (более надежно)
        bool hasBlockInChunk = voxelWorld.HasBlockAt(blockX, blockY, blockZ);
        
        // Дополнительно проверяем через Raycast для коллайдеров
        if (!hasBlockInChunk)
        {
            RaycastHit hit;
            Vector3 rayStart = worldPosition + Vector3.up * 0.1f;
            return Physics.Raycast(rayStart, Vector3.down, out hit, 1.5f);
        }
        
        return hasBlockInChunk;
    }
    
    
    private Vector3 FindSafePositionBelow(Vector3 savedPosition)
    {
        
        
        // Проверяем все точки ниже сохраненной позиции (но не ниже 10 блоков - зона смерти)
        for (float y = savedPosition.y - 1f; y >= 10f; y -= 1f)
        {
            Vector3 testPosition = new Vector3(savedPosition.x, y, savedPosition.z);
            
            // Проверяем несколько точек вокруг для стабильности
            Vector3[] testPoints = {
                testPosition,
                testPosition + Vector3.left * 0.5f,
                testPosition + Vector3.right * 0.5f,
                testPosition + Vector3.forward * 0.5f,
                testPosition + Vector3.back * 0.5f
            };
            
            foreach (Vector3 point in testPoints)
            {
                if (IsPositionSafe(point))
                {
                    
                    return point;
                }
            }
        }
        
        
        return Vector3.zero;
    }
    
    private Vector3 FindSafePositionInChunks(Vector3 savedPosition)
    {
        
        
        // Начинаем с чанка, где была сохраненная позиция
        int startChunkX = Mathf.FloorToInt(savedPosition.x / VoxelChunk16.WIDTH);
        int startChunkZ = Mathf.FloorToInt(savedPosition.z / VoxelChunk16.DEPTH);
        
        // Проверяем чанки по спирали от стартового
        for (int radius = 0; radius < Mathf.Max(voxelWorld.chunksX, voxelWorld.chunksZ); radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    // Проверяем только границу текущего радиуса
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius && radius > 0)
                        continue;
                    
                    int chunkX = startChunkX + dx;
                    int chunkZ = startChunkZ + dz;
                    
                    // Проверяем границы чанков
                    if (chunkX < 0 || chunkX >= voxelWorld.chunksX || chunkZ < 0 || chunkZ >= voxelWorld.chunksZ)
                        continue;
                    
                    Vector3 safePosition = FindSafePositionInChunk(chunkX, chunkZ);
                    if (safePosition != Vector3.zero)
                    {
                        
                        return safePosition;
                    }
                }
            }
        }
        
        
        return Vector3.zero;
    }
    
    private Vector3 FindSafePositionInChunk(int chunkX, int chunkZ)
    {
        // Проверяем чанк сверху вниз послойно (но не ниже 10 блоков - зона смерти)
        for (int y = VoxelChunk16.HEIGHT - 1; y >= 10; y--)
        {
            // Проверяем несколько точек в чанке на этой высоте
            for (int localX = 1; localX < VoxelChunk16.WIDTH - 1; localX += 4) // Шаг 4 для оптимизации
            {
                for (int localZ = 1; localZ < VoxelChunk16.DEPTH - 1; localZ += 4)
                {
                    int worldX = chunkX * VoxelChunk16.WIDTH + localX;
                    int worldZ = chunkZ * VoxelChunk16.DEPTH + localZ;
                    
                    Vector3 testPosition = new Vector3(worldX + 0.5f, y + 1f, worldZ + 0.5f);
                    
                    if (IsPositionSafe(testPosition))
                    {
                        
                        return testPosition;
                    }
                }
            }
        }
        
        return Vector3.zero;
    }
    
    private void ResetWorld()
    {
        
        
        // Находим игрока
        if (!spawnables.TryGetValue("Player", out var playerData))
        {
            Debug.LogError("AutoSpawnService: Игрок не найден! Не можем сбросить мир.");
            return;
        }
        
        // Отключаем CharacterController игрока
        CharacterController controller = playerData.spawnable.GetGameObject().GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            
        }
        
        // Перемещаем игрока на стартовую позицию
        playerData.spawnable.GetTransform().position = playerData.startPosition;
        
        
        // Генерируем новый мир
        if (voxelWorld != null)
        {
            
            voxelWorld.Generate();
            
        }
        
        // Включаем CharacterController игрока
        if (controller != null)
        {
            controller.enabled = true;
            
        }
        
        // Сбрасываем сохраненные позиции всех сущностей на стартовые
        foreach (var kvp in spawnables)
        {
            kvp.Value.lastSavedPosition = kvp.Value.startPosition;
            kvp.Value.hasValidSavePosition = true;
            kvp.Value.lastSaveTime = Time.time;
        }
        
        // Сбрасываем флаг перемещения игрока, чтобы он переместился на новую точку спавна
        hasMovedPlayerToSpawn = false;
        
        
    }
    
    public void ForceSavePosition(ISpawnable spawnable)
    {
        string id = spawnable.GetSpawnableID();
        
        if (spawnables.TryGetValue(id, out var data))
        {
            if (spawnable.IsGrounded())
            {
                SavePosition(data);
            }
        }
    }
    
    public Vector3 GetLastSavedPosition(string spawnableID)
    {
        if (spawnables.TryGetValue(spawnableID, out var data))
        {
            return data.lastSavedPosition;
        }
        return Vector3.zero;
    }
    
    public bool HasValidSavePosition(string spawnableID)
    {
        if (spawnables.TryGetValue(spawnableID, out var data))
        {
            return data.hasValidSavePosition;
        }
        return false;
    }
    
    public int GetRegisteredCount()
    {
        return spawnables.Count;
    }
    
    public void SetSaveInterval(float interval)
    {
        saveInterval = Mathf.Max(0.1f, interval);
    }
    
    /// <summary>
    /// Сбрасывает флаг перемещения игрока на точку спавна
    /// </summary>
    public void ResetPlayerSpawnFlag()
    {
        hasMovedPlayerToSpawn = false;
        Debug.Log("[AutoSpawn] Флаг перемещения игрока сброшен");
    }
    
    /// <summary>
    /// Обработка регенерации мира для игрока
    /// </summary>
    public void OnWorldRegenerated(PlayerController player)
    {
        string id = "Player";
        
        if (spawnables.TryGetValue(id, out var data))
        {
            // Сбрасываем флаг перемещения
            hasMovedPlayerToSpawn = false;
            
            // Обновляем точку спавна из нового мира
            if (voxelWorld != null && voxelWorld.IsWorldReady)
            {
                Vector3 worldSpawnPoint = voxelWorld.PlayerSpawnPoint;
                if (worldSpawnPoint != Vector3.zero)
                {
                    data.startPosition = worldSpawnPoint;
                    data.lastSavedPosition = worldSpawnPoint;
                    data.hasValidSavePosition = true;
                    data.lastSaveTime = Time.time;
                    
                    Debug.Log($"[AutoSpawn] Игрок обновлен после регенерации мира. Новая точка спавна: {worldSpawnPoint}");
                }
            }
        }
    }
}

