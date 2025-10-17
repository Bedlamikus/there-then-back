using UnityEngine;

/// <summary>
/// Главный мозг бота для воксельного мира
/// Объединяет состояние ИИ, поиск пути и движение
/// </summary>
public class VoxelBotAI : MonoBehaviour
{
    [Header("AI Configuration")]
    public EnemyAIConfig aiConfig;
    public EnemyPathfindingConfig pathfindingConfig;
    public EnemyMovementConfig movementConfig;
    
    [Header("Components")]
    public VoxelBotController voxelController;
    public Animator animator;
    
    [Header("Target")]
    public Transform target;
    
    // Сервисы
    private EnemyAIStateMachine stateMachine;
    private EnemyPathfindingService pathfindingService;
    private VoxelMovementServiceAdapter movementService;
    
    // Состояние
    private bool isInitialized = false;
    private Vector3 lastMoveDirection = Vector3.zero;
    
    // Ссылка на EnemyBot для получения информации о снарядах
    private EnemyBot enemyBot;
    
    public AIState CurrentState => stateMachine?.CurrentState ?? AIState.Patrol;
    public Transform Target => target;
    public Vector3 PatrolTarget => stateMachine?.PatrolTarget ?? Vector3.zero;
    public bool IsWaiting => stateMachine?.IsWaiting ?? false;
    public bool IsReturningToStart => stateMachine?.IsReturningToStart ?? false;
    
    void Start()
    {
        InitializeAI();
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        UpdateAI();
    }
    
    /// <summary>
    /// Инициализирует ИИ бота
    /// </summary>
    public void InitializeAI()
    {
        if (isInitialized) return;
        
        // Получаем ссылку на EnemyBot
        enemyBot = GetComponent<EnemyBot>();
        
        // Получаем компоненты если не назначены
        if (voxelController == null)
            voxelController = GetComponent<VoxelBotController>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        // Проверяем конфигурации
        if (aiConfig == null)
        {
            Debug.LogError($"[VoxelBotAI] EnemyAIConfig not assigned for {gameObject.name}!");
            return;
        }
        
        if (pathfindingConfig == null)
        {
            Debug.LogError($"[VoxelBotAI] EnemyPathfindingConfig not assigned for {gameObject.name}!");
            return;
        }
        
        if (movementConfig == null)
        {
            Debug.LogError($"[VoxelBotAI] EnemyMovementConfig not assigned for {gameObject.name}!");
            return;
        }
        
        // Создаем сервисы
        pathfindingService = new EnemyPathfindingService(transform, pathfindingConfig);
        movementService = new VoxelMovementServiceAdapter(null, transform, movementConfig, animator);
        
        // Создаем машину состояний
        stateMachine = new EnemyAIStateMachine(
            transform, 
            aiConfig, 
            pathfindingService, 
            movementService, 
            enemyBot
        );
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Инициализирует ИИ с целью (вызывается из EnemyBot)
    /// </summary>
    public void InitializeWithTarget(Transform targetPlayer)
    {
        InitializeAI();
        
        if (targetPlayer != null)
        {
            target = targetPlayer;
            stateMachine?.SetTarget(target);
        }
    }
    
    /// <summary>
    /// Устанавливает цель для бота
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        stateMachine?.SetTarget(target);
    }
    
    /// <summary>
    /// Обновляет ИИ
    /// </summary>
    private void UpdateAI()
    {
        if (!isInitialized) return;
        
        // Обновляем машину состояний
        stateMachine.Update();
        
        // Получаем направление движения от машины состояний
        Vector3 moveDirection = stateMachine.GetMoveDirection();
        
        // Проверяем нужен ли прыжок
        bool shouldJump = stateMachine.ShouldJump();
        
        // Обновляем сервисы
        pathfindingService.Update(moveDirection);
        movementService.Update();
        
        // Передаем команды контроллеру движения
        if (voxelController != null)
        {
            voxelController.MoveToTarget(moveDirection, shouldJump);
        }
        
        // Сохраняем направление для отладки
        lastMoveDirection = moveDirection;
    }
    
    /// <summary>
    /// Получает прицельную дистанцию из текущего снаряда
    /// </summary>
    public float GetCurrentAimDistance()
    {
        if (enemyBot == null) return aiConfig?.attackRange ?? 10f;
        
        // Получаем EnemyWeaponController
        EnemyWeaponController weaponController = enemyBot.GetComponent<EnemyWeaponController>();
        if (weaponController == null) return aiConfig?.attackRange ?? 10f;
        
        // Получаем текущий снаряд
        GameObject currentProjectile = weaponController.GetCurrentProjectile();
        if (currentProjectile == null) return aiConfig?.attackRange ?? 10f;
        
        // Получаем Projectile компонент
        Projectile projectile = currentProjectile.GetComponent<Projectile>();
        if (projectile == null) return aiConfig?.attackRange ?? 10f;
        
        // Возвращаем прицельную дистанцию из снаряда
        return projectile.aimDistance;
    }
    
    /// <summary>
    /// Проверяет может ли бот атаковать цель
    /// </summary>
    public bool CanAttackTarget()
    {
        if (target == null) return false;
        
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        float attackRange = aiConfig?.attackRange ?? 10f;
        
        return distanceToTarget <= attackRange;
    }
    
    /// <summary>
    /// Проверяет обнаружена ли цель
    /// </summary>
    public bool IsTargetDetected()
    {
        if (target == null) return false;
        
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        float detectionRange = aiConfig?.detectionRange ?? 15f;
        
        return distanceToTarget <= detectionRange;
    }
    
    /// <summary>
    /// Получает расстояние до цели
    /// </summary>
    public float GetDistanceToTarget()
    {
        if (target == null) return float.MaxValue;
        
        return Vector3.Distance(transform.position, target.position);
    }
    
    /// <summary>
    /// Получает горизонтальное расстояние до цели
    /// </summary>
    public float GetHorizontalDistanceToTarget()
    {
        if (target == null) return float.MaxValue;
        
        Vector3 botPosFlat = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetPosFlat = new Vector3(target.position.x, 0, target.position.z);
        
        return Vector3.Distance(botPosFlat, targetPosFlat);
    }
    
    /// <summary>
    /// Принудительно переводит бота в состояние патруля
    /// </summary>
    public void ForcePatrolState()
    {
        if (stateMachine != null)
        {
            stateMachine.SetTarget(null);
            target = null;
        }
    }
    
    /// <summary>
    /// Сбрасывает ИИ к начальному состоянию
    /// </summary>
    public void ResetAI()
    {
        if (stateMachine != null)
        {
            stateMachine.SetTarget(null);
            target = null;
        }
        
        pathfindingService?.ResetStuckDetection();
    }
    
    void OnDrawGizmosSelected()
    {
        if (!isInitialized) return;
        
        // Рисуем направление движения
        if (lastMoveDirection.sqrMagnitude > 0.1f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, lastMoveDirection * 2f);
        }
        
        // Рисуем цель
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.position);
        }
        
        // Рисуем точку патруля
        Vector3 patrolTarget = PatrolTarget;
        if (patrolTarget != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(patrolTarget, 0.5f);
            Gizmos.DrawLine(transform.position, patrolTarget);
        }
        
        // Рисуем радиус обнаружения
        if (aiConfig != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aiConfig.detectionRange);
        }
        
        // Рисуем радиус атаки
        if (aiConfig != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aiConfig.attackRange);
        }
    }
}
