using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(HealthComponent))]
public class PlayerController : MonoBehaviour, ISpawnable
{
    public Transform cameraPivot;                 // Направление WASD движений по yaw камеры

    [Header("Move")]
    public float moveSpeed = 6f;                 // Скорость горизонтального движения
    public float turnSpeed = 5f;                 // Скорость поворота (1-10, чем больше - тем быстрее)
    public float turnThreshold = 0.1f;           // Минимальная скорость для поворота

    [Header("Jump & Gravity")]
    public float jumpHeight = 3.5f;              // Высота прыжка
    public float gravity = -9.81f;               // Сила гравитации
    public float coyoteTime = 0.1f;              // Время-призрак для прыжка после отрыва от земли
    public float groundCheckDistance = 0.2f;     // Расстояние проверки земли

    [Header("View Distance")]
    [Tooltip("Дистанция видимости для кулинга чанков и врагов")]
    public float viewDistance = 64f;             // Дистанция видимости (в блоках) - оптимизировано для GPU
    
    [Header("Animation")]
    public Animator animator;                    // Аниматор персонажа
    public string speedParameter = "Speed";      // Параметр скорости в аниматоре
    public string isGroundedParameter = "IsGrounded"; // Параметр нахождения на земле
    public float animationSpeedMultiplier = 1f;  // Множитель скорости анимации

    [Header("Health")]
    [Tooltip("Компонент здоровья игрока")]
    public HealthComponent healthComponent;      // Компонент здоровья
    
    [Header("Death Effect")]
    [Tooltip("Сила разлета частей при смерти")]
    public float deathExplosionForce = 5f;
    [Tooltip("Радиус разлета частей")]
    public float deathExplosionRadius = 2f;
    [Tooltip("Время жизни частей после смерти (секунды)")]
    public float partsLifetime = 15f;

    CharacterController controller;
    Vector2 _input;                              // Ввод WASD (-1..1)
    bool _jumpPressed;
    bool _grounded;
    float _lastGroundTime;
    Vector3 _velocity;                           // Вертикальная скорость для прыжка и гравитации

    // Анимация
    float _currentSpeed;                         // Текущая скорость движения
    Vector3 _lastPosition;                       // Позиция в предыдущем кадре для расчета скорости
    
    // Состояние смерти
    bool _isDead = false;
    
    // Публичное свойство для проверки смерти
    public bool IsDead => _isDead;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        GlobalEvents.PlayerMove.AddListener(SetInput);
        GlobalEvents.PlayerJump.AddListener(Jump);

        // Инициализация анимации
        if (animator == null)
            animator = GetComponent<Animator>();
        
        _lastPosition = transform.position;
        
        // Инициализация компонента здоровья
        InitializeHealth();
        
        // Инициализация сервиса автоспавна
        InitializeAutoSpawnService();
    }
    
    private void InitializeHealth()
    {
        // Получаем компонент здоровья
        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();
        
        // Подписываемся на события здоровья
        if (healthComponent != null)
        {
            healthComponent.OnDeath += OnPlayerDeath;
            healthComponent.OnDamageTaken += OnPlayerDamageTaken;
            Debug.Log($"[Player] Инициализировано здоровье: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
        }
        else
        {
            Debug.LogError("[Player] HealthComponent не найден!");
        }
    }
    
    private void OnPlayerDeath()
    {
        Debug.Log("[Player] Игрок погиб!");
        
        _isDead = true;
        
        // Останавливаем движение
        _input = Vector2.zero;
        _velocity = Vector3.zero;
        
        // Отключаем CharacterController чтобы не мешал физике частей
        if (controller != null)
            controller.enabled = false;
        
        // Разбрасываем части
        ExplodePlayerParts();
        
        // Отключаем Update но оставляем скрипт активным (для камеры)
        // enabled = false; - НЕ отключаем, просто блокируем управление через _isDead
    }
    
    /// <summary>
    /// Разбрасывает части игрока при смерти
    /// </summary>
    private void ExplodePlayerParts()
    {
        // Находим все дочерние объекты с MeshRenderer/SkinnedMeshRenderer
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        Vector3 explosionCenter = transform.position;
        
        // Обрабатываем обычные меши
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            GameObject part = meshRenderer.gameObject;
            
            // Пропускаем сам объект игрока
            if (part == gameObject)
                continue;
            
            // Получаем MeshFilter для создания MeshCollider
            MeshFilter meshFilter = part.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;
            
            // Отсоединяем от игрока
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
            
            Debug.Log($"[Player] Часть {part.name} отсоединена и получила физику");
        }
        
        // Обрабатываем skinned меши (для персонажей с анимацией)
        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedMeshRenderers)
        {
            GameObject part = skinnedRenderer.gameObject;
            
            // Пропускаем сам объект игрока
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
            
            // Отсоединяем от игрока
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
            
            Debug.Log($"[Player] Skinned часть {part.name} отсоединена и получила физику");
        }
        
        // Отключаем animator чтобы не мешал
        if (animator != null)
            animator.enabled = false;
    }
    
    private void OnPlayerDamageTaken(float damage)
    {
        Debug.Log($"[Player] Получен урон: {damage}. Осталось здоровья: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
        
        // Здесь можно добавить визуальные/звуковые эффекты получения урона:
        // - Красная вспышка на экране
        // - Звук удара
        // - Дрожание камеры
        // - Анимация получения урона
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        GlobalEvents.PlayerMove.RemoveListener(SetInput);
        GlobalEvents.PlayerJump.RemoveListener(Jump);
        
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= OnPlayerDeath;
            healthComponent.OnDamageTaken -= OnPlayerDamageTaken;
        }
    }
    
    private void InitializeAutoSpawnService()
    {
        // Создаем новый экземпляр сервиса автоспавна
        new AutoSpawnService();
        
        // Инициализируем сервис с данным игроком
        AutoSpawnService.Instance?.Initialize(this);
    }

    private void SetInput(Vector2 direction)
    {
        // Блокируем управление после смерти
        if (_isDead)
        {
            _input = Vector2.zero;
            return;
        }
        
        _input = direction;
    }

    private void Jump()
    {
        // Блокируем прыжок после смерти
        if (_isDead)
            return;
        
        _jumpPressed = true;
    }

    void Update()
    {
        // Если мертв - не обрабатываем управление
        if (_isDead)
        {
            // Обновляем сервис автоспавна даже после смерти
            AutoSpawnService.Instance?.TickSpawnable(this, Time.deltaTime);
            return;
        }
        
        // Ввод
        //_input.x = Input.GetAxisRaw("Horizontal");   // A/D
        //_input.y = Input.GetAxisRaw("Vertical");     // W/S
        _input = _input.sqrMagnitude > 1 ? _input.normalized : _input;

        if (Input.GetKeyDown(KeyCode.Space)) _jumpPressed = true;

        // Движение
        HandleMovement();
        
        // Обновление сервиса автоспавна
        AutoSpawnService.Instance?.TickSpawnable(this, Time.deltaTime);
    }

    void HandleMovement()
    {
        if (!CameraPivot) return;

        float dt = Time.deltaTime;

        // 1) Проверка земли
        GroundCheck();

        // 2) Прыжок
        if (_jumpPressed)
        {
            if (_grounded || Time.time - _lastGroundTime <= coyoteTime)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _grounded = false;
            }
            _jumpPressed = false;
        }

        // 3) Горизонтальное движение относительно камеры
        Vector3 fwd = cameraPivot.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = cameraPivot.right; right.y = 0; right.Normalize();
        Vector3 moveDirection = (fwd * _input.y + right * _input.x) * moveSpeed;

        // 4) Плавный поворот персонажа в направлении движения
        if (moveDirection.sqrMagnitude > turnThreshold)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * dt);
        }

        // 5) Применение гравитации
        if (_grounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Небольшая сила вниз для прижимания к земле
        }
        else
        {
            _velocity.y += gravity * dt;
        }

        // 6) Движение CharacterController
        Vector3 move = moveDirection + Vector3.up * _velocity.y;
        controller.Move(move * dt);

        // 7) Обновление анимации
        UpdateAnimation();
    }


    // ---------- Ground Check ----------
    void GroundCheck()
    {
        // Проверка земли с помощью CharacterController
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

    // ---------- Animation Update ----------
    void UpdateAnimation()
    {
        if (animator == null) return;

        // Рассчитываем скорость движения по горизонтали
        Vector3 currentPosition = transform.position;
        Vector3 horizontalMovement = currentPosition - _lastPosition;
        horizontalMovement.y = 0; // Убираем вертикальную составляющую
        
        _currentSpeed = horizontalMovement.magnitude / Time.deltaTime;
        
        // Нормализуем скорость (0 = стоит, 1 = максимальная скорость)
        float normalizedSpeed = Mathf.Clamp01(_currentSpeed / moveSpeed);
        
        // Устанавливаем параметры аниматора
        animator.SetFloat(speedParameter, normalizedSpeed);
        animator.SetBool(isGroundedParameter, _grounded);
        
        // Синхронизируем скорость анимации с реальной скоростью
        if (normalizedSpeed > 0.1f) // Если движется
        {
            animator.speed = 1f + (normalizedSpeed - 0.5f) * animationSpeedMultiplier;
        }
        else // Если стоит
        {
            animator.speed = 1f;
        }
        
        // Обновляем позицию для следующего кадра
        _lastPosition = currentPosition;
    }

    private Transform CameraPivot
    {
        get
        {
            if (cameraPivot == null)
                cameraPivot = Camera.main.transform;
            return cameraPivot;
        }
    }
    
    // ========== ISpawnable Implementation ==========
    
    public string GetSpawnableID()
    {
        return "Player"; // У игрока всегда один ID
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
}
