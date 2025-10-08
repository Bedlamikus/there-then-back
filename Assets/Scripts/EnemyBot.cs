using UnityEngine;

/// <summary>
/// Враг-бот на базе PlayerController
/// Использует CharacterController и те же механики движения, но с AI логикой
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyBot : MonoBehaviour, ISpawnable
{
    [Header("Bot ID")]
    [Tooltip("Уникальный ID бота для системы спавна")]
    public string botID;
    [Header("Target")]
    [Tooltip("Цель для преследования (обычно игрок)")]
    public Transform target;
    
    [Tooltip("Автоматически искать игрока как цель")]
    public bool autoFindPlayer = true;

    [Header("Movement")]
    public float moveSpeed = 4f;                 // Скорость движения (немного медленнее игрока)
    public float turnSpeed = 5f;                 // Скорость поворота
    public float turnThreshold = 0.1f;           // Минимальная скорость для поворота

    [Header("AI Behavior")]
    public float detectionRange = 20f;           // Дальность обнаружения цели
    public float attackRange = 2f;               // Дальность атаки
    public float patrolRadius = 10f;             // Радиус патрулирования
    public float patrolWaitTime = 2f;            // Время ожидания на точке патрулирования
    public float minPatrolDistance = 3f;         // Минимальная дистанция до следующей точки патруля

    [Header("Jump")]
    public float jumpHeight = 3.5f;              // Высота прыжка
    public float coyoteTime = 0.1f;              // Время-призрак для прыжка
    public bool canJump = true;                  // Может ли бот прыгать
    
    [Header("Pathfinding")]
    public bool usePathfinding = true;           // Использовать поиск пути
    public float pathUpdateInterval = 1f;        // Интервал обновления пути (секунды)
    public int maxPathLength = 50;               // Максимальная длина пути
    public float waypointReachDistance = 1.5f;   // Дистанция достижения точки пути
    public float stuckCheckTime = 2f;            // Время для проверки застревания
    public float stuckDistanceThreshold = 1f;    // Минимальное расстояние для незастревания
    public float stuckAreaSize = 4f;             // Размер области застревания (4x4x4)
    public int maxStuckAttempts = 3;             // Максимум попыток выбраться
    public float unstuckPatrolTime = 5f;         // Время патруля после застревания

    [Header("Gravity")]
    public float gravity = -9.81f;               // Сила гравитации
    public float groundCheckDistance = 0.2f;     // Расстояние проверки земли

    [Header("Animation")]
    public Animator animator;                    // Аниматор
    public string speedParameter = "Speed";      // Параметр скорости
    public string isGroundedParameter = "IsGrounded";

    // Состояния AI
    public enum AIState
    {
        Idle,           // Стоит на месте
        Patrol,         // Патрулирует
        Chase,          // Преследует цель
        Attack          // Атакует
    }

    private AIState currentState = AIState.Idle;
    private CharacterController controller;
    private Vector3 _velocity;
    private bool _grounded;
    private float _lastGroundTime;
    private bool _jumpPressed;
    
    // AI переменные
    private Vector3 startPosition;              // Стартовая позиция для патруля
    private Vector3 patrolTarget;               // Текущая точка патруля
    private float patrolWaitTimer = 0f;
    private bool isWaiting = false;
    
    // Pathfinding
    private VoxelWorld voxelWorld;
    private System.Collections.Generic.List<Vector3> currentPath;
    private int currentWaypointIndex = 0;
    private Coroutine pathfindingCoroutine;
    private float lastPathUpdateTime = 0f;
    
    // Проверка застревания
    private Vector3 stuckCheckPosition;
    private float stuckCheckTimer = 0f;
    private int stuckAttempts = 0;
    private bool isInStuckArea = false;
    private Vector3 stuckAreaCenter;
    private float unstuckPatrolTimer = 0f;
    private AIState stateBeforeUnstuck;
    private bool isRecoveringFromStuck = false;
    
    // Анимация
    private float _currentSpeed;
    private Vector3 _lastPosition;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        _lastPosition = transform.position;
        startPosition = transform.position;
        stuckCheckPosition = transform.position;
        
        // Генерируем уникальный ID если не задан
        if (string.IsNullOrEmpty(botID))
        {
            botID = $"Bot_{GetInstanceID()}_{Random.Range(1000, 9999)}";
            Debug.Log($"EnemyBot [{name}]: Сгенерирован ID: {botID}");
        }
        
        // Поиск VoxelWorld для pathfinding - ищем по скрипту, а не по Instance
        voxelWorld = FindObjectOfType<VoxelWorld>();
        if (voxelWorld == null)
        {
            Debug.LogWarning($"EnemyBot [{name}]: VoxelWorld не найден! Pathfinding отключен.");
            usePathfinding = false;
        }
        
        // Автопоиск игрока
        if (autoFindPlayer && target == null)
        {
            FindPlayer();
        }
        
        // Регистрируем бота в AutoSpawnService
        AutoSpawnService.Instance?.RegisterSpawnable(this);
        
        // Начинаем с патрулирования
        currentState = AIState.Patrol;
        SetNewPatrolTarget();
    }

    void Update()
    {
        // AI логика
        UpdateAI();
        
        // Проверка застревания
        CheckIfStuck();
        
        // Движение
        HandleMovement();
        
        // Анимация
        UpdateAnimation();
        
        // Обновление сервиса автоспавна
        AutoSpawnService.Instance?.TickSpawnable(this, Time.deltaTime);
    }

    void UpdateAI()
    {
        // Проверяем дистанцию до цели
        float distanceToTarget = target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
        
        switch (currentState)
        {
            case AIState.Idle:
                // Переход к патрулю через некоторое время
                patrolWaitTimer += Time.deltaTime;
                if (patrolWaitTimer >= patrolWaitTime)
                {
                    currentState = AIState.Patrol;
                    SetNewPatrolTarget();
                }
                
                // Проверка обнаружения цели
                if (distanceToTarget <= detectionRange)
                {
                    currentState = AIState.Chase;
                }
                break;
                
            case AIState.Patrol:
                // Проверка обнаружения цели
                if (distanceToTarget <= detectionRange)
                {
                    currentState = AIState.Chase;
                    isWaiting = false;
                    break;
                }
                
                // Патрулирование
                if (!isWaiting)
                {
                    float distanceToPatrol = Vector3.Distance(transform.position, patrolTarget);
                    
                    if (distanceToPatrol < 1f)
                    {
                        // Достигли точки патруля - ждем
                        isWaiting = true;
                        patrolWaitTimer = 0f;
                    }
                }
                else
                {
                    // Ожидание на точке
                    patrolWaitTimer += Time.deltaTime;
                    if (patrolWaitTimer >= patrolWaitTime)
                    {
                        SetNewPatrolTarget();
                        isWaiting = false;
                    }
                }
                break;
                
            case AIState.Chase:
                // Преследование цели
                if (distanceToTarget > detectionRange * 1.5f)
                {
                    // Цель далеко - возвращаемся к патрулю
                    currentState = AIState.Patrol;
                    StopPathfinding();
                    SetNewPatrolTarget();
                }
                else if (distanceToTarget <= attackRange)
                {
                    // Достаточно близко для атаки
                    currentState = AIState.Attack;
                    StopPathfinding();
                }
                else
                {
                    // Обновляем путь к цели через интервал
                    if (usePathfinding && Time.time - lastPathUpdateTime >= pathUpdateInterval)
                    {
                        StartPathfindingToTarget();
                        lastPathUpdateTime = Time.time;
                    }
                }
                break;
                
            case AIState.Attack:
                // Атака
                if (distanceToTarget > attackRange * 1.5f)
                {
                    // Цель отошла - преследуем
                    currentState = AIState.Chase;
                }
                // TODO: Логика атаки
                break;
        }
    }

    void HandleMovement()
    {
        float dt = Time.deltaTime;
        
        // Проверка земли
        GroundCheck();
        
        // Обработка прыжка
        if (_jumpPressed && canJump)
        {
            if (_grounded || Time.time - _lastGroundTime <= coyoteTime)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _grounded = false;
            }
            _jumpPressed = false;
        }
        
        // Вычисляем направление движения на основе состояния AI
        Vector3 moveDirection = GetMoveDirection();
        
        // Плавный поворот к направлению движения
        if (moveDirection.sqrMagnitude > turnThreshold)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * dt);
        }
        
        // Применение гравитации
        if (_grounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        else
        {
            _velocity.y += gravity * dt;
        }
        
        // Движение
        Vector3 move = moveDirection + Vector3.up * _velocity.y;
        controller.Move(move * dt);
    }

    Vector3 GetMoveDirection()
    {
        Vector3 direction = Vector3.zero;
        
        switch (currentState)
        {
            case AIState.Idle:
                // Не двигаемся
                return Vector3.zero;
                
            case AIState.Patrol:
                if (!isWaiting)
                {
                    // Двигаемся к точке патруля
                    direction = (patrolTarget - transform.position);
                    direction.y = 0;
                    direction = direction.normalized * moveSpeed;
                }
                break;
                
            case AIState.Chase:
                // Используем pathfinding если доступен
                if (usePathfinding && currentPath != null && currentPath.Count > 0)
                {
                    direction = FollowPath();
                }
                else if (target != null)
                {
                    // Простое движение к цели без pathfinding
                    direction = (target.position - transform.position);
                    direction.y = 0;
                    direction = direction.normalized * moveSpeed;
                }
                break;
                
            case AIState.Attack:
                // Стоим на месте при атаке (или можно добавить небольшое движение)
                return Vector3.zero;
        }
        
        return direction;
    }
    
    Vector3 FollowPath()
    {
        if (currentPath == null || currentWaypointIndex >= currentPath.Count)
            return Vector3.zero;
        
        Vector3 currentWaypoint = currentPath[currentWaypointIndex];
        Vector3 directionToWaypoint = currentWaypoint - transform.position;
        float distanceToWaypoint = directionToWaypoint.magnitude;
        
        // Достигли текущей точки пути - переходим к следующей
        if (distanceToWaypoint < waypointReachDistance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= currentPath.Count)
            {
                // Путь пройден
                currentPath = null;
                return Vector3.zero;
            }
            currentWaypoint = currentPath[currentWaypointIndex];
            directionToWaypoint = currentWaypoint - transform.position;
        }
        
        // Возвращаем направление к текущей точке пути
        directionToWaypoint.y = 0;
        return directionToWaypoint.normalized * moveSpeed;
    }

    void SetNewPatrolTarget()
    {
        // Генерируем случайную точку в радиусе патрулирования
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        Vector3 randomPoint = startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // Проверяем, что точка достаточно далеко
        if (Vector3.Distance(transform.position, randomPoint) < minPatrolDistance)
        {
            // Если слишком близко, увеличиваем расстояние
            Vector3 direction = (randomPoint - transform.position).normalized;
            randomPoint = transform.position + direction * minPatrolDistance;
        }
        
        patrolTarget = randomPoint;
    }

    void GroundCheck()
    {
        if (controller.isGrounded)
        {
            _grounded = true;
            _lastGroundTime = Time.time;
        }
        else
        {
            _grounded = false;
        }
    }

    void UpdateAnimation()
    {
        // Вычисляем текущую скорость
        Vector3 horizontalVelocity = transform.position - _lastPosition;
        horizontalVelocity.y = 0;
        _currentSpeed = horizontalVelocity.magnitude / Time.deltaTime;
        
        _lastPosition = transform.position;
        
        // Обновляем параметры аниматора
        if (animator != null)
        {
            animator.SetFloat(speedParameter, _currentSpeed);
            animator.SetBool(isGroundedParameter, _grounded);
        }
    }

    void FindPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
            Debug.Log($"EnemyBot [{name}]: Нашел игрока: {target.name}");
        }
    }
    
    // ========== PATHFINDING ==========
    
    void StartPathfindingToTarget()
    {
        if (target == null || voxelWorld == null) return;
        
        // Останавливаем предыдущий поиск пути
        if (pathfindingCoroutine != null)
        {
            StopCoroutine(pathfindingCoroutine);
        }
        
        // Запускаем новый поиск пути
        pathfindingCoroutine = StartCoroutine(FindPathCoroutine(transform.position, target.position));
    }
    
    void StopPathfinding()
    {
        if (pathfindingCoroutine != null)
        {
            StopCoroutine(pathfindingCoroutine);
            pathfindingCoroutine = null;
        }
        currentPath = null;
        currentWaypointIndex = 0;
    }
    
    System.Collections.IEnumerator FindPathCoroutine(Vector3 start, Vector3 end)
    {
        //Debug.Log($"EnemyBot [{name}]: Начинаем поиск пути от {start} к {end}");
        
        // Конвертируем в блочные координаты
        Vector3Int startBlock = WorldToBlock(start);
        Vector3Int endBlock = WorldToBlock(end);
        
        // Простой поиск пути (жадный алгоритм с учетом прыжков)
        var path = new System.Collections.Generic.List<Vector3>();
        Vector3Int current = startBlock;
        int iterations = 0;
        
        while (iterations < maxPathLength)
        {
            iterations++;
            
            // Достигли цели
            if (Vector3Int.Distance(current, endBlock) < 2)
            {
                path.Add(BlockToWorld(endBlock));
                break;
            }
            
            // Находим лучшее направление к цели
            Vector3Int direction = endBlock - current;
            Vector3Int nextStep = GetBestStep(current, direction);
            
            if (nextStep == current)
            {
                // Не можем двигаться дальше
                //Debug.LogWarning($"EnemyBot [{name}]: Не могу найти путь к цели");
                break;
            }
            
            current = nextStep;
            path.Add(BlockToWorld(current));
            
            // Даем другим системам поработать (каждые 5 итераций)
            if (iterations % 5 == 0)
            {
                yield return null;
            }
        }
        
        // Устанавливаем найденный путь
        if (path.Count > 0)
        {
            currentPath = path;
            currentWaypointIndex = 0;
            //Debug.Log($"EnemyBot [{name}]: Путь найден, {path.Count} точек");
        }
        else
        {
            currentPath = null;
            //Debug.LogWarning($"EnemyBot [{name}]: Путь не найден");
        }
        
        pathfindingCoroutine = null;
    }
    
    Vector3Int GetBestStep(Vector3Int current, Vector3Int direction)
    {
        // Нормализуем направление для пошагового движения
        Vector3Int stepDir = new Vector3Int(
            direction.x != 0 ? (int)Mathf.Sign(direction.x) : 0,
            0,
            direction.z != 0 ? (int)Mathf.Sign(direction.z) : 0
        );
        
        // Пробуем разные варианты движения с приоритетом
        Vector3Int[] candidates = new Vector3Int[]
        {
            // Прямой путь
            current + stepDir,                                      // Прямо к цели
            current + new Vector3Int(stepDir.x, 0, 0),             // По X
            current + new Vector3Int(0, 0, stepDir.z),             // По Z
            
            // Прыжки
            current + stepDir + Vector3Int.up,                     // Прямо к цели с прыжком
            current + new Vector3Int(stepDir.x, 0, 0) + Vector3Int.up, // По X с прыжком
            current + new Vector3Int(0, 0, stepDir.z) + Vector3Int.up, // По Z с прыжком
            
            // Обход (диагонали)
            current + new Vector3Int(stepDir.x, 0, -stepDir.z),    // Диагональ 1
            current + new Vector3Int(-stepDir.x, 0, stepDir.z),    // Диагональ 2
            
            // Обход с прыжком
            current + new Vector3Int(stepDir.x, 0, -stepDir.z) + Vector3Int.up,
            current + new Vector3Int(-stepDir.x, 0, stepDir.z) + Vector3Int.up,
            
            // Назад с отступом (если совсем застряли)
            current + new Vector3Int(-stepDir.x, 0, 0),
            current + new Vector3Int(0, 0, -stepDir.z),
        };
        
        Vector3Int bestCandidate = current;
        float bestDistance = float.MaxValue;
        
        foreach (var candidate in candidates)
        {
            if (IsWalkable(candidate))
            {
                float distance = Vector3Int.Distance(candidate, current + direction);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCandidate = candidate;
                }
            }
        }
        
        return bestCandidate;
    }
    
    bool IsWalkable(Vector3Int blockPos)
    {
        if (voxelWorld == null) return false;
        
        // Проверяем что позиция в границах мира
        if (blockPos.y < 0 || blockPos.y >= VoxelChunk16.HEIGHT) return false;
        
        // Проверяем что текущая позиция свободна (для тела бота - 3 блока высоты)
        if (voxelWorld.HasBlockAt(blockPos.x, blockPos.y, blockPos.z)) return false;
        if (voxelWorld.HasBlockAt(blockPos.x, blockPos.y + 1, blockPos.z)) return false;
        if (voxelWorld.HasBlockAt(blockPos.x, blockPos.y + 2, blockPos.z)) return false;
        
        // Проверяем что под ногами есть твердый блок (всегда, даже для Y=0)
        if (blockPos.y == 0)
        {
            // На уровне 0 должен быть блок под нами (bedrock)
            return true;
        }
        
        if (!voxelWorld.HasBlockAt(blockPos.x, blockPos.y - 1, blockPos.z))
        {
            // Нет блока под ногами - нельзя идти
            return false;
        }
        
        return true;
    }
    
    Vector3Int WorldToBlock(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y),
            Mathf.FloorToInt(worldPos.z)
        );
    }
    
    Vector3 BlockToWorld(Vector3Int blockPos)
    {
        return new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
    }
    
    // ========== ПРОВЕРКА ЗАСТРЕВАНИЯ ==========
    
    void CheckIfStuck()
    {
        // Если восстанавливаемся от застревания - ведем обратный отсчет
        if (isRecoveringFromStuck)
        {
            unstuckPatrolTimer += Time.deltaTime;
            
            if (unstuckPatrolTimer >= unstuckPatrolTime)
            {
                // Время патруля истекло - можем снова искать игрока
                Debug.Log($"EnemyBot [{name}]: Восстановление от застревания завершено, возвращаемся к {stateBeforeUnstuck}");
                isRecoveringFromStuck = false;
                currentState = stateBeforeUnstuck;
                stuckAttempts = 0;
                isInStuckArea = false;
                
                // Если были в погоне - обновляем путь
                if (currentState == AIState.Chase && target != null)
                {
                    StartPathfindingToTarget();
                }
            }
            return;
        }
        
        // Проверяем застревание только в состоянии преследования
        if (currentState != AIState.Chase) return;
        
        stuckCheckTimer += Time.deltaTime;
        
        if (stuckCheckTimer >= stuckCheckTime)
        {
            Vector3 currentPos = transform.position;
            float distanceMoved = Vector3.Distance(currentPos, stuckCheckPosition);
            
            // Проверяем, находимся ли мы в той же области 4x4x4
            if (!isInStuckArea)
            {
                // Первая проверка - запоминаем центр области
                stuckAreaCenter = currentPos;
                isInStuckArea = true;
            }
            else
            {
                // Проверяем, вышли ли мы за пределы области 4x4x4
                Vector3 offset = currentPos - stuckAreaCenter;
                bool stillInArea = Mathf.Abs(offset.x) <= stuckAreaSize * 0.5f && 
                                   Mathf.Abs(offset.y) <= stuckAreaSize * 0.5f && 
                                   Mathf.Abs(offset.z) <= stuckAreaSize * 0.5f;
                
                if (!stillInArea)
                {
                    // Вышли из области застревания - сбрасываем счетчик
                    Debug.Log($"EnemyBot [{name}]: Вышел из области застревания");
                    stuckAttempts = 0;
                    isInStuckArea = false;
                    stuckAreaCenter = currentPos;
                }
            }
            
            // Проверяем, двигались ли мы
            if (distanceMoved < stuckDistanceThreshold)
            {
                stuckAttempts++;
                Debug.Log($"EnemyBot [{name}]: Застрял! Попытка {stuckAttempts}/{maxStuckAttempts}, перемещение: {distanceMoved:F2}");
                
                if (stuckAttempts == 1)
                {
                    // Первая попытка - прыгаем
                    _jumpPressed = true;
                    Debug.Log($"EnemyBot [{name}]: Пытаюсь прыгнуть через препятствие");
                }
                else if (stuckAttempts == 2)
                {
                    // Вторая попытка - ищем новый путь
                    Debug.Log($"EnemyBot [{name}]: Прыжок не помог, ищу альтернативный путь");
                    StopPathfinding();
                    StartPathfindingToTarget();
                }
                else if (stuckAttempts >= maxStuckAttempts)
                {
                    // Третья попытка не удалась - переходим к патрулю на время
                    Debug.LogWarning($"EnemyBot [{name}]: Не могу выбраться! Переключаюсь на патруль на {unstuckPatrolTime} секунд");
                    
                    stateBeforeUnstuck = AIState.Chase;
                    currentState = AIState.Patrol;
                    isRecoveringFromStuck = true;
                    unstuckPatrolTimer = 0f;
                    
                    StopPathfinding();
                    SetNewPatrolTarget();
                    
                    stuckAttempts = 0;
                    isInStuckArea = false;
                }
            }
            else
            {
                // Двигаемся нормально - сбрасываем счетчик попыток
                if (stuckAttempts > 0)
                {
                    Debug.Log($"EnemyBot [{name}]: Снова двигаюсь, сброс счетчика застревания");
                }
                stuckAttempts = 0;
            }
            
            stuckCheckPosition = currentPos;
            stuckCheckTimer = 0f;
        }
    }

    // Публичные методы для внешнего управления
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public AIState GetCurrentState()
    {
        return currentState;
    }
    
    public void ForceState(AIState newState)
    {
        currentState = newState;
        if (newState == AIState.Patrol)
        {
            SetNewPatrolTarget();
        }
    }

    // Визуализация в редакторе
    void OnDrawGizmosSelected()
    {
        Vector3 pos = Application.isPlaying ? transform.position : transform.position;
        Vector3 start = Application.isPlaying ? startPosition : transform.position;
        
        // Радиус обнаружения
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, detectionRange);
        
        // Радиус атаки
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, attackRange);
        
        // Радиус патрулирования
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(start, patrolRadius);
        
        // Текущая точка патруля
        if (Application.isPlaying && currentState == AIState.Patrol)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(patrolTarget, 0.5f);
            Gizmos.DrawLine(pos, patrolTarget);
        }
        
        // Линия к цели
        if (Application.isPlaying && target != null && (currentState == AIState.Chase || currentState == AIState.Attack))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, target.position);
        }
        
        // Визуализация пути (Pathfinding)
        if (Application.isPlaying && currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            
            // Рисуем точки пути
            foreach (var waypoint in currentPath)
            {
                Gizmos.DrawWireSphere(waypoint, 0.3f);
            }
            
            // Рисуем линии между точками
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }
            
            // Линия от бота к первой точке пути
            if (currentPath.Count > 0)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(pos, currentPath[0]);
            }
            
            // Текущая целевая точка (зеленая)
            if (currentWaypointIndex < currentPath.Count)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(currentPath[currentWaypointIndex], 0.5f);
            }
        }
    }
    
    // ========== ISpawnable Implementation ==========
    
    public string GetSpawnableID()
    {
        return botID;
    }
    
    public Transform GetTransform()
    {
        return transform;
    }
    
    public GameObject GetGameObject()
    {
        return gameObject;
    }
    
    public bool IsGrounded()
    {
        return _grounded;
    }
    
    void OnDestroy()
    {
        // Отписываемся от AutoSpawnService при уничтожении
        AutoSpawnService.Instance?.UnregisterSpawnable(this);
    }
}

