using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(HealthComponent))]
public class EnemyBot : MonoBehaviour, ISpawnable
{
    [Header("Configurations")]
    public EnemyMovementConfig movementConfig;
    public EnemyPathfindingConfig pathfindingConfig;
    public EnemyAIConfig aiConfig;
    
    [Header("Components")]
    public CharacterController controller;
    public Animator animator;
    
    [Header("Weapon")]
    public Transform turret;           // Турель (вращается по Z горизонтально)
    public Transform weaponBarrel;     // Ствол оружия (вращается вверх/вниз)
    public Transform shootPoint;       // Точка выстрела (на конце ствола)
    
    [Header("Bot Identity")]
    public string botID;
    
    [Header("Health")]
    [Tooltip("Компонент здоровья бота")]
    public HealthComponent healthComponent;
    
    [Header("Death Effect")]
    [Tooltip("Сила разлета частей при смерти")]
    public float deathExplosionForce = 5f;
    [Tooltip("Радиус разлета частей")]
    public float deathExplosionRadius = 2f;
    [Tooltip("Время до удаления основного объекта врага после смерти")]
    public float corpseLifetime = 10f;
    [Tooltip("Время жизни частей после смерти (секунды)")]
    public float partsLifetime = 15f;
    
    // Сервисы
    private EnemyMovementService movementService;
    private EnemyPathfindingService pathfindingService;
    private EnemyAIStateMachine aiStateMachine;
    
    // Цель
    private Transform target;
    private bool isInitialized = false;
    
    // Система кулинга
    private float nextCullingCheckTime = 0f;
    private PlayerController playerController; // Для получения viewDistance

    public void Init(Transform playerTarget)
    {
        if (isInitialized)
        {
            return;
        }
        
        InitializeBot();
        InitializeServices();
        InitializeHealth();
        
        // Устанавливаем цель
        target = playerTarget;
        if (aiStateMachine != null && target != null)
        {
            aiStateMachine.SetTarget(target);
        }
        
        // Получаем PlayerController для кулинга
        if (playerTarget != null)
        {
            playerController = playerTarget.GetComponent<PlayerController>();
        }
        
        // Инициализируем кулинг со случайной задержкой
        nextCullingCheckTime = Time.time + Random.Range(5f, 10f);
        
        Debug.Log($"[Bot] Бот {botID} создан в позиции {transform.position}");
        
        RegisterWithAutoSpawn();
        isInitialized = true;
    }

    void Update()
    {
        UpdateServices();
        HandleMovement();
        
        // Проверка кулинга раз в ~10 секунд
        if (Time.time >= nextCullingCheckTime)
        {
            CheckCulling();
            nextCullingCheckTime = Time.time + Random.Range(8f, 12f);
        }
    }
    
    void OnDestroy()
    {
        Debug.Log($"[Bot] Бот {botID} уничтожен");
        AutoSpawnService.Instance?.UnregisterSpawnable(this);
        
        // Отписываемся от событий здоровья
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= OnEnemyDeath;
            healthComponent.OnDamageTaken -= OnEnemyDamageTaken;
        }
    }
    
    /// <summary>
    /// Проверка дистанции до игрока и деспавн если слишком далеко
    /// Использует КВАДРАТ расстояния для оптимизации (без Mathf.Sqrt)
    /// </summary>
    void CheckCulling()
    {
        if (target == null || playerController == null)
            return;
        
        // Вычисляем КВАДРАТ расстояния БЕЗ Mathf.Sqrt (быстрее!)
        Vector3 pos = transform.position;
        Vector3 targetPos = target.position;
        
        float dx = pos.x - targetPos.x;
        float dz = pos.z - targetPos.z;
        float distanceSqr = dx * dx + dz * dz;
        
        // Враги деспавнятся на расстоянии = viewDistance
        // Чанки исчезают на viewDistance * 1.5
        // Сравниваем квадраты расстояний
        float despawnDistanceSqr = playerController.viewDistance * playerController.viewDistance;
        
        if (distanceSqr > despawnDistanceSqr)
        {
            // Убрали Debug.Log чтобы не создавать строки (GC)
            Destroy(gameObject);
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
    
    private void InitializeHealth()
    {
        // Получаем компонент здоровья
        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();
        
        // Подписываемся на события здоровья
        if (healthComponent != null)
        {
            healthComponent.OnDeath += OnEnemyDeath;
            healthComponent.OnDamageTaken += OnEnemyDamageTaken;
            Debug.Log($"[Bot] {botID} инициализировано здоровье: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
        }
        else
        {
            Debug.LogError($"[Bot] {botID} HealthComponent не найден!");
        }
    }
    
    private void OnEnemyDeath()
    {
        Debug.Log($"[Bot] {botID} погиб!");
        
        // Останавливаем движение
        if (controller != null)
            controller.enabled = false;
        
        // Разбрасываем части
        ExplodeEnemyParts();
        
        // Удаляем основной объект врага через некоторое время
        StartCoroutine(DestroyCorpseAfterDelay());
        
        // Останавливаем AI (отключаем Update)
        enabled = false;
    }
    
    /// <summary>
    /// Удаляет основной объект врага через заданное время
    /// </summary>
    private System.Collections.IEnumerator DestroyCorpseAfterDelay()
    {
        yield return new UnityEngine.WaitForSeconds(corpseLifetime);
        
        Debug.Log($"[Bot] {botID} удаление трупа");
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Разбрасывает части врага при смерти
    /// </summary>
    private void ExplodeEnemyParts()
    {
        // Находим все дочерние объекты с MeshRenderer/SkinnedMeshRenderer
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        Vector3 explosionCenter = transform.position;
        
        // Обрабатываем обычные меши
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            GameObject part = meshRenderer.gameObject;
            
            // Пропускаем сам объект врага
            if (part == gameObject)
                continue;
            
            // Получаем MeshFilter для создания MeshCollider
            MeshFilter meshFilter = part.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;
            
            // Отсоединяем от врага
            part.transform.SetParent(null);
            
            // Добавляем Rigidbody
            Rigidbody rb = part.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = part.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.drag = 0.5f;
                rb.angularDrag = 0.5f;
            }
            
            // Добавляем MeshCollider
            MeshCollider meshCollider = part.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = part.AddComponent<MeshCollider>();
                meshCollider.convex = true; // Обязательно для Rigidbody
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
            
            // Применяем силу взрыва
            Vector3 direction = (part.transform.position - explosionCenter).normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = Random.insideUnitSphere;
            
            rb.AddForce(direction * deathExplosionForce + Vector3.up * (deathExplosionForce * 0.5f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * deathExplosionForce, ForceMode.VelocityChange);
            
            // Добавляем компонент автоудаления
            DestroyAfterTime destroyComponent = part.AddComponent<DestroyAfterTime>();
            destroyComponent.lifetime = partsLifetime;
            
            Debug.Log($"[Bot] Часть {part.name} отсоединена и получила физику");
        }
        
        // Обрабатываем skinned меши (для персонажей с анимацией)
        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedMeshRenderers)
        {
            GameObject part = skinnedRenderer.gameObject;
            
            // Пропускаем сам объект врага
            if (part == gameObject)
                continue;
            
            // Создаем статичный меш из skinned mesh
            Mesh bakedMesh = new Mesh();
            skinnedRenderer.BakeMesh(bakedMesh);
            
            // Отключаем SkinnedMeshRenderer
            skinnedRenderer.enabled = false;
            
            // Добавляем обычный MeshRenderer и MeshFilter
            MeshFilter meshFilter = part.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = part.AddComponent<MeshFilter>();
            meshFilter.mesh = bakedMesh;
            
            MeshRenderer meshRenderer = part.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.materials = skinnedRenderer.materials;
            
            // Отсоединяем от врага
            part.transform.SetParent(null);
            
            // Добавляем Rigidbody
            Rigidbody rb = part.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = part.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.drag = 0.5f;
                rb.angularDrag = 0.5f;
            }
            
            // Добавляем MeshCollider
            MeshCollider meshCollider = part.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = part.AddComponent<MeshCollider>();
                meshCollider.convex = true;
                meshCollider.sharedMesh = bakedMesh;
            }
            
            // Применяем силу взрыва
            Vector3 direction = (part.transform.position - explosionCenter).normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = Random.insideUnitSphere;
            
            rb.AddForce(direction * deathExplosionForce + Vector3.up * (deathExplosionForce * 0.5f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * deathExplosionForce, ForceMode.VelocityChange);
            
            // Добавляем компонент автоудаления
            DestroyAfterTime destroyComponent = part.AddComponent<DestroyAfterTime>();
            destroyComponent.lifetime = partsLifetime;
            
            Debug.Log($"[Bot] Skinned часть {part.name} отсоединена и получила физику");
        }
        
        // Отключаем animator чтобы не мешал
        if (animator != null)
            animator.enabled = false;
    }
    
    private void OnEnemyDamageTaken(float damage)
    {
        Debug.Log($"[Bot] {botID} получил {damage} урона. Осталось здоровья: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
        
        // Здесь можно добавить визуальные/звуковые эффекты получения урона:
        // - Воспроизвести анимацию получения урона
        // - Звук удара
        // - Эффект крови/искр
        // - Красная вспышка на модели
    }
    
    private void UpdateServices()
    {
        if (!isInitialized)
            return;
            
        movementService.Update();
        aiStateMachine.Update();
        
        // Обновляем сервис автоспавна
        AutoSpawnService.Instance?.TickSpawnable(this, Time.deltaTime);
    }
    
    private void HandleMovement()
    {
        if (!isInitialized)
            return;
            
        Vector3 moveDirection = aiStateMachine.GetMoveDirection();
        
        // Обновляем pathfinding service с текущим направлением движения
        pathfindingService.Update(moveDirection);
        
        // Проверяем нужно ли прыгать
        if (aiStateMachine.ShouldJump())
        {
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
    
    // Методы для работы с оружием
    public Transform GetTurret()
    {
        return turret;
    }
    
    public Transform GetWeaponBarrel()
    {
        return weaponBarrel;
    }
    
    public Transform GetShootPoint()
    {
        return shootPoint;
    }
    
    public Transform GetTarget()
    {
        return target;
    }
    
    public AIState GetCurrentState()
    {
        return aiStateMachine != null ? aiStateMachine.CurrentState : AIState.Idle;
    }
    
    public EnemyPathfindingService GetPathfindingService()
    {
        return pathfindingService;
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
