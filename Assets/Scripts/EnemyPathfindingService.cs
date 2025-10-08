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
    
    public void Update(Vector3 currentMoveDirection)
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
                    Debug.Log($"[Pathfinding] Вышли из зоны застревания во время восстановления. Позиция: {currentPos}");
                    stuckAttempts = 0;
                    isInStuckArea = false;
                }
            }
            
            if (unstuckPatrolTimer >= config.unstuckPatrolTime)
            {
                Debug.Log($"[Pathfinding] Восстановление завершено. В зоне застревания: {isInStuckArea}");
                isRecoveringFromStuck = false;
                justFinishedRecovery = true; // Сигнализируем о завершении
                
                // Если все еще в зоне - принудительно сбрасываем
                if (isInStuckArea)
                {
                    Debug.LogWarning($"[Pathfinding] Все еще в зоне застревания после восстановления! Принудительно сбрасываем.");
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
    
    public Vector3 GetMoveDirection(Vector3 targetPosition, bool usePathfinding)
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
    
    public void StartPathfindingToTarget(Transform target)
    {
        if (target == null) return;
        StartPathfindingToPosition(target.position);
    }
    
    public void StartPathfindingToPosition(Vector3 targetPosition)
    {
        if (voxelWorld == null) return;
        
        Vector3 startPos = transform.position;
        Vector3 endPos = targetPosition;
        
        // Простой жадный алгоритм поиска пути
        currentPath = FindPathGreedy(startPos, endPos);
        currentWaypointIndex = 0;
        lastPathUpdateTime = Time.time;
    }
    
    public void StopPathfinding()
    {
        currentPath = null;
        currentWaypointIndex = 0;
    }
    
    public bool CheckOscillationNearTarget(Vector3 patrolTarget, float currentDistance)
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
    
    public Vector3 FindSafePatrolPoint(Vector3 startPosition, float patrolRadius, float minPatrolDistance)
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
            
            if (safePoint != Vector3.zero)
            {
                Debug.Log($"[Pathfinding] Найдена безопасная точка патруля: {safePoint} (попытка {attempt + 1}/{maxAttempts})");
                return safePoint;
            }
        }
        
        Debug.LogWarning($"[Pathfinding] Не удалось найти безопасную точку патруля за {maxAttempts} попыток. Остаемся на месте.");
        return transform.position;
    }
    
    public void StartUnstuckRecovery()
    {
        isRecoveringFromStuck = true;
        unstuckPatrolTimer = 0f;
        // НЕ сбрасываем stuckAttempts и isInStuckArea сразу
        // Они сбросятся когда бот выйдет из зоны застревания
        Debug.Log($"[Pathfinding] Начинаем восстановление от застревания. Центр зоны: {stuckAreaCenter}");
    }
    
    public void ResetStuckDetection()
    {
        stuckAttempts = 0;
        isInStuckArea = false;
        stuckCheckTimer = 0f;
        shouldJumpThisFrame = false;
        Debug.Log($"[Pathfinding] Сброс обнаружения застревания");
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
            
            Debug.Log($"[Pathfinding] Границы мира обновлены: X=[{cachedWorldMinX}, {cachedWorldMaxX}], Z=[{cachedWorldMinZ}, {cachedWorldMaxZ}] (размер мира: {worldWidth}x{worldDepth})");
        }
        else if (!worldBoundsCached)
        {
            // Используем значения по умолчанию
            Debug.LogWarning($"[Pathfinding] VoxelWorld не найден, используем границы по умолчанию: X=[5, 75], Z=[5, 75]");
            worldBoundsCached = true; // Не спамим предупреждениями
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
        
        while (Vector3.Distance(current, end) > config.waypointReachDistance && steps < maxSteps)
        {
            Vector3 direction = (end - current).normalized;
            Vector3 nextStep = current + direction * 2f; // Шаг в 2 блока
            
            // Проверяем можно ли идти прямо
            if (CanMoveTo(nextStep))
            {
                current = nextStep;
            }
            else
            {
                // Пытаемся обойти препятствие
                Vector3[] directions = {
                    new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
                    new Vector3(0, 0, 1), new Vector3(0, 0, -1)
                };
                
                bool foundAlternative = false;
                foreach (Vector3 dir in directions)
                {
                    Vector3 alternative = current + dir * 2f;
                    if (CanMoveTo(alternative))
                    {
                        current = alternative;
                        foundAlternative = true;
                        break;
                    }
                }
                
                if (!foundAlternative)
                {
                    break; // Не можем найти путь
                }
            }
            
            path.Add(current);
            steps++;
        }
        
        return path;
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
                if (stuckAttempts > 0)
                {
                    Debug.Log($"[Pathfinding] Бот вышел из зоны застревания. Попыток было: {stuckAttempts}");
                }
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
                        stuckAttempts = 1; // Устанавливаем в 1 для отслеживания
                        shouldJumpThisFrame = true; // Устанавливаем флаг прыжка (одноразовый)
                        Debug.Log($"[Pathfinding] Обнаружено застревание с препятствием! Попытка #1: Инициируем прыжок. Позиция: {currentPos}");
                        stuckCheckTimer = 0f;
                        stuckCheckPosition = currentPos;
                        return;
                    }
                    else
                    {
                        Debug.Log($"[Pathfinding] Застревание без препятствия. Попытка #1: Пропускаем прыжок, ищем новый путь.");
                        stuckAttempts = 2; // Сразу переходим ко второй попытке
                        stuckCheckTimer = 0f;
                        stuckCheckPosition = currentPos;
                    }
                }
                else if (stuckAttempts == 1)
                {
                    // После прыжка - переходим к поиску нового пути
                    stuckAttempts = 2;
                    Debug.Log($"[Pathfinding] Прыжок не помог. Попытка #2: Ищем новый путь. Позиция: {currentPos}");
                    StopPathfinding();
                    stuckCheckTimer = 0f;
                    stuckCheckPosition = currentPos;
                }
                else if (stuckAttempts == 2)
                {
                    // Новый путь не помог - переходим к отдыху
                    stuckAttempts = 3;
                    Debug.Log($"[Pathfinding] Новый путь не помог. Попытка #3: Переходим к отдыху на {config.unstuckPatrolTime}s. Позиция: {currentPos}");
                    StartUnstuckRecovery();
                    stuckCheckTimer = 0f;
                    stuckCheckPosition = currentPos;
                }
            }
        }
    }
    
    private Vector3 FindSafeHeightForPoint(Vector3 point, Vector3 startPosition)
    {
        float startY = startPosition.y;
        float minY = startY - 20f;
        float stepY = 1f;
        
        for (float y = startY; y >= minY; y -= stepY)
        {
            Vector3 testPoint = new Vector3(point.x, y, point.z);
            Vector3 groundCheckPoint = new Vector3(point.x, y - 1f, point.z);
            
            if (IsPositionSafe(testPoint, groundCheckPoint))
            {
                return testPoint;
            }
        }
        
        return Vector3.zero;
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
        
        // Проверяем на уровне головы, тела и ног
        float[] checkHeights = { 0f, 1f, 2f }; // Ноги, тело, голова
        
        foreach (float height in checkHeights)
        {
            Vector3 checkPos = currentPos + Vector3.up * height + checkDirection * 1.5f; // 1.5 блока вперед
            
            if (HasSolidBlockAt(checkPos))
            {
                Debug.Log($"[Pathfinding] Обнаружено препятствие на высоте {height}m в позиции {checkPos}");
                return true;
            }
        }
        
        return false;
    }
}
