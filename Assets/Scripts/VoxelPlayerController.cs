using UnityEngine;
using System.Collections;

[RequireComponent(typeof(HealthComponent))]
public class VoxelPlayerController : MonoBehaviour
{
    [Header("Voxel Player Configuration")]
    public VoxelBotConfig config;
    
    [Header("Input")]
    public FloatingJoystick movementJoystick;
    public FloatingJoystick cameraJoystick;
    
    [Header("Camera")]
    public Transform cameraPivot;
    public float cameraSensitivity = 2f;
    
    [Header("Health")]
    public HealthComponent healthComponent;
    
    [Header("Death Effect")]
    [Tooltip("Сила разлета частей при смерти")]
    public float deathExplosionForce = 5f;
    [Tooltip("Радиус разлета частей")]
    public float deathExplosionRadius = 2f;
    [Tooltip("Время жизни частей после смерти (секунды)")]
    public float partsLifetime = 15f;
    
    // Состояние
    private bool isDead = false;
    private Vector3 currentVelocity = Vector3.zero;
    private bool isGrounded = true;
    private bool isJumping = false;
    private float jumpStartTime;
    private Vector3 jumpStartPosition;
    private Vector3 jumpTargetPosition;
    
    // Данные игрока
    private VoxelBotData playerData;
    private string playerId = "Player";
    
    // Система кулинга
    public float viewDistance = 100f;
    
    void Start()
    {
        InitializePlayer();
    }
    
    void Update()
    {
        if (isDead) return;
        
        HandleInput();
        HandleMovement();
        HandleCamera();
        UpdatePlayerData();
    }
    
    /// <summary>
    /// Инициализирует игрока
    /// </summary>
    void InitializePlayer()
    {
        // Создаем данные игрока
        playerData = new VoxelBotData(playerId, transform.position, config);
        
        // Регистрируем в базе данных
        VoxelBotDatabase.Instance.RegisterBot(playerId, transform.position, config);
        
        // Инициализируем здоровье
        InitializeHealth();
        
        // Удаляем старые компоненты
        CharacterController oldController = GetComponent<CharacterController>();
        if (oldController != null)
        {
            DestroyImmediate(oldController);
        }
        
        Rigidbody oldRigidbody = GetComponent<Rigidbody>();
        if (oldRigidbody != null)
        {
            DestroyImmediate(oldRigidbody);
        }
        
        Debug.Log("[VoxelPlayerController] Initialized voxel player");
    }
    
    /// <summary>
    /// Инициализирует систему здоровья
    /// </summary>
    void InitializeHealth()
    {
        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
        }
        
        if (healthComponent != null)
        {
            healthComponent.OnDeath += OnPlayerDeath;
            healthComponent.OnDamageTaken += OnPlayerDamageTaken;
            Debug.Log($"[VoxelPlayerController] Health initialized: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
        }
    }
    
    /// <summary>
    /// Обрабатывает ввод
    /// </summary>
    void HandleInput()
    {
        // Обрабатываем движение
        Vector2 moveInput = Vector2.zero;
        if (movementJoystick != null)
        {
            moveInput = movementJoystick.Direction;
        }
        
        // Обрабатываем прыжок
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isJumping)
        {
            StartJump();
        }
        
        // Вызываем события для совместимости
        GlobalEvents.PlayerMove?.Invoke(moveInput);
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GlobalEvents.PlayerJump?.Invoke();
        }
    }
    
    /// <summary>
    /// Обрабатывает движение
    /// </summary>
    void HandleMovement()
    {
        Vector2 moveInput = Vector2.zero;
        if (movementJoystick != null)
        {
            moveInput = movementJoystick.Direction;
        }
        
        if (isJumping)
        {
            HandleJump();
            return;
        }
        
        if (moveInput.magnitude > 0.1f)
        {
            // Вычисляем направление движения относительно камеры
            Vector3 moveDirection = GetCameraRelativeDirection(moveInput);
            
            // Двигаемся
            Vector3 newPosition = transform.position + moveDirection * config.moveSpeed * Time.deltaTime;
            
            // Проверяем столкновение с потолком
            if (IsHittingCeiling(newPosition))
            {
                // Возвращаем притяжение земли
                newPosition.y = transform.position.y - config.gravity * Time.deltaTime;
            }
            
            transform.position = newPosition;
        }
        else
        {
            // Применяем гравитацию когда не двигаемся
            if (!isGrounded)
            {
                Vector3 newPosition = transform.position + Vector3.down * config.fallSpeed * Time.deltaTime;
                transform.position = newPosition;
            }
        }
        
        // Проверяем касание земли
        isGrounded = IsGrounded();
    }
    
    /// <summary>
    /// Обрабатывает прыжок
    /// </summary>
    void HandleJump()
    {
        if (!isJumping) return;
        
        float jumpTime = Time.time - jumpStartTime;
        float jumpDuration = 0.5f; // Длительность прыжка
        
        if (jumpTime >= jumpDuration)
        {
            // Прыжок завершен
            transform.position = jumpTargetPosition;
            isJumping = false;
            return;
        }
        
        // Интерполируем позицию
        float t = jumpTime / jumpDuration;
        Vector3 currentPos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, t);
        
        // Добавляем дугу прыжка
        float jumpArc = Mathf.Sin(t * Mathf.PI) * config.jumpHeight;
        currentPos.y += jumpArc;
        
        transform.position = currentPos;
    }
    
    /// <summary>
    /// Начинает прыжок
    /// </summary>
    void StartJump()
    {
        if (isJumping) return;
        
        isJumping = true;
        jumpStartTime = Time.time;
        jumpStartPosition = transform.position;
        
        // Вычисляем целевую позицию прыжка
        Vector2 moveInput = Vector2.zero;
        if (movementJoystick != null)
        {
            moveInput = movementJoystick.Direction;
        }
        
        Vector3 jumpDirection = GetCameraRelativeDirection(moveInput);
        jumpTargetPosition = transform.position + jumpDirection * config.jumpDistance;
        
        Debug.Log("[VoxelPlayerController] Starting jump");
    }
    
    /// <summary>
    /// Обрабатывает камеру
    /// </summary>
    void HandleCamera()
    {
        if (cameraJoystick == null || cameraPivot == null) return;
        
        Vector2 cameraInput = cameraJoystick.Direction;
        
        if (cameraInput.magnitude > 0.1f)
        {
            // Поворачиваем камеру
            float rotationY = cameraInput.x * cameraSensitivity * Time.deltaTime;
            cameraPivot.Rotate(0, rotationY, 0);
        }
    }
    
    /// <summary>
    /// Получает направление движения относительно камеры
    /// </summary>
    Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (cameraPivot == null) return new Vector3(input.x, 0, input.y);
        
        Vector3 forward = cameraPivot.forward;
        Vector3 right = cameraPivot.right;
        
        forward.y = 0;
        right.y = 0;
        
        forward.Normalize();
        right.Normalize();
        
        return right * input.x + forward * input.y;
    }
    
    /// <summary>
    /// Обновляет данные игрока
    /// </summary>
    void UpdatePlayerData()
    {
        playerData.UpdatePosition(transform.position);
        playerData.isGrounded = isGrounded;
        playerData.isJumping = isJumping;
        playerData.currentHeight = GetCurrentHeight();
        
        VoxelBotDatabase.Instance.UpdateBotPosition(playerId, transform.position);
    }
    
    /// <summary>
    /// Проверяет касание земли
    /// </summary>
    bool IsGrounded()
    {
        Vector3Int belowVoxel = VoxelWorld.WorldToVoxel(transform.position + Vector3.down * config.groundCheckHeight);
        return VoxelWorld.IsVoxelSolid(belowVoxel);
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
    /// Обработчик смерти игрока
    /// </summary>
    void OnPlayerDeath()
    {
        if (isDead) return;
        
        isDead = true;
        
        Debug.Log("[VoxelPlayerController] Player died");
        
        // Взрываем части игрока
        ExplodePlayerParts();
        
        // Отключаем компонент
        enabled = false;
    }
    
    /// <summary>
    /// Обработчик получения урона
    /// </summary>
    void OnPlayerDamageTaken(float damage)
    {
        Debug.Log($"[VoxelPlayerController] Player took {damage} damage");
    }
    
    /// <summary>
    /// Взрывает части игрока при смерти
    /// </summary>
    void ExplodePlayerParts()
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
    /// Получает данные игрока
    /// </summary>
    public VoxelBotData GetPlayerData()
    {
        return playerData;
    }
    
    /// <summary>
    /// Проверяет попадание в игрока
    /// </summary>
    public bool CheckHit(Vector3 hitPoint, float radius)
    {
        return playerData.bounds.Contains(hitPoint) || 
               Vector3.Distance(hitPoint, playerData.bounds.ClosestPoint(hitPoint)) <= radius;
    }
    
    /// <summary>
    /// Проверяет жив ли игрок
    /// </summary>
    public bool IsDead => isDead;
}
