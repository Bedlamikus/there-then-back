using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
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

    [Header("Animation")]
    public Animator animator;                    // Аниматор персонажа
    public string speedParameter = "Speed";      // Параметр скорости в аниматоре
    public string isGroundedParameter = "IsGrounded"; // Параметр нахождения на земле
    public float animationSpeedMultiplier = 1f;  // Множитель скорости анимации

    CharacterController controller;
    Vector2 _input;                              // Ввод WASD (-1..1)
    bool _jumpPressed;
    bool _grounded;
    float _lastGroundTime;
    Vector3 _velocity;                           // Вертикальная скорость для прыжка и гравитации

    // Анимация
    float _currentSpeed;                         // Текущая скорость движения
    Vector3 _lastPosition;                       // Позиция в предыдущем кадре для расчета скорости

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        GlobalEvents.PlayerMove.AddListener(SetInput);
        GlobalEvents.PlayerJump.AddListener(Jump);

        // Инициализация анимации
        if (animator == null)
            animator = GetComponent<Animator>();
        
        _lastPosition = transform.position;
        
        // Инициализация сервиса автоспавна
        InitializeAutoSpawnService();
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
        _input = direction;
    }

    private void Jump()
    {
        _jumpPressed = true;
    }

    void Update()
    {
        // Ввод
        //_input.x = Input.GetAxisRaw("Horizontal");   // A/D
        //_input.y = Input.GetAxisRaw("Vertical");     // W/S
        _input = _input.sqrMagnitude > 1 ? _input.normalized : _input;

        if (Input.GetKeyDown(KeyCode.Space)) _jumpPressed = true;

        // Движение
        HandleMovement();
        
        // Обновление сервиса автоспавна
        AutoSpawnService.Instance?.Tick(Time.deltaTime);
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
}
