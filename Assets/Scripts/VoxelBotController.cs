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
    
    // Состояние ИИ
    private bool hasTarget = false;
    private float lastPathUpdateTime = 0f;
    
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
        
        if (hasTarget && target != null)
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
        else
        {
            // Останавливаемся если нет цели
            StopMovement();
        }
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
            // Находим высоту поверхности под целью
            Vector3Int targetVoxel = VoxelWorld.WorldToVoxel(jumpTargetPosition);
            float surfaceHeight = GetSurfaceHeight(targetVoxel);
            finalPosition.y = surfaceHeight + config.botHeight * 0.5f;
            
            transform.position = finalPosition;
            isJumping = false;
            Debug.Log($"[VoxelBotController] Jump completed to {finalPosition}");
            return;
        }
        
        // Интерполируем горизонтальную позицию
        float t = jumpTime / jumpDuration;
        Vector3 horizontalStart = new Vector3(jumpStartPosition.x, 0, jumpStartPosition.z);
        Vector3 horizontalTarget = new Vector3(jumpTargetPosition.x, 0, jumpTargetPosition.z);
        Vector3 currentPos = Vector3.Lerp(horizontalStart, horizontalTarget, t);
        
        // Добавляем дугу прыжка
        float jumpArc = Mathf.Sin(t * Mathf.PI) * config.jumpHeight;
        currentPos.y = jumpStartPosition.y + jumpArc;
        
        transform.position = currentPos;
    }
    
    /// <summary>
    /// Двигается к цели
    /// </summary>
    void MoveTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        Vector3 newPosition = transform.position + direction * config.moveSpeed * Time.deltaTime;
        
        // ВАЖНО: Боты должны ходить по поверхности вокселей!
        // Находим высоту поверхности под ботом
        Vector3Int voxelPos = VoxelWorld.WorldToVoxel(newPosition);
        float surfaceHeight = GetSurfaceHeight(voxelPos);
        
        // Устанавливаем Y позицию на поверхность
        newPosition.y = surfaceHeight + config.botHeight * 0.5f;
        
        // Проверяем столкновение с потолком
        if (IsHittingCeiling(newPosition))
        {
            // Если потолок слишком низко, останавливаемся
            newPosition = transform.position;
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
