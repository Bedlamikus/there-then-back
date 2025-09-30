using UnityEngine;

public class AutoSpawnService
{
    public static AutoSpawnService Instance { get; private set; }
    
    [Header("Auto Spawn Settings")]
    private float saveInterval = 5f; // Интервал сохранения позиции в секундах
    private float lastSaveTime = 0f;
    private Vector3 lastSavedPosition;
    private bool hasValidSavePosition = false;
    
    private PlayerController player;
    private VoxelWorld voxelWorld;
    private bool isWaitingForVoxelWorld = false;
    private float lastVoxelWorldCheckTime = 0f;
    private float voxelWorldCheckInterval = 0.5f; // Проверяем каждые 0.5 секунды
    private Vector3 startPosition; // Стартовая позиция игрока для сброса мира
    
    public AutoSpawnService()
    {
        if (Instance != null)
        {
            Debug.LogWarning("AutoSpawnService уже существует! Удаляем старый экземпляр.");
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
        player = playerController;
        
        // Пытаемся найти VoxelWorld
        TryFindVoxelWorld();
        
        // Сохраняем стартовую позицию игрока (для сброса мира)
        startPosition = player.transform.position;
        
        // Сохраняем начальную позицию игрока
        lastSavedPosition = player.transform.position;
        hasValidSavePosition = true;
        lastSaveTime = Time.time;
        
        Debug.Log($"AutoSpawnService инициализирован для игрока {player.name}, стартовая позиция: {startPosition}");
    }
    
    private void TryFindVoxelWorld()
    {
        voxelWorld = FindVoxelWorldInScene();
        
        if (voxelWorld == null)
        {
            Debug.Log("VoxelWorld пока не найден, ждем его появления...");
            isWaitingForVoxelWorld = true;
        }
        else
        {
            Debug.Log($"VoxelWorld найден: {voxelWorld.name}");
            isWaitingForVoxelWorld = false;
        }
    }
    
    public void Tick(float deltaTime)
    {
        if (player == null) return;
        
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
        
        // Проверяем, нужно ли сохранить позицию
        CheckForPositionSave();
    }
    
    private void CheckForPositionSave()
    {
        float currentTime = Time.time;
        
        // Если прошло достаточно времени с последнего сохранения
        if (currentTime - lastSaveTime >= saveInterval)
        {
            // Проверяем, находится ли игрок на земле
            if (IsPlayerOnGround())
            {
                SavePlayerPosition();
            }
            // Если игрок не на земле, ждем пока он окажется на земле
        }
    }
    
    private bool IsPlayerOnGround()
    {
        if (player == null) return false;
        
        // Используем CharacterController для проверки земли
        CharacterController controller = player.GetComponent<CharacterController>();
        return controller != null && controller.isGrounded;
    }
    
    private void SavePlayerPosition()
    {
        if (player == null) return;
        
        lastSavedPosition = player.transform.position;
        hasValidSavePosition = true;
        lastSaveTime = Time.time;
        
        Debug.Log($"Позиция игрока сохранена: {lastSavedPosition}");
    }
    
    public void OnPlayerEnterDeadZone(PlayerController deadPlayer)
    {
        if (deadPlayer != player)
        {
            Debug.LogWarning("Получено уведомление о смерти от другого игрока!");
            return;
        }
        
        Debug.Log("Игрок попал в зону смерти! Инициируем спавн...");
        
        // Скрываем игрока
        player.gameObject.SetActive(false);
        
        // Находим безопасную позицию для спавна
        Vector3 spawnPosition = FindSafeSpawnPosition();
        
        // Перемещаем игрока
        player.transform.position = spawnPosition;
        
        // Показываем игрока
        player.gameObject.SetActive(true);
        
        Debug.Log($"Игрок заспавнен в позиции: {spawnPosition}");
    }
    
    private Vector3 FindSafeSpawnPosition()
    {
        if (voxelWorld == null)
        {
            Debug.LogWarning("VoxelWorld не найден! Возвращаем стартовую позицию.");
            return startPosition;
        }
        
        // Сначала проверяем последнюю сохраненную позицию
        if (hasValidSavePosition && IsPositionSafe(lastSavedPosition))
        {
            Debug.Log("Последняя сохраненная позиция безопасна, используем её");
            return lastSavedPosition;
        }
        
        Debug.Log("Последняя сохраненная позиция небезопасна, ищем новую...");
        
        // Ищем точки ниже сохраненной позиции
        Vector3 safePosition = FindSafePositionBelow(lastSavedPosition);
        if (safePosition != Vector3.zero)
        {
            Debug.Log($"Найдена безопасная позиция ниже: {safePosition}");
            return safePosition;
        }
        
        // Если не найдено ниже, ищем по чанкам сверху вниз
        safePosition = FindSafePositionInChunks();
        if (safePosition != Vector3.zero)
        {
            Debug.Log($"Найдена безопасная позиция в чанках: {safePosition}");
            return safePosition;
        }
        
        // Если весь мир разрушен, сбрасываем мир
        Debug.LogWarning("Весь мир разрушен! Сбрасываем мир и возвращаемся на стартовую позицию.");
        ResetWorld();
        return startPosition;
    }
    
    private bool IsPositionSafe(Vector3 position)
    {
        if (voxelWorld == null) 
        {
            // Если VoxelWorld еще не найден, считаем позицию безопасной если она не слишком низко
            bool safe = position.y > -10f;
            Debug.Log($"VoxelWorld не найден, проверка высоты: {position.y} > -10 = {safe}");
            return safe;
        }
        
        // Проверяем, что позиция в пределах мира
        bool inBounds = position.x >= 0 && position.z >= 0 && position.x < voxelWorld.chunksX * VoxelChunk16.WIDTH && position.z < voxelWorld.chunksZ * VoxelChunk16.DEPTH;
        if (!inBounds)
        {
            Debug.Log($"Позиция вне границ мира: {position}, границы: (0,0) - ({voxelWorld.chunksX * VoxelChunk16.WIDTH}, {voxelWorld.chunksZ * VoxelChunk16.DEPTH})");
            return false;
        }
        
        // Проверяем, есть ли блок под ногами игрока (игрок стоит НА блоке, поэтому проверяем блок ниже)
        Vector3 checkPosition = position + Vector3.down * 1.0f; // Проверяем блок на 1 единицу ниже
        
        // Проверяем несколько точек вокруг позиции игрока (игрок стоит на блоке, а не внутри него)
        Vector3[] checkPoints = {
            checkPosition,
            checkPosition + Vector3.left * 0.4f,
            checkPosition + Vector3.right * 0.4f,
            checkPosition + Vector3.forward * 0.4f,
            checkPosition + Vector3.back * 0.4f
        };
        
        bool hasGround = false;
        int groundPoints = 0;
        foreach (Vector3 point in checkPoints)
        {
            bool hasBlock = HasSolidBlockAt(point);
            if (hasBlock) groundPoints++;
            Debug.Log($"Проверка земли в точке {point}: {hasBlock}");
        }
        
        hasGround = groundPoints > 0;
        Debug.Log($"Проверка земли завершена: {groundPoints}/5 точек имеют блоки, hasGround = {hasGround}");
        
        if (!hasGround) 
        {
            Debug.Log($"Нет земли под ногами в позиции {position}");
            return false;
        }
        
        // Дополнительно проверяем, что над головой игрока нет блоков (игрок высотой 2.3)
        Vector3 headPosition = position + Vector3.up * 2.5f; // Добавляем запас
        bool hasBlockAbove = HasSolidBlockAt(headPosition);
        Debug.Log($"Проверка блока над головой в точке {headPosition}: {hasBlockAbove}");
        
        if (hasBlockAbove)
        {
            Debug.Log($"Есть блок над головой в позиции {position}");
            return false; // Есть блок над головой
        }
        
        Debug.Log($"✓ Позиция {position} безопасна!");
        return true;
    }
    
    private bool HasSolidBlockAt(Vector3 worldPosition)
    {
        if (voxelWorld == null) 
        {
            Debug.Log($"VoxelWorld не найден, HasSolidBlockAt({worldPosition}) = false");
            return false;
        }
        
        // Конвертируем мировую позицию в координаты блока
        int blockX = Mathf.FloorToInt(worldPosition.x);
        int blockY = Mathf.FloorToInt(worldPosition.y);
        int blockZ = Mathf.FloorToInt(worldPosition.z);
        
        Debug.Log($"Проверка блока в мировых координатах {worldPosition} -> блочные координаты ({blockX}, {blockY}, {blockZ})");
        
        // Проверяем, что координаты в пределах мира
        if (blockX < 0 || blockZ < 0 || blockY < 0 || blockY >= VoxelChunk16.HEIGHT) 
        {
            Debug.Log($"Координаты вне пределов мира: x={blockX}, y={blockY}, z={blockZ}");
            return false;
        }
        if (blockX >= voxelWorld.chunksX * VoxelChunk16.WIDTH || blockZ >= voxelWorld.chunksZ * VoxelChunk16.DEPTH) 
        {
            Debug.Log($"Координаты вне пределов мира: x={blockX} >= {voxelWorld.chunksX * VoxelChunk16.WIDTH}, z={blockZ} >= {voxelWorld.chunksZ * VoxelChunk16.DEPTH}");
            return false;
        }
        
        // Получаем данные чанка
        int chunkX = blockX / VoxelChunk16.WIDTH;
        int chunkZ = blockZ / VoxelChunk16.DEPTH;
        int localX = blockX % VoxelChunk16.WIDTH;
        int localZ = blockZ % VoxelChunk16.DEPTH;
        
        Debug.Log($"Чанк координаты: chunkX={chunkX}, chunkZ={chunkZ}, localX={localX}, localZ={localZ}");
        
        // Проверяем через прямой доступ к данным чанка (более надежно)
        bool hasBlockInChunk = voxelWorld.HasBlockAt(blockX, blockY, blockZ);
        Debug.Log($"Прямая проверка блока в чанке ({blockX}, {blockY}, {blockZ}): {hasBlockInChunk}");
        
        // Дополнительно проверяем через Raycast для коллайдеров
        RaycastHit hit;
        Vector3 rayStart = worldPosition + Vector3.up * 0.1f;
        bool hasHit = Physics.Raycast(rayStart, Vector3.down, out hit, 1.5f);
        
        Debug.Log($"Raycast от {rayStart} вниз на 1.5f: hit={hasHit}");
        if (hasHit)
        {
            Debug.Log($"Raycast попал в: {hit.collider.name} в точке {hit.point}");
        }
        
        // Используем результат чанка как основной, Raycast как дополнительный
        bool finalResult = hasBlockInChunk || hasHit;
        Debug.Log($"Итоговый результат: ChunkData={hasBlockInChunk}, Raycast={hasHit}, Final={finalResult}");
        return finalResult;
    }
    
    
    private Vector3 FindSafePositionBelow(Vector3 savedPosition)
    {
        Debug.Log($"Ищем безопасную позицию ниже сохраненной: {savedPosition}");
        
        // Проверяем все точки ниже сохраненной позиции
        for (float y = savedPosition.y - 1f; y >= 0f; y -= 1f)
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
                    Debug.Log($"Найдена безопасная позиция ниже: {point}");
                    return point;
                }
            }
        }
        
        Debug.Log("Не найдено безопасных позиций ниже сохраненной");
        return Vector3.zero;
    }
    
    private Vector3 FindSafePositionInChunks()
    {
        Debug.Log("Ищем безопасную позицию по чанкам сверху вниз");
        
        // Начинаем с чанка, где была сохраненная позиция
        int startChunkX = Mathf.FloorToInt(lastSavedPosition.x / VoxelChunk16.WIDTH);
        int startChunkZ = Mathf.FloorToInt(lastSavedPosition.z / VoxelChunk16.DEPTH);
        
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
                        Debug.Log($"Найдена безопасная позиция в чанке ({chunkX}, {chunkZ}): {safePosition}");
                        return safePosition;
                    }
                }
            }
        }
        
        Debug.Log("Не найдено безопасных позиций ни в одном чанке");
        return Vector3.zero;
    }
    
    private Vector3 FindSafePositionInChunk(int chunkX, int chunkZ)
    {
        Debug.Log($"Ищем безопасную позицию в чанке ({chunkX}, {chunkZ}) сверху вниз");
        
        // Проверяем чанк сверху вниз послойно
        for (int y = VoxelChunk16.HEIGHT - 1; y >= 0; y--)
        {
            // Проверяем несколько точек в чанке на этой высоте
            for (int localX = 0; localX < VoxelChunk16.WIDTH; localX += 2) // Шаг 2 для оптимизации
            {
                for (int localZ = 0; localZ < VoxelChunk16.DEPTH; localZ += 2)
                {
                    int worldX = chunkX * VoxelChunk16.WIDTH + localX;
                    int worldZ = chunkZ * VoxelChunk16.DEPTH + localZ;
                    
                    Vector3 testPosition = new Vector3(worldX + 0.5f, y + 1f, worldZ + 0.5f);
                    
                    if (IsPositionSafe(testPosition))
                    {
                        Debug.Log($"Найдена безопасная позиция в чанке ({chunkX}, {chunkZ}) на высоте {y}: {testPosition}");
                        return testPosition;
                    }
                }
            }
        }
        
        Debug.Log($"Не найдено безопасных позиций в чанке ({chunkX}, {chunkZ})");
        return Vector3.zero;
    }
    
    private void ResetWorld()
    {
        Debug.Log("Сбрасываем мир и возвращаем игрока на стартовую позицию");
        
        // Отключаем Rigidbody игрока
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.isKinematic = true;
            Debug.Log("Rigidbody игрока отключен");
        }
        
        // Перемещаем игрока на стартовую позицию
        player.transform.position = startPosition;
        
        // Генерируем новый мир
        if (voxelWorld != null)
        {
            Debug.Log("Генерируем новый мир...");
            voxelWorld.Generate(); // Используем правильное имя метода
        }
        
        // Включаем Rigidbody игрока
        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            Debug.Log("Rigidbody игрока включен");
        }
        
        // Сбрасываем сохраненную позицию на стартовую
        lastSavedPosition = startPosition;
        hasValidSavePosition = true;
        
        Debug.Log($"Мир сброшен, игрок на стартовой позиции: {startPosition}");
    }
    
    public void ForceSavePosition()
    {
        if (player != null && IsPlayerOnGround())
        {
            SavePlayerPosition();
        }
    }
    
    public Vector3 GetLastSavedPosition()
    {
        return lastSavedPosition;
    }
    
    public bool HasValidSavePosition()
    {
        return hasValidSavePosition;
    }
    
    public void SetSaveInterval(float interval)
    {
        saveInterval = Mathf.Max(0.1f, interval);
    }
}

