using UnityEngine;

public enum AIState
{
    Idle,
    Patrol,
    Chase,
    Attack
}

public class EnemyAIStateMachine
{
    private Transform transform;
    private EnemyAIConfig config;
    private EnemyPathfindingService pathfindingService;
    private EnemyMovementService movementService;
    private ISpawnable spawnable;
    
    // Состояние ИИ
    private AIState currentState = AIState.Patrol;
    private Transform target;
    private Vector3 startPosition;
    private Vector3 patrolTarget;
    private Vector3 positionBeforeChase;
    private Vector3 lastSeenTargetPosition;
    
    // Ссылка на EnemyBot для получения информации о снарядах
    private EnemyBot enemyBot;
    
    // Патрулирование
    private bool isWaiting = false;
    private float patrolWaitTimer = 0f;
    private int patrolPointsVisited = 0;
    private int patrolPointsBeforeRest;
    private bool isReturningToStart = false;
    
    // Отдых
    private float idleTimer = 0f;
    private float currentIdleDuration;
    
    // Падение
    private float fallingTimer = 0f;
    private float lastYPosition = 0f;
    
    public AIState CurrentState => currentState;
    public Transform Target => target;
    public Vector3 PatrolTarget => patrolTarget;
    public bool IsWaiting => isWaiting;
    public bool IsReturningToStart => isReturningToStart;
    
    public EnemyAIStateMachine(Transform transform, EnemyAIConfig config, EnemyPathfindingService pathfindingService, EnemyMovementService movementService, ISpawnable spawnable)
    {
        this.transform = transform;
        this.config = config;
        this.pathfindingService = pathfindingService;
        this.movementService = movementService;
        this.spawnable = spawnable;
        
        // Получаем ссылку на EnemyBot
        this.enemyBot = spawnable as EnemyBot;
        
        startPosition = transform.position;
        lastYPosition = transform.position.y;
        patrolPointsBeforeRest = Random.Range(config.minPatrolPointsBeforeRest, config.maxPatrolPointsBeforeRest + 1);
        
        // ВАЖНО: Устанавливаем первую точку патруля сразу
        SetNewPatrolTarget();
    }
    
    public void Update()
    {
        CheckFalling();
        UpdateAI();
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetPatrolTarget(Vector3 newPatrolTarget)
    {
        patrolTarget = newPatrolTarget;
    }
    
    public Vector3 GetMoveDirection()
    {
        switch (currentState)
        {
            case AIState.Idle:
                return Vector3.zero;
                
            case AIState.Patrol:
                return GetPatrolMoveDirection();
                
            case AIState.Chase:
                return GetChaseMoveDirection();
                
            case AIState.Attack:
                return Vector3.zero;
                
            default:
                return Vector3.zero;
        }
    }
    
    public bool ShouldJump()
    {
        // Проверяем нужен ли прыжок для выхода из застревания
        return pathfindingService.ShouldJump;
    }
    
    private void ChangeState(AIState newState, string reason = "")
    {
        if (currentState == newState) return;
        
        AIState oldState = currentState;
        currentState = newState;
        
        // При переходе в Idle сбрасываем обнаружение застревания
        if (newState == AIState.Idle)
        {
            pathfindingService.ResetStuckDetection();
        }
    }
    
    private void UpdateAI()
    {
        // Для обнаружения и преследования игрока используем полное 3D расстояние
        float distanceToTarget = target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
        
        // Также вычисляем горизонтальное расстояние для дополнительных проверок
        float horizontalDistanceToTarget = float.MaxValue;
        if (target != null)
        {
            Vector3 botPosFlat = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetPosFlat = new Vector3(target.position.x, 0, target.position.z);
            horizontalDistanceToTarget = Vector3.Distance(botPosFlat, targetPosFlat);
        }
        
        switch (currentState)
        {
            case AIState.Idle:
                HandleIdleState(distanceToTarget);
                break;
                
            case AIState.Patrol:
                HandlePatrolState(distanceToTarget);
                break;
                
            case AIState.Chase:
                HandleChaseState(distanceToTarget, horizontalDistanceToTarget);
                break;
                
            case AIState.Attack:
                HandleAttackState(distanceToTarget, horizontalDistanceToTarget);
                break;
        }
    }
    
    private void HandleIdleState(float distanceToTarget)
    {
        idleTimer += Time.deltaTime;
        
        if (idleTimer >= currentIdleDuration)
        {
            ChangeState(AIState.Patrol, $"Отдых завершен ({idleTimer:F1}s)");
            patrolPointsVisited = 0;
            patrolPointsBeforeRest = Random.Range(config.minPatrolPointsBeforeRest, config.maxPatrolPointsBeforeRest + 1);
            SetNewPatrolTarget();
        }
        
        // Проверка обнаружения цели
        if (target != null && distanceToTarget <= config.detectionRange)
        {
            positionBeforeChase = transform.position;
            ChangeState(AIState.Chase, $"Обнаружена цель на расстоянии {distanceToTarget:F1}m");
            idleTimer = 0f;
        }
    }
    
    private void HandlePatrolState(float distanceToTarget)
    {
        // Проверяем завершилось ли восстановление от застревания
        if (pathfindingService.JustFinishedRecovery)
        {
            SetNewPatrolTarget();
        }
        
        if (isReturningToStart)
        {
            float distanceToReturn = Vector3.Distance(transform.position, positionBeforeChase);
            
            if (distanceToReturn < 2f)
            {
                isReturningToStart = false;
                startPosition = positionBeforeChase;
                SetNewPatrolTarget();
            }
        }
        
        // Проверка обнаружения цели
        if (target != null && distanceToTarget <= config.detectionRange)
        {
            positionBeforeChase = transform.position;
            ChangeState(AIState.Chase, $"Обнаружена цель на расстоянии {distanceToTarget:F1}m");
            isWaiting = false;
            isReturningToStart = false;
            patrolPointsVisited = 0;
        }
        
        if (!isWaiting)
        {
            // ВАЖНО: Проверяем только горизонтальное расстояние (X, Z)
            Vector3 currentPosFlat = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetPosFlat = new Vector3(patrolTarget.x, 0, patrolTarget.z);
            float horizontalDistance = Vector3.Distance(currentPosFlat, targetPosFlat);
            
            if (horizontalDistance < 1f)
            {
                patrolPointsVisited++;
                
                if (patrolPointsVisited >= patrolPointsBeforeRest)
                {
                    currentIdleDuration = Random.Range(config.minIdleTime, config.maxIdleTime);
                    ChangeState(AIState.Idle, $"Пора отдохнуть на {currentIdleDuration:F1}s");
                    idleTimer = 0f;
                    patrolPointsVisited = 0;
                }
                else
                {
                    isWaiting = true;
                    patrolWaitTimer = 0f;
                }
            }
        }
        else
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer >= config.patrolWaitTime)
            {
                SetNewPatrolTarget();
                isWaiting = false;
            }
        }
    }
    
    private void HandleChaseState(float distanceToTarget, float horizontalDistance)
    {
        if (target != null && distanceToTarget <= config.detectionRange)
        {
            lastSeenTargetPosition = target.position;
            
        }
        
        if (distanceToTarget > config.detectionRange * config.returnToStartThreshold)
        {
            pathfindingService.StopPathfinding();
            
            if (TryReturnToStartPosition())
            {
                isReturningToStart = true;
            }
            else
            {
                startPosition = transform.position;
                ChangeState(AIState.Patrol, "Цель потеряна, стартовая позиция разрушена");
                patrolPointsVisited = 0;
                SetNewPatrolTarget();
            }
        }
        else if (distanceToTarget <= config.attackRange)
        {
            // Для атаки проверяем что цель достижима по горизонтали
            // (не имеет смысла атаковать если игрок далеко по вертикали)
            if (horizontalDistance <= config.attackRange)
            {
                // Получаем прицельную дистанцию из текущего снаряда
                float aimDistance = GetCurrentAimDistance();
                
                // Проверяем что цель в пределах прицельной дистанции
                if (horizontalDistance <= aimDistance)
                {
                    ChangeState(AIState.Attack, $"Цель в радиусе атаки (3D: {distanceToTarget:F1}m, гориз: {horizontalDistance:F1}m, прицел: {aimDistance:F1}m)");
                    pathfindingService.StopPathfinding();
                }
            }
        }
    }
    
    private void HandleAttackState(float distanceToTarget, float horizontalDistance)
    {
        // Получаем прицельную дистанцию из текущего снаряда
        float aimDistance = GetCurrentAimDistance();
        
        // Выходим из атаки если цель далеко по горизонтали
        if (horizontalDistance > aimDistance * 1.5f)
        {
            ChangeState(AIState.Chase, $"Цель вышла из радиуса атаки (гориз: {horizontalDistance:F1}m, прицел: {aimDistance:F1}m)");
        }
    }
    
    private Vector3 GetPatrolMoveDirection()
    {
        if (isWaiting)
        {
            return Vector3.zero;
        }
        
        // ВАЖНО: Проверяем расстояние только по горизонтали (X, Z), игнорируя Y
        Vector3 currentPosFlat = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetPosFlat = new Vector3(patrolTarget.x, 0, patrolTarget.z);
        float horizontalDistance = Vector3.Distance(currentPosFlat, targetPosFlat);
        
        if (horizontalDistance < 1f)
        {
            return Vector3.zero;
        }
        
        // Для проверки колебаний используем полное расстояние
        float fullDistance = Vector3.Distance(transform.position, patrolTarget);
        if (pathfindingService.CheckOscillationNearTarget(patrolTarget, horizontalDistance))
        {
            return Vector3.zero;
        }
        
        Vector3 direction = (patrolTarget - transform.position);
        direction.y = 0;
        Vector3 normalized = direction.normalized;
        
        return normalized;
    }
    
    private Vector3 GetChaseMoveDirection()
    {
        if (target == null) return Vector3.zero;
        
        return pathfindingService.GetMoveDirection(target.position, true);
    }
    
    private void SetNewPatrolTarget()
    {
        Vector3 safePoint = pathfindingService.FindSafePatrolPoint(startPosition, config.patrolRadius, config.minPatrolDistance);
        patrolTarget = safePoint;
    }
    
    private bool TryReturnToStartPosition()
    {
        if (IsPositionWalkable(positionBeforeChase))
        {
            patrolTarget = positionBeforeChase;
            return true;
        }
        
        Vector3 nearbyPosition = FindNearbyWalkablePosition(positionBeforeChase);
        if (nearbyPosition != Vector3.zero)
        {
            patrolTarget = nearbyPosition;
            return true;
        }
        
        return false;
    }
    
    private bool IsPositionWalkable(Vector3 position)
    {
        // Простая проверка - есть ли блок под ногами
        Vector3 groundCheck = new Vector3(position.x, position.y - 1f, position.z);
        return Physics.CheckBox(groundCheck, Vector3.one * 0.4f, Quaternion.identity, LayerMask.GetMask("Default"));
    }
    
    private Vector3 FindNearbyWalkablePosition(Vector3 center)
    {
        float searchRadius = 5f;
        int maxAttempts = 8;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            float angle = (360f / maxAttempts) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * searchRadius;
            Vector3 testPosition = center + offset;
            
            if (IsPositionWalkable(testPosition))
            {
                return testPosition;
            }
        }
        
        return Vector3.zero;
    }
    
    private void CheckFalling()
    {
        // Проверяем, действительно ли бот падает (не на земле и Y-координата уменьшается)
        bool isGrounded = movementService.IsGrounded;
        float currentY = transform.position.y;
        bool isFalling = !isGrounded && (currentY < lastYPosition - 0.5f); // Падает если Y уменьшилась больше чем на 0.5
        
        if (isFalling)
        {
            fallingTimer += Time.deltaTime;
            
            if (fallingTimer >= config.maxFallingTime)
            {
                Debug.Log($"[Bot] Бот '{spawnable.GetSpawnableID()}' падает слишком долго, инициируем респавн");
                AutoSpawnService.Instance?.OnEnterDeadZone(spawnable);
                fallingTimer = 0f;
            }
        }
        else
        {
            if (fallingTimer > 0f)
            {
                fallingTimer = 0f;
            }
        }
        
        lastYPosition = currentY;
    }
    
    /// <summary>
    /// Получает прицельную дистанцию из текущего снаряда
    /// </summary>
    private float GetCurrentAimDistance()
    {
        if (enemyBot == null) return config.attackRange;
        
        // Получаем EnemyWeaponController
        EnemyWeaponController weaponController = enemyBot.GetComponent<EnemyWeaponController>();
        if (weaponController == null) return config.attackRange;
        
        // Получаем текущий снаряд
        GameObject currentProjectile = weaponController.GetCurrentProjectile();
        if (currentProjectile == null) return config.attackRange;
        
        // Получаем Projectile компонент
        Projectile projectile = currentProjectile.GetComponent<Projectile>();
        if (projectile == null) return config.attackRange;
        
        // Возвращаем прицельную дистанцию из снаряда
        return projectile.aimDistance;
    }
}
