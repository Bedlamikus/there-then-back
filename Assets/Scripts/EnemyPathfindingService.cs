using System.Collections.Generic;
using UnityEngine;

public class EnemyPathfindingService
{
    private Transform transform;
    private EnemyPathfindingConfig config;
    private VoxelWorld voxelWorld;
    
    // Состояние поиска пути
    private List<Vector3> currentPath;
    private int currentWaypointIndex;
    private float lastPathUpdateTime;
    
    // Обнаружение застревания
    private float stuckCheckTimer;
    private int stuckAttempts;
    private bool isInStuckArea;
    private Vector3 stuckAreaCenter;
    private Vector3 stuckCheckPosition;
    private float unstuckPatrolTimer;
    private bool isRecoveringFromStuck;
    private bool justFinishedRecovery;
    private bool shouldJumpThisFrame; // Флаг для одноразового прыжка
    
    // Обнаружение колебаний
    private Vector3 lastPatrolTarget;
    private float oscillationTimer;
    private const float oscillationCheckTime = 3f;
    
    // Кэш границ мира
    private float cachedWorldMinX = 5f;
    private float cachedWorldMinZ = 5f;
    private float cachedWorldMaxX = 75f;
    private float cachedWorldMaxZ = 75f;
    private bool worldBoundsCached = false;
    
    public List<Vector3> CurrentPath => currentPath;
    public int CurrentWaypointIndex => currentWaypointIndex;
    public bool IsRecoveringFromStuck => isRecoveringFromStuck;
    public bool ShouldJump
    {
        get
        {
            bool result = shouldJumpThisFrame;
            shouldJumpThisFrame = false; // Сбрасываем после чтения (одноразовый флаг)
            return result;
        }
    }
    public bool JustFinishedRecovery
    {
        get
        {
            bool result = justFinishedRecovery;
            justFinishedRecovery = false; // Сбрасываем после чтения
            return result;
        }
    }
    
    public EnemyPathfindingService(Transform transform, EnemyPathfindingConfig config)
    {
        this.transform = transform;
        this.config = config;
        voxelWorld = Object.FindObjectOfType<VoxelWorld>();
    }
    
    public virtual void Update(Vector3 currentMoveDirection)
    {
        if (isRecoveringFromStuck)
        {
            unstuckPatrolTimer += Time.deltaTime;
            
            // Проверяем вышли ли из зоны застревания
            if (isInStuckArea)
            {
                Vector3 currentPos = transform.position;
                Vector3 offset = currentPos - stuckAreaCenter;
                bool stillInArea = Mathf.Abs(offset.x) <= config.stuckAreaSize * 0.5f && 
                                   Mathf.Abs(offset.z) <= config.stuckAreaSize * 0.5f;
                
                if (!stillInArea)
                {
                    stuckAttempts = 0;
                    isInStuckArea = false;
                }
            }
            
            if (unstuckPatrolTimer >= config.unstuckPatrolTime)
            {
                isRecoveringFromStuck = false;
                justFinishedRecovery = true;
                
                // Если все еще в зоне - принудительно сбрасываем
                if (isInStuckArea)
                {
                    stuckAttempts = 0;
                    isInStuckArea = false;
                }
            }
        }
        else
        {
            CheckIfStuck(currentMoveDirection);
        }
    }
    
    public virtual Vector3 GetMoveDirection(Vector3 targetPosition, bool usePathfinding)
    {
        if (usePathfinding && currentPath != null && currentPath.Count > 0)
        {
            return FollowPath();
        }
        else
        {
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0;
            return direction.normalized;
        }
    }
    
    public virtual void StartPathfindingToTarget(Transform target)
    {
        if (target == null) return;
        StartPathfindingToPosition(target.position);
    }
    
    public virtual void StartPathfindingToPosition(Vector3 targetPosition)
    {
        if (voxelWorld == null) return;
        
        Vector3 startPos = transform.position;
        Vector3 endPos = targetPosition;
        
        // Простой жадный алгоритм поиска пути
        currentPath = FindPathGreedy(startPos, endPos);
        currentWaypointIndex = 0;
        lastPathUpdateTime = Time.time;
    }
    
    public virtual void StopPathfinding()
    {
        currentPath = null;
        currentWaypointIndex = 0;
    }
    
    public virtual bool CheckOscillationNearTarget(Vector3 patrolTarget, float currentDistance)
    {
        // Проверяем только если цель не изменилась
        if (patrolTarget != lastPatrolTarget)
        {
            lastPatrolTarget = patrolTarget;
            oscillationTimer = 0f;
            return false;
        }
        
        // Если расстояние больше порога колебаний - не считаем колебанием
        if (currentDistance > config.oscillationThreshold)
        {
            oscillationTimer = 0f;
            return false;
        }
        
        // Увеличиваем таймер колебаний
        oscillationTimer += Time.deltaTime;
        
        // Если колебаемся достаточно долго - считаем что достигли цели
        if (oscillationTimer >= oscillationCheckTime)
        {
            oscillationTimer = 0f;
            return true;
        }
        
        return false;
    }
    
    public virtual Vector3 FindSafePatrolPoint(Vector3 startPosition, float patrolRadius, float minPatrolDistance)
    {
        int maxAttempts = 10;
        
        // Получаем границы мира (с кэшированием)
        UpdateWorldBounds();
        
        float worldMinX = cachedWorldMinX;
        float worldMinZ = cachedWorldMinZ;
        float worldMaxX = cachedWorldMaxX;
        float worldMaxZ = cachedWorldMaxZ;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            Vector3 randomPoint = startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // ВАЖНО: Ограничиваем координаты границами мира
            randomPoint.x = Mathf.Clamp(randomPoint.x, worldMinX, worldMaxX);
            randomPoint.z = Mathf.Clamp(randomPoint.z, worldMinZ, worldMaxZ);
            
            float distanceToNewPoint = Vector3.Distance(transform.position, randomPoint);
            
            if (distanceToNewPoint < minPatrolDistance)
            {
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle));
                randomPoint = transform.position + direction * minPatrolDistance;
                
                // Снова ограничиваем
                randomPoint.x = Mathf.Clamp(randomPoint.x, worldMinX, worldMaxX);
                randomPoint.z = Mathf.Clamp(randomPoint.z, worldMinZ, worldMaxZ);
            }
            
            Vector3 offsetFromStart = randomPoint - startPosition;
            if (offsetFromStart.magnitude > patrolRadius)
            {
                randomPoint = startPosition + offsetFromStart.normalized * patrolRadius;
                
                // И снова ограничиваем
                randomPoint.x = Mathf.Clamp(randomPoint.x, worldMinX, worldMaxX);
                randomPoint.z = Mathf.Clamp(randomPoint.z, worldMinZ, worldMaxZ);
            }
            
            Vector3 safePoint = FindSafeHeightForPoint(randomPoint, startPosition);
            
            // ИСПРАВЛЕНИЕ: Дополнительная проверка что точка не слишком высокая
            if (safePoint != Vector3.zero)
            {
                // Проверяем что найденная точка не слишком далеко от земли
                Vector3Int safeVoxel = VoxelWorld.WorldToVoxel(safePoint);
                int groundLevel = FindGroundLevel(safeVoxel.x, safeVoxel.z);
                
                // Если точка не более чем на 10 блоков выше земли - принимаем её
                if (safePoint.y <= groundLevel + 10)
                {
                    return safePoint;
                }
                // Иначе продолжаем поиск
            }
        }
        
        // Если не нашли - возвращаем стартовую позицию (безопаснее чем текущую)
        if (startPosition != Vector3.zero)
        {
            return startPosition;
        }
        
        return transform.position;
    }
    
    public void StartUnstuckRecovery()
    {
        isRecoveringFromStuck = true;
        unstuckPatrolTimer = 0f;
        // НЕ сбрасываем stuckAttempts и isInStuckArea сразу
        // Они сбросятся когда бот выйдет из зоны застревания
    }
    
    public void ResetStuckDetection()
    {
        stuckAttempts = 0;
        isInStuckArea = false;
        stuckCheckTimer = 0f;
        shouldJumpThisFrame = false;
    }
    
    public void ConsumeJumpFlag()
    {
        // Принудительно сбрасываем флаг прыжка (если не был прочитан)
        shouldJumpThisFrame = false;
    }
    
    private void UpdateWorldBounds()
    {
        // Если уже кэшировали и VoxelWorld есть - не обновляем
        if (worldBoundsCached && voxelWorld != null) return;
        
        if (voxelWorld != null)
        {
            // Получаем реальные размеры мира из VoxelWorld
            int worldWidth = voxelWorld.GetWorldWidth();
            int worldDepth = voxelWorld.GetWorldDepth();
            
            // Устанавливаем границы с буфером 5 блоков от края
            cachedWorldMinX = 5f;
            cachedWorldMinZ = 5f;
            cachedWorldMaxX = worldWidth - 5f;
            cachedWorldMaxZ = worldDepth - 5f;
            
            worldBoundsCached = true;
        }
        else if (!worldBoundsCached)
        {
            // Используем значения по умолчанию
            worldBoundsCached = true;
        }
    }
    
    private Vector3 FollowPath()
    {
        if (currentPath == null || currentWaypointIndex >= currentPath.Count)
            return Vector3.zero;
        
        Vector3 currentWaypoint = currentPath[currentWaypointIndex];
        Vector3 directionToWaypoint = currentWaypoint - transform.position;
        float distanceToWaypoint = directionToWaypoint.magnitude;
        
        if (distanceToWaypoint < config.waypointReachDistance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= currentPath.Count)
            {
                currentPath = null;
                return Vector3.zero;
            }
            currentWaypoint = currentPath[currentWaypointIndex];
            directionToWaypoint = currentWaypoint - transform.position;
        }
        
        directionToWaypoint.y = 0;
        return directionToWaypoint.normalized;
    }
    
    private List<Vector3> FindPathGreedy(Vector3 start, Vector3 end)
    {
        List<Vector3> path = new List<Vector3>();
        Vector3 current = start;
        
        path.Add(current);
        
        int maxSteps = config.maxPathLength;
        int steps = 0;
        
        // Получаем размеры бота для более точного планирования
        float botDiameter = 0.7f;
        float botHeight = 1.8f;
        
        VoxelBotController botController = transform.GetComponent<VoxelBotController>();
        if (botController != null && botController.config != null)
        {
            botDiameter = botController.config.botDiameter;
            botHeight = botController.config.botHeight;
        }
        
        // Уменьшаем шаг для более точного пути
        float stepSize = Mathf.Max(1.0f, botDiameter); // Шаг не меньше диаметра бота
        
        while (Vector3.Distance(current, end) > stepSize && steps < maxSteps)
        {
            Vector3 direction = (end - current).normalized;
            Vector3 nextStep = current + direction * stepSize;
            
            // Ищем проходимую позицию с учетом размера бота
            Vector3 walkableStep = FindWalkablePosition(nextStep);
            
            if (walkableStep != Vector3.zero)
            {
                current = walkableStep;
            }
            else
            {
                // Пытаемся обойти препятствие в 8 направлениях (включая диагонали)
                Vector3[] directions = {
                    new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
                    new Vector3(0, 0, 1), new Vector3(0, 0, -1),
                    new Vector3(0.7f, 0, 0.7f), new Vector3(-0.7f, 0, 0.7f),
                    new Vector3(0.7f, 0, -0.7f), new Vector3(-0.7f, 0, -0.7f)
                };
                
                bool foundAlternative = false;
                foreach (Vector3 dir in directions)
                {
                    Vector3 alternative = current + dir.normalized * stepSize;
                    Vector3 walkableAlternative = FindWalkablePosition(alternative);
                    
                    if (walkableAlternative != Vector3.zero)
                    {
                        current = walkableAlternative;
                        foundAlternative = true;
                        break;
                    }
                }
                
                if (!foundAlternative)
                {
                    // Пробуем прыжок вверх
                    Vector3 jumpUp = current + direction * stepSize + Vector3.up * 2f;
                    Vector3 walkableJump = FindWalkablePosition(jumpUp);
                    
                    if (walkableJump != Vector3.zero)
                    {
                        current = walkableJump;
                    }
                    else
                    {
                        break; // Не можем найти путь
                    }
                }
            }
            
            path.Add(current);
            steps++;
        }
        
        return path;
    }
    
    private Vector3 FindWalkablePosition(Vector3 targetPosition)
    {
        if (voxelWorld == null) return targetPosition;
        
        // Округляем до ближайшего центра блока для более точной проверки
        int blockX = Mathf.RoundToInt(targetPosition.x);
        int blockZ = Mathf.RoundToInt(targetPosition.z);
        int startY = Mathf.RoundToInt(targetPosition.y);
        
        // Ищем проходимую высоту в диапазоне ±15 блоков от целевой Y
        for (int yOffset = 0; yOffset <= 15; yOffset++)
        {
            // Проверяем сначала вверх, потом вниз
            int[] yChecks = { startY + yOffset, startY - yOffset };
            
            foreach (int checkY in yChecks)
            {
                if (checkY < 0 || checkY >= voxelWorld.GetWorldHeight()) continue;
                
                // Проверяем проходимость с учетом размера бота
                if (IsWalkablePosition(blockX, checkY, blockZ))
                {
                    // Возвращаем позицию в центре блока
                    return new Vector3(blockX + 0.5f, checkY + 0.5f, blockZ + 0.5f);
                }
            }
        }
        
        return Vector3.zero; // Не нашли проходимую позицию
    }
    
    private bool IsWalkablePosition(int blockX, int blockY, int blockZ)
    {
        if (voxelWorld == null) return false;
        
        // Получаем размеры бота из конфига (если доступен)
        float botDiameter = 0.7f; // По умолчанию
        float botHeight = 1.8f;   // По умолчанию
        
        // Пытаемся получить размеры из VoxelBotController
        VoxelBotController botController = transform.GetComponent<VoxelBotController>();
        if (botController != null && botController.config != null)
        {
            botDiameter = botController.config.botDiameter;
            botHeight = botController.config.botHeight;
        }
        
        // Вычисляем количество блоков для проверки
        int radiusBlocks = Mathf.CeilToInt(botDiameter / 2f); // Радиус в блоках
        int heightBlocks = Mathf.CeilToInt(botHeight);        // Высота в блоках
        
        // Проверяем все блоки в области бота
        for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
        {
            for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
            {
                for (int dy = 0; dy <= heightBlocks; dy++)
                {
                    int checkX = blockX + dx;
                    int checkY = blockY + dy;
                    int checkZ = blockZ + dz;
                    
                    // Проверяем что в этой позиции нет блока (можем стоять)
                    if (voxelWorld.HasBlockAt(checkX, checkY, checkZ)) return false;
                }
            }
        }
        
        // Проверяем что есть земля под ногами (вся область под ботом)
        bool hasGround = false;
        for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
        {
            for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
            {
                int checkX = blockX + dx;
                int checkZ = blockZ + dz;
                
                if (voxelWorld.HasBlockAt(checkX, blockY - 1, checkZ))
                {
                    hasGround = true;
                    break;
                }
            }
        }
        
        if (!hasGround) return false;
        
        return true;
    }
    
    private bool CanMoveTo(Vector3 position)
    {
        if (voxelWorld == null) return true;
        
        int blockX = Mathf.FloorToInt(position.x);
        int blockY = Mathf.FloorToInt(position.y);
        int blockZ = Mathf.FloorToInt(position.z);
        
        return !voxelWorld.HasBlockAt(blockX, blockY, blockZ);
    }
    
    private void CheckIfStuck(Vector3 currentMoveDirection)
    {
        if (isRecoveringFromStuck) return;
        
        // Проверяем, пытается ли бот двигаться
        bool isTryingToMove = currentMoveDirection.sqrMagnitude > 0.1f;
        
        if (!isTryingToMove)
        {
            // Если бот не пытается двигаться, сбрасываем таймер
            stuckCheckTimer = 0f;
            return;
        }
        
        // ВАЖНО: Проверяем застревание только на земле, не в воздухе!
        CharacterController controller = transform.GetComponent<CharacterController>();
        if (controller != null && !controller.isGrounded)
        {
            // Бот в воздухе - не проверяем застревание
            stuckCheckTimer = 0f;
            return;
        }
        
        stuckCheckTimer += Time.deltaTime;
        
        if (stuckCheckTimer >= config.stuckCheckTime)
        {
            Vector3 currentPos = transform.position;
            
            if (!isInStuckArea)
            {
                stuckAreaCenter = currentPos;
                isInStuckArea = true;
                stuckCheckPosition = currentPos;
            }
            
            Vector3 offset = currentPos - stuckAreaCenter;
            bool stillInArea = Mathf.Abs(offset.x) <= config.stuckAreaSize * 0.5f && 
                               Mathf.Abs(offset.z) <= config.stuckAreaSize * 0.5f;
            
            if (!stillInArea)
            {
                stuckAttempts = 0;
                isInStuckArea = false;
            }
            else
            {
                // Проверяем текущее значение stuckAttempts ПЕРЕД увеличением
                if (stuckAttempts == 0)
                {
                    // Первая попытка - проверяем есть ли препятствие, если да - прыжок
                    if (HasObstacleInFront(currentMoveDirection))
                    {
                        stuckAttempts = 1;
                        shouldJumpThisFrame = true;
                        stuckCheckTimer = 0f;
                        stuckCheckPosition = currentPos;
                        return;
                    }
                    else
                    {
                        stuckAttempts = 2;
                        stuckCheckTimer = 0f;
                        stuckCheckPosition = currentPos;
                    }
                }
                else if (stuckAttempts == 1)
                {
                    // После прыжка - переходим к поиску нового пути
                    stuckAttempts = 2;
                    StopPathfinding();
                    stuckCheckTimer = 0f;
                    stuckCheckPosition = currentPos;
                }
                else if (stuckAttempts == 2)
                {
                    // Новый путь не помог - переходим к отдыху
                    stuckAttempts = 3;
                    StartUnstuckRecovery();
                    stuckCheckTimer = 0f;
                    stuckCheckPosition = currentPos;
                }
            }
        }
    }
    
    private Vector3 FindSafeHeightForPoint(Vector3 point, Vector3 startPosition)
    {
        // Округляем до ближайшего центра блока для более точной проверки
        int blockX = Mathf.RoundToInt(point.x);
        int blockZ = Mathf.RoundToInt(point.z);
        float centerX = blockX + 0.5f;
        float centerZ = blockZ + 0.5f;
        
        // ИСПРАВЛЕНИЕ: Находим уровень земли для этого блока
        int groundLevel = FindGroundLevel(blockX, blockZ);
        int startY = Mathf.RoundToInt(startPosition.y);
        
        // ИСПРАВЛЕНИЕ: Ограничиваем поиск разумными пределами
        int minY = Mathf.Max(0, groundLevel - 5); // Не ниже чем на 5 блоков под землей
        int maxY = Mathf.Min(
            voxelWorld != null ? voxelWorld.GetWorldHeight() - 3 : 125, 
            Mathf.Max(groundLevel + 10, startY + 5) // Не выше чем на 10 блоков над землей или на 5 от старта
        );
        
        // ИСПРАВЛЕНИЕ: Приоритизируем поиск ближе к земле
        // Сначала ищем от уровня земли вверх (более безопасно)
        for (int y = groundLevel; y <= maxY; y++)
        {
            if (IsWalkablePosition(blockX, y, blockZ))
            {
                // Дополнительная проверка: не слишком ли высоко?
                if (y <= groundLevel + 15) // Максимум на 15 блоков над землей
                {
                    return new Vector3(centerX, y + 0.5f, centerZ);
                }
            }
        }
        
        // Если не нашли вверх от земли, пробуем вниз от земли
        for (int y = groundLevel - 1; y >= minY; y--)
        {
            if (IsWalkablePosition(blockX, y, blockZ))
            {
                return new Vector3(centerX, y + 0.5f, centerZ);
            }
        }
        
        // Если ничего не нашли - возвращаем точку на уровне земли
        return new Vector3(centerX, groundLevel + 0.5f, centerZ);
    }
    
    /// <summary>
    /// Находит уровень земли для указанного блока
    /// </summary>
    private int FindGroundLevel(int blockX, int blockZ)
    {
        if (voxelWorld == null) return 64; // Значение по умолчанию
        
        // Ищем самый верхний твердый блок (поверхность земли)
        for (int y = voxelWorld.GetWorldHeight() - 1; y >= 0; y--)
        {
            if (VoxelWorld.IsVoxelSolid(new Vector3Int(blockX, y, blockZ)))
            {
                return y + 1; // Возвращаем уровень НАД поверхностью
            }
        }
        
        return 0; // Если не нашли - возвращаем 0
    }
    
    private bool IsPositionSafe(Vector3 position, Vector3 groundPosition)
    {
        if (HasSolidBlockAt(position)) return false;
        if (!HasSolidBlockAt(groundPosition)) return false;
        
        Vector3 headCheckPoint = new Vector3(position.x, position.y + 2f, position.z);
        if (HasSolidBlockAt(headCheckPoint)) return false;
        
        return true;
    }
    
    private bool HasSolidBlockAt(Vector3 worldPosition)
    {
        if (voxelWorld != null)
        {
            int blockX = Mathf.FloorToInt(worldPosition.x);
            int blockY = Mathf.FloorToInt(worldPosition.y);
            int blockZ = Mathf.FloorToInt(worldPosition.z);
            
            return voxelWorld.HasBlockAt(blockX, blockY, blockZ);
        }
        
        return Physics.CheckBox(worldPosition, Vector3.one * 0.4f, Quaternion.identity, LayerMask.GetMask("Default"));
    }
    
    private bool HasObstacleInFront(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.01f) return false;
        
        Vector3 currentPos = transform.position;
        Vector3 checkDirection = moveDirection.normalized;
        
        // Получаем размеры бота из конфига
        float botDiameter = 0.7f; // По умолчанию
        float botHeight = 1.8f;   // По умолчанию
        
        VoxelBotController botController = transform.GetComponent<VoxelBotController>();
        if (botController != null && botController.config != null)
        {
            botDiameter = botController.config.botDiameter;
            botHeight = botController.config.botHeight;
        }
        
        // Вычисляем количество блоков для проверки
        int radiusBlocks = Mathf.CeilToInt(botDiameter / 2f);
        int heightBlocks = Mathf.CeilToInt(botHeight);
        
        // Проверяем препятствия на разных высотах и по ширине бота
        for (int dy = 0; dy <= heightBlocks; dy++)
        {
            for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
            {
                for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
                {
                    // Вычисляем позицию для проверки
                    Vector3 offset = new Vector3(dx, dy, dz);
                    Vector3 checkPos = currentPos + offset + checkDirection * 1.5f; // 1.5 блока вперед
                    
                    // Проверяем есть ли препятствие в этой позиции
                    if (HasSolidBlockAt(checkPos))
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
}
