using UnityEngine;

public class EnemyMovementService
{
    private CharacterController controller;
    private Transform transform;
    private EnemyMovementConfig config;
    private Animator animator;
    
    // Состояние движения
    private Vector3 _velocity;
    private bool _grounded;
    private float _lastGroundTime;
    private Vector3 _lastPosition;
    
    // Система прыжка
    private bool _jumpPressed;
    private bool isPreparingJump;
    private bool isJumpCooldown;
    private float jumpPrepareTimer;
    private float jumpCooldownTimer;
    private bool wasInAirLastFrame; // Для отслеживания приземления
    
    public bool IsGrounded => _grounded;
    public Vector3 Velocity => _velocity;
    public bool IsPreparingJump => isPreparingJump;
    
    public EnemyMovementService(CharacterController controller, Transform transform, EnemyMovementConfig config, Animator animator)
    {
        this.controller = controller;
        this.transform = transform;
        this.config = config;
        this.animator = animator;
        _lastPosition = transform.position;
    }
    
    public void Update()
    {
        GroundCheck();
        UpdateJumpSystem();
        UpdateAnimation();
    }
    
    public void HandleMovement(Vector3 moveDirection)
    {
        float dt = Time.deltaTime;
        
        // Обработка прыжка
        if (_jumpPressed && CanJump())
        {
            if (_grounded || Time.time - _lastGroundTime <= config.coyoteTime)
            {
                _velocity.y = Mathf.Sqrt(config.jumpHeight * -2f * config.gravity);
                _grounded = false;
            }
            _jumpPressed = false;
        }
        
        // Плавный поворот к направлению движения
        if (moveDirection.sqrMagnitude > config.turnThreshold)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, config.turnSpeed * dt);
        }
        
        // Применение гравитации
        if (_grounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        else
        {
            _velocity.y += config.gravity * dt;
        }
        
        // Движение (применяем скорость к направлению)
        Vector3 move = moveDirection * config.moveSpeed + Vector3.up * _velocity.y;
        controller.Move(move * dt);
    }
    
    public void InitiateJump()
    {
        if (!isPreparingJump && !isJumpCooldown)
        {
            isPreparingJump = true;
            jumpPrepareTimer = 0f;
        }
    }
    
    public bool IsPreparingJumpOrCooldown()
    {
        return isPreparingJump || isJumpCooldown;
    }
    
    private void GroundCheck()
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
    
    private void UpdateJumpSystem()
    {
        // Обработка подготовки к прыжку (0.1s стоп перед прыжком)
        if (isPreparingJump)
        {
            jumpPrepareTimer += Time.deltaTime;
            
            if (jumpPrepareTimer >= config.jumpPrepareTime)
            {
                _jumpPressed = true;
                isPreparingJump = false;
                jumpPrepareTimer = 0f;
                // НЕ включаем кулдаун сразу - бот должен двигаться в воздухе!
            }
        }
        
        // Отслеживаем приземление и включаем кулдаун ПОСЛЕ приземления
        if (!_grounded)
        {
            wasInAirLastFrame = true;
        }
        else if (wasInAirLastFrame && _grounded)
        {
            // Только что приземлились - включаем кулдаун
            wasInAirLastFrame = false;
            isJumpCooldown = true;
            jumpCooldownTimer = 0f;
        }
        
        // Обработка кулдауна после приземления
        if (isJumpCooldown)
        {
            jumpCooldownTimer += Time.deltaTime;
            
            if (jumpCooldownTimer >= config.jumpCooldownTime)
            {
                isJumpCooldown = false;
                jumpCooldownTimer = 0f;
            }
        }
    }
    
    private void UpdateAnimation()
    {
        if (animator == null) return;
        
        // Вычисляем текущую скорость
        Vector3 horizontalVelocity = transform.position - _lastPosition;
        float speed = horizontalVelocity.magnitude / Time.deltaTime;
        
        // Обновляем параметры анимации
        animator.SetFloat(config.speedParameter, speed);
        animator.SetBool(config.isGroundedParameter, _grounded);
        
        _lastPosition = transform.position;
    }
    
    private bool CanJump()
    {
        return _grounded || Time.time - _lastGroundTime <= config.coyoteTime;
    }
}
