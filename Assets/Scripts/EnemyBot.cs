using UnityEngine;
using System.Collections.Generic;

public class EnemyBot : MonoBehaviour, ISpawnable
{
    [Header("Configurations")]
    public EnemyMovementConfig movementConfig;
    public EnemyPathfindingConfig pathfindingConfig;
    public EnemyAIConfig aiConfig;
    
    [Header("Components")]
    public CharacterController controller;
    public Animator animator;
    
    [Header("Bot Identity")]
    public string botID;
    
    // Сервисы
    private EnemyMovementService movementService;
    private EnemyPathfindingService pathfindingService;
    private EnemyAIStateMachine aiStateMachine;
    
    // Цель
    private Transform target;

    void Start()
    {
        InitializeBot();
        InitializeServices();
        RegisterWithAutoSpawn();
    }

    void Update()
    {
        UpdateServices();
        HandleMovement();
    }
    
    void OnDestroy()
    {
        AutoSpawnService.Instance?.UnregisterSpawnable(this);
    }
    
    void OnDrawGizmos()
    {
        // Рисуем путь если он есть
        if (pathfindingService != null && pathfindingService.CurrentPath != null && pathfindingService.CurrentPath.Count > 0)
        {
            List<Vector3> path = pathfindingService.CurrentPath;
            
            // Рисуем кубики на каждой точке пути
            for (int i = 0; i < path.Count; i++)
            {
                // Цвет зависит от того, пройдена ли точка
                if (i < pathfindingService.CurrentWaypointIndex)
                {
                    Gizmos.color = Color.gray; // Пройденные точки
                }
                else if (i == pathfindingService.CurrentWaypointIndex)
                {
                    Gizmos.color = Color.yellow; // Текущая цель
                }
                else
                {
                    Gizmos.color = Color.green; // Будущие точки
                }
                
                Gizmos.DrawWireCube(path[i], Vector3.one * 0.5f);
                
                // Рисуем линии между точками
                if (i > 0)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(path[i - 1], path[i]);
                }
            }
            
            // Рисуем линию от бота до первой точки пути
            if (path.Count > 0)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, path[0]);
            }
        }
        
        // Рисуем текущую точку патруля (если в режиме патруля)
        if (aiStateMachine != null && aiStateMachine.CurrentState == AIState.Patrol)
        {
            Vector3 patrolTarget = aiStateMachine.PatrolTarget;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(patrolTarget, Vector3.one * 0.8f);
            Gizmos.DrawLine(transform.position, patrolTarget);
        }
    }
    
    private void InitializeBot()
    {
        // Генерируем уникальный ID если не задан
        if (string.IsNullOrEmpty(botID))
        {
            botID = $"EnemyBot_{GetInstanceID()}";
        }
        
        // Получаем компоненты если не назначены
        if (controller == null)
            controller = GetComponent<CharacterController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        // Создаем конфигурации по умолчанию если не назначены
        if (movementConfig == null)
            movementConfig = CreateDefaultMovementConfig();
        
        if (pathfindingConfig == null)
            pathfindingConfig = CreateDefaultPathfindingConfig();
        
        if (aiConfig == null)
            aiConfig = CreateDefaultAIConfig();
    }
    
    private void InitializeServices()
    {
        movementService = new EnemyMovementService(controller, transform, movementConfig, animator);
        pathfindingService = new EnemyPathfindingService(transform, pathfindingConfig);
        aiStateMachine = new EnemyAIStateMachine(transform, aiConfig, pathfindingService, movementService, this);
    }
    
    private void UpdateServices()
    {
        movementService.Update();
        aiStateMachine.Update();
        
        // Обновляем сервис автоспавна
        AutoSpawnService.Instance?.TickSpawnable(this, Time.deltaTime);
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection = aiStateMachine.GetMoveDirection();
        
        // Обновляем pathfinding service с текущим направлением движения
        pathfindingService.Update(moveDirection);
        
        // Проверяем нужно ли прыгать
        if (aiStateMachine.ShouldJump())
        {
            AIState currentState = aiStateMachine.CurrentState;
            Debug.Log($"[Bot Jump] Инициирован прыжок! Состояние: {currentState}, Позиция: {transform.position}");
            movementService.InitiateJump();
        }
        
        // Блокируем движение ТОЛЬКО во время подготовки к прыжку (0.1s)
        // Во время полета и кулдауна после приземления - бот ДВИГАЕТСЯ!
        if (movementService.IsPreparingJump)
        {
            moveDirection = Vector3.zero;
        }
        
        movementService.HandleMovement(moveDirection);
    }

    // Публичные методы для внешнего управления
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (aiStateMachine != null)
        {
            aiStateMachine.SetTarget(newTarget);
        }
    }
    
    // ISpawnable интерфейс
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
        return movementService.IsGrounded;
    }
    
    private void RegisterWithAutoSpawn()
    {
        AutoSpawnService.Instance?.RegisterSpawnable(this);
    }
    
    // Создание конфигураций по умолчанию
    private EnemyMovementConfig CreateDefaultMovementConfig()
    {
        EnemyMovementConfig config = ScriptableObject.CreateInstance<EnemyMovementConfig>();
        config.moveSpeed = 5f;
        config.turnSpeed = 10f;
        config.turnThreshold = 0.1f;
        config.jumpHeight = 3f;
        config.jumpPrepareTime = 0.1f;
        config.jumpCooldownTime = 0.5f;
        config.gravity = -9.81f;
        config.groundCheckDistance = 0.2f;
        config.coyoteTime = 0.1f;
        config.speedParameter = "Speed";
        config.isGroundedParameter = "IsGrounded";
        return config;
    }
    
    private EnemyPathfindingConfig CreateDefaultPathfindingConfig()
    {
        EnemyPathfindingConfig config = ScriptableObject.CreateInstance<EnemyPathfindingConfig>();
        config.usePathfinding = true;
        config.pathUpdateInterval = 1f;
        config.maxPathLength = 50;
        config.waypointReachDistance = 1.5f;
        config.stuckCheckTime = 2f;
        config.stuckDistanceThreshold = 1f;
        config.stuckAreaSize = 4f;
        config.maxStuckAttempts = 3;
        config.unstuckPatrolTime = 5f;
        config.oscillationThreshold = 1.5f;
        return config;
    }
    
    private EnemyAIConfig CreateDefaultAIConfig()
    {
        EnemyAIConfig config = ScriptableObject.CreateInstance<EnemyAIConfig>();
        config.detectionRange = 15f;
        config.attackRange = 3f;
        config.patrolRadius = 10f;
        config.minPatrolDistance = 3f;
        config.patrolWaitTime = 2f;
        config.minPatrolPointsBeforeRest = 3;
        config.maxPatrolPointsBeforeRest = 6;
        config.minIdleTime = 30f;
        config.maxIdleTime = 120f;
        config.returnToStartThreshold = 1.5f;
        config.maxFallingTime = 10f;
        return config;
    }
}
