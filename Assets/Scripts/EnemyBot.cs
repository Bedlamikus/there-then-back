using UnityEngine;
using System.Collections;

[RequireComponent(typeof(HealthComponent))]
public class EnemyBot : MonoBehaviour, ISpawnable
{
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
    
    // Компоненты
    private VoxelBotController voxelController;
    private EnemyWeaponController weaponController;
    private Animator animator;
    
    // Цель и состояние
    private Transform target;
    private bool isInitialized = false;
    private bool isDead = false;
    
    // Система кулинга
    private float nextCullingCheckTime = 0f;
    private PlayerController playerController;
    
    // AI состояние
    private float lastDetectionTime = 0f;
    private bool hasDetectedPlayer = false;
    private float lastAttackTime = 0f;
    private float attackCooldown = 1f;
    
    void Start()
    {
        // Инициализируем только если еще не инициализирован
        if (!isInitialized)
        {
            InitializeBot();
        }
    }
    
    void Update()
    {
        if (!isInitialized || isDead) return;
        
        UpdateCulling();
        UpdateAI();
        UpdateWeapon();
    }
    
    /// <summary>
    /// Инициализирует бота
    /// </summary>
    void InitializeBot()
    {
        // Генерируем ID если не задан
        if (string.IsNullOrEmpty(botID))
        {
            botID = $"EnemyBot_{GetInstanceID()}";
        }
        
        // Получаем компоненты
        healthComponent = GetComponent<HealthComponent>();
        animator = GetComponent<Animator>();
        weaponController = GetComponent<EnemyWeaponController>();
        
        // Создаем VoxelBotController
        voxelController = gameObject.AddComponent<VoxelBotController>();
        voxelController.botId = botID;
        
        // Проверяем что конфиг назначен
        if (voxelController.config == null)
        {
            Debug.LogError($"[EnemyBot] VoxelBotConfig not assigned to VoxelBotController for {botID}!");
        }
        
        // Инициализируем здоровье
        InitializeHealth();
        
        // Получаем PlayerController для кулинга
        playerController = FindObjectOfType<PlayerController>();
        
        // Регистрируем в системе автоспавна
        RegisterWithAutoSpawn();
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Инициализирует бота с целью (вызывается из EnemySpawner)
    /// </summary>
    public void Init(Transform targetPlayer)
    {
        target = targetPlayer;
        
        // Если VoxelBotController еще не создан, создаем его
        if (voxelController == null)
        {
            InitializeBot();
        }
        
        // Устанавливаем цель для VoxelBotController
        if (voxelController != null)
        {
            voxelController.SetTarget(target);
        }
        
    }
    
    /// <summary>
    /// Устанавливает цель для бота
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        
        // Устанавливаем цель для VoxelBotController
        if (voxelController != null)
        {
            voxelController.SetTarget(target);
        }
        
    }
    
    /// <summary>
    /// Инициализирует систему здоровья
    /// </summary>
    void InitializeHealth()
    {
        if (healthComponent == null)
        {
            Debug.LogError($"[EnemyBot] HealthComponent not found on {gameObject.name}");
            return;
        }
        
        // Подписываемся на события здоровья
        healthComponent.OnDeath += OnEnemyDeath;
        healthComponent.OnDamageTaken += OnEnemyDamageTaken;
        
    }
    
    /// <summary>
    /// Обновляет систему кулинга
    /// </summary>
    void UpdateCulling()
    {
        if (playerController == null) return;
        
        // Проверка кулинга раз в ~10 секунд
        if (Time.time >= nextCullingCheckTime)
        {
            nextCullingCheckTime = Time.time + 10f;
            
            float distanceToPlayer = Vector3.Distance(transform.position, playerController.transform.position);
            float despawnDistance = playerController.viewDistance + 50f; // Дополнительный буфер
            
            if (distanceToPlayer > despawnDistance)
            {
                Destroy(gameObject);
            }
        }
    }
    
    /// <summary>
    /// Обновляет ИИ бота
    /// </summary>
    void UpdateAI()
    {
        if (target == null)
        {
            // Ищем игрока
            FindPlayer();
            return;
        }
        
        // Проверяем расстояние до игрока
        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
        
        // Проверяем обнаружение игрока
        if (distanceToPlayer <= voxelController.config.detectionRadius)
        {
            if (!hasDetectedPlayer)
            {
                hasDetectedPlayer = true;
                lastDetectionTime = Time.time;
            }
            
            // Устанавливаем цель для движения
            voxelController.SetTarget(target);
            
            // Проверяем возможность атаки
            if (distanceToPlayer <= voxelController.config.attackRange)
            {
                TryAttack();
            }
        }
        else
        {
            // Игрок слишком далеко
            if (hasDetectedPlayer)
            {
                hasDetectedPlayer = false;
                voxelController.SetTarget(null);
            }
        }
        
        // Обновляем анимацию (теперь это делает VoxelBotController)
        // UpdateAnimation();
        
    }
    
    /// <summary>
    /// Обновляет анимацию бота
    /// </summary>
    void UpdateAnimation()
    {
        if (animator == null) 
        {
            Debug.LogWarning($"[EnemyBot] Animator is null for bot {botID}!");
            return;
        }
        
        // Вычисляем скорость движения
        Vector3 velocity = Vector3.zero;
        bool isJumping = false;
        if (voxelController != null)
        {
            VoxelBotData botData = voxelController.GetBotData();
            if (botData != null)
            {
                velocity = botData.velocity;
                isJumping = botData.isJumping;
            }
        }
        
        // Устанавливаем параметры анимации (как у игрока)
        float speed = velocity.magnitude;
        bool isGrounded = IsGrounded();
        
        // Нормализуем скорость как у игрока (0 = стоит, 1 = максимальная скорость)
        float normalizedSpeed = Mathf.Clamp01(speed / voxelController.config.moveSpeed);
        
        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsGrounded", isGrounded);
        // animator.SetBool("IsJumping", isJumping); // УБРАНО - как у игрока
        
        // Отладочная информация для анимации
        if (Time.frameCount % 60 == 0) // Каждую секунду
        {
            Debug.Log($"[EnemyBot] Animation params: Speed={normalizedSpeed:F2}, IsGrounded={isGrounded}");
        }
        
        // Поворачиваем бота в направлении движения
        if (speed > 0.1f)
        {
            Vector3 moveDirection = velocity.normalized;
            moveDirection.y = 0; // Игнорируем вертикальное движение
            
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }
    
    
    /// <summary>
    /// Ищет игрока
    /// </summary>
    void FindPlayer()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning($"[EnemyBot] PlayerController not found!");
                return;
            }
        }
        
        target = playerController.transform;
    }
    
    /// <summary>
    /// Пытается атаковать
    /// </summary>
    void TryAttack()
    {
        // Блокируем атаку если бот мертв
        if (isDead) return;
        
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        if (weaponController != null)
        {
            // Проверяем что оружие направлено на цель
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
            
            if (angleToTarget < 30f) // Допустимый угол для атаки
            {
                weaponController.TryShoot();
                lastAttackTime = Time.time;
            }
        }
    }
    
    /// <summary>
    /// Обновляет оружие
    /// </summary>
    void UpdateWeapon()
    {
        // Блокируем обновление оружия если бот мертв
        if (isDead) return;
        
        if (weaponController != null && target != null)
        {
            weaponController.Update();
        }
    }
    
    /// <summary>
    /// Обработчик смерти бота
    /// </summary>
    void OnEnemyDeath()
    {
        if (isDead) return;
        
        isDead = true;
        
        // Устанавливаем флаг смерти в данных бота
        if (voxelController != null)
        {
            VoxelBotData botData = voxelController.GetBotData();
            if (botData != null)
            {
                botData.isDead = true;
            }
        }
        
        
        // Останавливаем движение
        if (voxelController != null)
        {
            voxelController.SetTarget(null);
        }
        
        // Взрываем части бота (ОТКЛЮЧЕНО - части провалятся без коллайдеров)
        // ExplodeEnemyParts();
        
        // Удаляем основной объект врага через некоторое время
        StartCoroutine(DestroyCorpseAfterDelay());
        
        // Останавливаем AI (отключаем Update)
        enabled = false;
    }
    
    /// <summary>
    /// Обработчик получения урона
    /// </summary>
    void OnEnemyDamageTaken(float damage)
    {
        
        // Можно добавить эффекты урона:
        // - Звук удара
        // - Эффект крови/искр
        // - Красная вспышка на модели
    }
    
    /// <summary>
    /// Взрывает части врага при смерти
    /// </summary>
    void ExplodeEnemyParts()
    {
        // Находим все меш рендереры в детях
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        // Обрабатываем обычные меш рендереры
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            if (meshRenderer.transform == transform) continue; // Пропускаем основной объект
            
            ExplodePart(meshRenderer.gameObject);
        }
        
        // Обрабатываем скин меш рендереры
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            if (skinnedMeshRenderer.transform == transform) continue; // Пропускаем основной объект
            
            ExplodePart(skinnedMeshRenderer.gameObject);
        }
        
        // Отключаем аниматор
        if (animator != null)
        {
            animator.enabled = false;
        }
    }
    
    /// <summary>
    /// Взрывает отдельную часть
    /// </summary>
    void ExplodePart(GameObject part)
    {
        // Отсоединяем от родителя
        part.transform.SetParent(null);
        
        // Добавляем Rigidbody
        Rigidbody rb = part.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.5f;
        
        // Добавляем MeshCollider
        MeshCollider meshCollider = part.AddComponent<MeshCollider>();
        meshCollider.convex = true;
        
        // Применяем взрывную силу
        Vector3 explosionDirection = (part.transform.position - transform.position).normalized;
        rb.AddForce(explosionDirection * deathExplosionForce, ForceMode.Impulse);
        
        // Добавляем случайное вращение
        rb.AddTorque(Random.insideUnitSphere * deathExplosionForce, ForceMode.Impulse);
        
        // Добавляем компонент для автоматического удаления
        DestroyAfterTime destroyAfterTime = part.AddComponent<DestroyAfterTime>();
        destroyAfterTime.lifetime = partsLifetime;
    }
    
    /// <summary>
    /// Удаляет основной объект врага через заданное время
    /// </summary>
    IEnumerator DestroyCorpseAfterDelay()
    {
        yield return new WaitForSeconds(corpseLifetime);
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Регистрирует бота в системе автоспавна
    /// </summary>
    void RegisterWithAutoSpawn()
    {
        AutoSpawnService.Instance?.RegisterSpawnable(this);
    }
    
    // Реализация ISpawnable
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
        return voxelController?.GetBotData()?.isGrounded ?? false;
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
    
    /// <summary>
    /// Получает текущее состояние ИИ бота
    /// </summary>
    public AIState GetCurrentState()
    {
        if (hasDetectedPlayer)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, target.position);
            if (distanceToPlayer <= voxelController.config.attackRange)
            {
                return AIState.Attack;
            }
            else
            {
                return AIState.Chase;
            }
        }
        else
        {
            return AIState.Patrol;
        }
    }
    
    /// <summary>
    /// Получает данные бота для проверки попаданий
    /// </summary>
    public VoxelBotData GetBotData()
    {
        return voxelController?.GetBotData();
    }
    
    /// <summary>
    /// Проверяет попадание в бота
    /// </summary>
    public bool CheckHit(Vector3 hitPoint, float radius)
    {
        return voxelController?.CheckHit(hitPoint, radius) ?? false;
    }
}