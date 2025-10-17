using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Контроллер движения бота по вокселям
/// </summary>
public class VoxelBotController : MonoBehaviour
{
    [Header("Configuration")]
    public VoxelBotConfig config;
    
    [Header("Bot Identity")]
    public string botId;
    
    [Header("Animation")]
    public Animator animator;
    
    // Компоненты
    private VoxelBotData botData;
    private VoxelPathfinding pathfinding;
    private Transform target;
    
    // Состояние движения
    private List<Vector3Int> currentPath = new List<Vector3Int>();
    private int currentPathIndex = 0;
    private Vector3 targetWorldPosition;
    private bool isMoving = false;
    private bool isJumping = false;
    private float jumpStartTime;
    private Vector3 jumpStartPosition;
    private Vector3 jumpTargetPosition;
    private Vector3 lastPosition;
    private Vector3 currentVelocity;
    
    // Физика и гравитация
    private Vector3 velocity = Vector3.zero;
    private bool isGrounded = false;
    private float lastGroundCheckTime = 0f;
    private const float groundCheckInterval = 0.1f;
    
    // Состояние ИИ
    private bool hasTarget = false;
    private float lastPathUpdateTime = 0f;
    
    // Команды от ИИ
    private Vector3 aiMoveDirection = Vector3.zero;
    private bool aiShouldJump = false;
    private bool isAIControlled = false;
    
    void Start()
    {
        // Генерируем ID если не задан
        if (string.IsNullOrEmpty(botId))
        {
            botId = $"Bot_{GetInstanceID()}";
        }
        
        // Инициализируем компоненты
        InitializeComponents();
        
        // Регистрируем бота в базе данных
        VoxelBotDatabase.Instance.RegisterBot(botId, transform.position, config);
        
        // Инициализируем позицию для вычисления скорости
        lastPosition = transform.position;
        
        Debug.Log($"[VoxelBotController] Initialized bot: {botId}");
    }
    
    void Update()
    {
        if (botData == null) return;
        
        // Вычисляем скорость движения (только горизонтальную для анимации)
        Vector3 fullVelocity = (transform.position - lastPosition) / Time.deltaTime;
        currentVelocity = new Vector3(fullVelocity.x, 0, fullVelocity.z); // Только горизонтальная скорость
        lastPosition = transform.position;
        
        // Обновляем данные бота
        UpdateBotData();
        
        // Проверяем землю под ботом
        CheckGrounded();
        
        // Применяем гравитацию
        ApplyGravity();
        
        // Обрабатываем прыжок
        HandleJump();
        
        // Обрабатываем движение
        HandleMovement();
        
        // Обновляем позицию в базе данных
        VoxelBotDatabase.Instance.UpdateBotPosition(botId, transform.position);
        
        // Обновляем анимацию
        UpdateAnimation();
    }
    
    void OnDestroy()
    {
        // Удаляем бота из базы данных
        VoxelBotDatabase.Instance.UnregisterBot(botId);
    }
    
    /// <summary>
    /// Инициализирует компоненты
    /// </summary>
    void InitializeComponents()
    {
        // Проверяем что конфиг назначен
        if (config == null)
        {
            Debug.LogError($"[VoxelBotController] VoxelBotConfig not assigned for {botId}! Please assign config in inspector.");
            return;
        }
        
        // Создаем данные бота
        botData = new VoxelBotData(botId, transform.position, config);
        
        // Создаем систему поиска пути
        pathfinding = new VoxelPathfinding(config);
        
        // Удаляем CharacterController если есть
        CharacterController oldController = GetComponent<CharacterController>();
        if (oldController != null)
        {
            DestroyImmediate(oldController);
        }
        
        // Удаляем Rigidbody если есть
        Rigidbody oldRigidbody = GetComponent<Rigidbody>();
        if (oldRigidbody != null)
        {
            DestroyImmediate(oldRigidbody);
        }
    }
    
    /// <summary>
    /// Обновляет данные бота
    /// </summary>
    void UpdateBotData()
    {
        botData.UpdatePosition(transform.position);
        botData.isGrounded = IsGrounded();
        botData.isJumping = isJumping;
        botData.currentHeight = GetCurrentHeight();
        botData.velocity = currentVelocity;
    }
    
    /// <summary>
    /// Обрабатывает движение
    /// </summary>
    void HandleMovement()
    {
        // Блокируем движение если бот мертв или прыгает
        if (botData == null || botData.isDead || isJumping) return;
        
        // Если управляется ИИ - используем команды от ИИ
        if (isAIControlled)
        {
            HandleAIMovement();
        }
        else if (hasTarget && target != null)
        {
            // Старый режим - движение к цели
            HandleTargetMovement();
        }
        else
        {
            // Останавливаемся если нет цели
            StopMovement();
        }
    }
    
    /// <summary>
    /// Обрабатывает движение под управлением ИИ
    /// </summary>
    void HandleAIMovement()
    {
        if (aiMoveDirection.sqrMagnitude > 0.1f)
        {
            // Двигаемся в направлении от ИИ
            MoveInDirection(aiMoveDirection);
            isMoving = true;
        }
        else
        {
            // Останавливаемся
            StopMovement();
        }
        
        // Обрабатываем прыжок от ИИ
        if (aiShouldJump && !isJumping)
        {
            // Для ИИ прыжок происходит в направлении движения
            Vector3 jumpTarget = transform.position + aiMoveDirection * config.jumpDistance;
            StartJump(jumpTarget);
        }
    }
    
    /// <summary>
    /// Обрабатывает движение к цели (старый режим)
    /// </summary>
    void HandleTargetMovement()
    {
        // Обновляем путь если нужно
        if (Time.time - lastPathUpdateTime > config.pathUpdateInterval)
        {
            UpdatePath();
            lastPathUpdateTime = Time.time;
        }
        
        // Двигаемся по пути
        MoveAlongPath();
    }
    
    /// <summary>
    /// Обновляет путь к цели
    /// </summary>
    void UpdatePath()
    {
        Vector3Int startPos = VoxelWorld.WorldToVoxel(transform.position);
        Vector3Int targetPos = VoxelWorld.WorldToVoxel(target.position);
        
        // Проверяем видимость
        if (pathfinding.HasLineOfSight(startPos, targetPos))
        {
            // Прямой путь
            currentPath = new List<Vector3Int> { targetPos };
        }
        else
        {
            // Ищем путь через A*
            currentPath = pathfinding.FindPath(startPos, targetPos);
        }
        
        currentPathIndex = 0;
        
        if (currentPath.Count > 0)
        {
            Debug.Log($"[VoxelBotController] Found path with {currentPath.Count} points");
        }
        else
        {
            Debug.LogWarning($"[VoxelBotController] No path found to target");
        }
    }
    
    /// <summary>
    /// Двигается по пути
    /// </summary>
    void MoveAlongPath()
    {
        if (currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            StopMovement();
            return;
        }
        
        Vector3Int targetVoxel = currentPath[currentPathIndex];
        Vector3 targetWorldPos = VoxelWorld.VoxelToWorld(targetVoxel);
        
        // Проверяем достижение текущей точки
        float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);
        if (distanceToTarget < config.goalReachDistance)
        {
            currentPathIndex++;
            return;
        }
        
        // Проверяем нужно ли прыгать
        if (ShouldJumpToTarget(targetVoxel))
        {
            StartJump(targetWorldPos);
        }
        else
        {
            // Обычное движение
            MoveTowardsTarget(targetWorldPos);
        }
    }
    
    /// <summary>
    /// Двигается в заданном направлении (для ИИ)
    /// </summary>
    void MoveInDirection(Vector3 direction)
    {
        // Нормализуем направление
        direction.y = 0; // Только горизонтальное движение
        direction = direction.normalized;
        
        // ПОВОРОТ: Поворачиваем бота в направлении движения
        if (direction.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            float turnSpeed = 5f; // Скорость поворота
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
        
        // Проверяем препятствия перед движением
        if (HasObstacleInDirection(direction))
        {
            // Не двигаемся если есть препятствие
            StopMovement();
            return;
        }
        
        // Вычисляем целевую позицию
        Vector3 targetPosition = transform.position + direction * config.moveSpeed * Time.deltaTime;
        
        // Проверяем можно ли двигаться в этом направлении
        Vector3Int targetVoxel = VoxelWorld.WorldToVoxel(targetPosition);
        Vector3Int currentVoxel = VoxelWorld.WorldToVoxel(transform.position);
        
        // Если нужно прыгать вверх
        if (targetVoxel.y > currentVoxel.y && pathfinding.CanJumpTo(currentVoxel, targetVoxel))
        {
            Vector3 jumpTarget = VoxelWorld.VoxelToWorld(targetVoxel);
            StartJump(jumpTarget);
        }
        else
        {
            // Обычное движение
            MoveTowardsTarget(targetPosition);
        }
    }
    
    /// <summary>
    /// Проверяет нужно ли прыгать к цели
    /// </summary>
    bool ShouldJumpToTarget(Vector3Int targetVoxel)
    {
        Vector3Int currentVoxel = VoxelWorld.WorldToVoxel(transform.position);
        
        // Проверяем что цель выше
        if (targetVoxel.y > currentVoxel.y)
        {
            return pathfinding.CanJumpTo(currentVoxel, targetVoxel);
        }
        
        return false;
    }
    
    /// <summary>
    /// Начинает прыжок
    /// </summary>
    void StartJump(Vector3 targetPosition)
    {
        if (isJumping) return;
        
        isJumping = true;
        jumpStartTime = Time.time;
        jumpStartPosition = transform.position;
        jumpTargetPosition = targetPosition;
        
        Debug.Log($"[VoxelBotController] Starting jump to {targetPosition}");
    }
    
    /// <summary>
    /// Обрабатывает прыжок
    /// </summary>
    void HandleJump()
    {
        if (!isJumping) return;
        
        float jumpTime = Time.time - jumpStartTime;
        float jumpDuration = Vector3.Distance(jumpStartPosition, jumpTargetPosition) / config.moveSpeed;
        
        if (jumpTime >= jumpDuration)
        {
            // Прыжок завершен - устанавливаем финальную позицию
            Vector3 finalPosition = jumpTargetPosition;
            
            // ИСПРАВЛЕНИЕ: Находим безопасную высоту для приземления относительно исходной цели
            Vector3Int targetVoxel = VoxelWorld.WorldToVoxel(jumpTargetPosition);
            float safeHeight = FindSafeLandingHeight(targetVoxel, jumpTargetPosition);
            
            if (safeHeight > 0)
            {
                finalPosition.y = safeHeight;
                transform.position = finalPosition;
                isJumping = false;
                Debug.Log($"[VoxelBotController] Jump completed safely to {finalPosition}");
            }
            else
            {
                // Не можем безопасно приземлиться - возвращаемся к стартовой позиции
                transform.position = jumpStartPosition;
                isJumping = false;
                Debug.LogWarning($"[VoxelBotController] Jump failed - no safe landing spot, returning to {jumpStartPosition}");
            }
            return;
        }
        
        // ИСПРАВЛЕНИЕ: Правильная интерполяция прыжка с учетом высоты цели
        float t = jumpTime / jumpDuration;
        
        // Интерполируем горизонтальную позицию
        Vector3 horizontalStart = new Vector3(jumpStartPosition.x, 0, jumpStartPosition.z);
        Vector3 horizontalTarget = new Vector3(jumpTargetPosition.x, 0, jumpTargetPosition.z);
        Vector3 horizontalPos = Vector3.Lerp(horizontalStart, horizontalTarget, t);
        
        // Интерполируем вертикальную позицию с дугой прыжка
        float verticalStart = jumpStartPosition.y;
        float verticalTarget = jumpTargetPosition.y;
        float verticalLerp = Mathf.Lerp(verticalStart, verticalTarget, t);
        
        // Добавляем дугу прыжка
        float jumpArc = Mathf.Sin(t * Mathf.PI) * config.jumpHeight;
        
        Vector3 currentPos = new Vector3(horizontalPos.x, verticalLerp + jumpArc, horizontalPos.z);
        transform.position = currentPos;
    }
    
    /// <summary>
    /// Проверяет находится ли бот на земле
    /// </summary>
    void CheckGrounded()
    {
        if (Time.time - lastGroundCheckTime < groundCheckInterval) return;
        lastGroundCheckTime = Time.time;
        
        Vector3 botPosition = transform.position;
        Vector3Int botVoxel = VoxelWorld.WorldToVoxel(botPosition);
        
        // Получаем размеры бота
        float botDiameter = config.botDiameter;
        int radiusBlocks = Mathf.CeilToInt(botDiameter / 2f);
        
        // Проверяем есть ли земля под ногами бота
        bool hasGround = false;
        for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
        {
            for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
            {
                Vector3Int groundPos = new Vector3Int(botVoxel.x + dx, botVoxel.y - 1, botVoxel.z + dz);
                if (VoxelWorld.IsVoxelSolid(groundPos))
                {
                    hasGround = true;
                    break;
                }
            }
            if (hasGround) break;
        }
        
        isGrounded = hasGround;
        
        // Если на земле - сбрасываем вертикальную скорость
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = 0;
        }
    }
    
    /// <summary>
    /// Применяет гравитацию к боту
    /// </summary>
    void ApplyGravity()
    {
        if (isJumping) return; // Во время прыжка гравитация не применяется
        
        if (!isGrounded)
        {
            // Применяем гравитацию
            velocity.y += config.gravity * Time.deltaTime;
            
            // Ограничиваем скорость падения
            velocity.y = Mathf.Max(velocity.y, -config.fallSpeed);
            
            // Применяем вертикальное движение
            Vector3 newPosition = transform.position + Vector3.up * velocity.y * Time.deltaTime;
            transform.position = newPosition;
        }
        else
        {
            // На земле - сбрасываем вертикальную скорость
            velocity.y = 0;
        }
    }
    
    /// <summary>
    /// Двигается к цели
    /// </summary>
    void MoveTowardsTarget(Vector3 targetPosition)
    {
        // ИСПРАВЛЕНИЕ: Плавное движение без телепортации
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // Только горизонтальное движение
        
        // Применяем горизонтальное движение
        Vector3 horizontalMovement = direction * config.moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + horizontalMovement;
        
        // ИСПРАВЛЕНИЕ: Проверяем поддерживается ли бот, а не принудительно ставим на поверхность
        if (isGrounded)
        {
            // Если бот на земле - проверяем не упадет ли он
            Vector3Int voxelPos = VoxelWorld.WorldToVoxel(newPosition);
            float surfaceHeight = GetSurfaceHeight(voxelPos);
            
            // Если поверхность ниже текущей позиции - бот может упасть
            if (surfaceHeight < transform.position.y - 0.5f)
            {
                // Бот упадет - не двигаемся горизонтально, пусть падает по гравитации
                return;
            }
            
            // Если поверхность выше - корректируем высоту плавно
            if (surfaceHeight > transform.position.y - 0.1f)
            {
                float targetHeight = surfaceHeight + config.botHeight * 0.5f;
                newPosition.y = Mathf.Lerp(transform.position.y, targetHeight, Time.deltaTime * 5f);
            }
        }
        
        // Проверяем столкновение с потолком
        if (IsHittingCeiling(newPosition))
        {
            // Если потолок слишком низко, останавливаемся
            return;
        }
        
        transform.position = newPosition;
    }
    
    /// <summary>
    /// Получает высоту поверхности под указанным вокселем
    /// </summary>
    float GetSurfaceHeight(Vector3Int voxelPos)
    {
        // Ищем самый верхний твердый блок под ботом
        for (int y = voxelPos.y; y >= 0; y--)
        {
            Vector3Int checkPos = new Vector3Int(voxelPos.x, y, voxelPos.z);
            if (VoxelWorld.IsVoxelSolid(checkPos))
            {
                // Нашли поверхность, возвращаем мировую координату
                return VoxelWorld.VoxelToWorld(checkPos).y;
            }
        }
        
        // Если не нашли поверхность, возвращаем 0
        return 0f;
    }
    
    /// <summary>
    /// Находит безопасную высоту для приземления относительно исходной цели
    /// </summary>
    float FindSafeLandingHeight(Vector3Int targetVoxel, Vector3 originalTarget)
    {
        // Получаем размеры бота
        float botDiameter = config.botDiameter;
        float botHeight = config.botHeight;
        
        // Вычисляем количество блоков для проверки
        int radiusBlocks = Mathf.CeilToInt(botDiameter / 2f);
        int heightBlocks = Mathf.CeilToInt(botHeight);
        
        // ИСПРАВЛЕНИЕ: Начинаем поиск от исходной высоты цели
        int originalY = Mathf.RoundToInt(originalTarget.y);
        int searchRange = 5; // Ищем в диапазоне ±5 блоков от цели
        
        // Ищем безопасную высоту сначала близко к цели, потом дальше
        for (int offset = 0; offset <= searchRange; offset++)
        {
            // Проверяем сначала вверх, потом вниз от исходной цели
            int[] yChecks = { originalY + offset, originalY - offset };
            
            foreach (int y in yChecks)
            {
                if (y < 0 || y >= VoxelWorld.Instance.GetWorldHeight()) continue;
                
                // Проверяем что в этой позиции нет блоков (можем стоять)
                bool canStand = true;
                for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
                {
                    for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
                    {
                        for (int dy = 0; dy <= heightBlocks; dy++)
                        {
                            Vector3Int checkPos = new Vector3Int(targetVoxel.x + dx, y + dy, targetVoxel.z + dz);
                            if (VoxelWorld.IsVoxelSolid(checkPos))
                            {
                                canStand = false;
                                break;
                            }
                        }
                        if (!canStand) break;
                    }
                    if (!canStand) break;
                }
                
                if (!canStand) continue;
                
                // Проверяем что есть земля под ногами
                bool hasGround = false;
                for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
                {
                    for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
                    {
                        Vector3Int groundPos = new Vector3Int(targetVoxel.x + dx, y - 1, targetVoxel.z + dz);
                        if (VoxelWorld.IsVoxelSolid(groundPos))
                        {
                            hasGround = true;
                            break;
                        }
                    }
                    if (hasGround) break;
                }
                
                if (hasGround)
                {
                    // ИСПРАВЛЕНИЕ: Возвращаем высоту центра бота (ноги на поверхности)
                    return y + 0.5f;
                }
            }
        }
        
        return -1f; // Не нашли безопасную позицию
    }
    
    /// <summary>
    /// Проверяет есть ли препятствие в направлении движения
    /// </summary>
    bool HasObstacleInDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return false;
        
        Vector3 currentPos = transform.position;
        Vector3 checkDirection = direction.normalized;
        
        // Получаем размеры бота из конфига
        float botDiameter = config.botDiameter;
        float botHeight = config.botHeight;
        
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
                    Vector3 checkPos = currentPos + offset + checkDirection * 1.2f; // 1.2 блока вперед
                    
                    // Проверяем есть ли препятствие в этой позиции
                    Vector3Int checkVoxel = VoxelWorld.WorldToVoxel(checkPos);
                    if (VoxelWorld.IsVoxelSolid(checkVoxel))
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Останавливает движение
    /// </summary>
    void StopMovement()
    {
        isMoving = false;
        currentPath.Clear();
        currentPathIndex = 0;
    }
    
    /// <summary>
    /// Обновляет анимацию бота
    /// </summary>
    void UpdateAnimation()
    {
        if (animator == null) return;
        
        // Вычисляем скорость движения (только горизонтальную)
        float speed = currentVelocity.magnitude;
        bool isGrounded = IsGrounded();
        
        // Нормализуем скорость как у игрока (0 = стоит, 1 = максимальная скорость)
        float normalizedSpeed = Mathf.Clamp01(speed / config.moveSpeed);
        
        // Устанавливаем параметры аниматора
        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsGrounded", isGrounded);
    }
    
    /// <summary>
    /// Проверяет касание земли
    /// </summary>
    bool IsGrounded()
    {
        // Ноги бота находятся на высоте: transform.position.y - config.botHeight * 0.5f
        float footHeight = transform.position.y - config.botHeight * 0.5f;
        Vector3 footPosition = new Vector3(transform.position.x, footHeight, transform.position.z);
        
        // Проверяем воксель под ногами
        Vector3Int footVoxel = VoxelWorld.WorldToVoxel(footPosition);
        bool isGrounded = VoxelWorld.IsVoxelSolid(footVoxel);
        
        return isGrounded;
    }
    
    /// <summary>
    /// Проверяет столкновение с потолком
    /// </summary>
    bool IsHittingCeiling(Vector3 position)
    {
        Vector3Int aboveVoxel = VoxelWorld.WorldToVoxel(position + Vector3.up * config.botHeight);
        return VoxelWorld.IsVoxelSolid(aboveVoxel);
    }
    
    /// <summary>
    /// Получает текущую высоту над вокселем
    /// </summary>
    float GetCurrentHeight()
    {
        Vector3Int voxelPos = VoxelWorld.WorldToVoxel(transform.position);
        Vector3 voxelWorldPos = VoxelWorld.VoxelToWorld(voxelPos);
        return transform.position.y - voxelWorldPos.y;
    }
    
    /// <summary>
    /// Устанавливает цель
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasTarget = newTarget != null;
        
        if (hasTarget)
        {
            Debug.Log($"[VoxelBotController] Target set: {newTarget.name}");
        }
    }
    
    /// <summary>
    /// Команда движения от ИИ
    /// </summary>
    public void MoveToTarget(Vector3 moveDirection, bool shouldJump)
    {
        aiMoveDirection = moveDirection;
        aiShouldJump = shouldJump;
        isAIControlled = true;
    }
    
    /// <summary>
    /// Отключает управление ИИ (возвращает к старому режиму)
    /// </summary>
    public void DisableAIControl()
    {
        isAIControlled = false;
        aiMoveDirection = Vector3.zero;
        aiShouldJump = false;
    }
    
    /// <summary>
    /// Включает управление ИИ
    /// </summary>
    public void EnableAIControl()
    {
        isAIControlled = true;
    }
    
    /// <summary>
    /// Получает данные бота
    /// </summary>
    public VoxelBotData GetBotData()
    {
        return botData;
    }
    
    /// <summary>
    /// Проверяет попадание в бота
    /// </summary>
    public bool CheckHit(Vector3 hitPoint, float radius)
    {
        return botData.bounds.Contains(hitPoint) || 
               Vector3.Distance(hitPoint, botData.bounds.ClosestPoint(hitPoint)) <= radius;
    }
}
