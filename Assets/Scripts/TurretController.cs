using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Turret Parts")]
    public Transform turretBase;                  // Башня (горизонтальное вращение)
    public Transform weaponBarrel;                // Ствол оружия (вертикальное вращение)
    
    [Header("Rotation Axes")]
    public Vector3 turretRotationAxis = Vector3.up;        // Ось вращения башни (по умолчанию Y)
    public Vector3 weaponRotationAxis = Vector3.right;     // Ось вращения оружия (по умолчанию X)
    
    [Header("Rotation Speeds")]
    public float turretRotationSpeed = 90f;       // Скорость вращения башни (градусы/сек)
    public float weaponRotationSpeed = 60f;       // Скорость вращения оружия (градусы/сек)
    
    [Header("Weapon Limits")]
    public float minWeaponAngle = -30f;           // Минимальный угол подъема оружия
    public float maxWeaponAngle = 60f;            // Максимальный угол подъема оружия
    public float weaponAngleCorrection = 90f;     // Корректировка угла оружия (градусы)
    
    [Header("Target Settings")]
    public float targetUpdateRate = 30f;          // Частота обновления цели (раз в секунду)
    public float aimThreshold = 1f;               // Порог точности прицеливания (градусы)
    
    [Header("Smoothing")]
    public float smoothingFactor = 0.1f;           // Фактор сглаживания (0.01-0.5, меньше = плавнее)
    public float minMovementThreshold = 0.5f;      // Минимальный порог движения для обновления (градусы)
    
    private Vector3 currentTargetPosition;       // Текущая позиция цели
    private bool hasTarget = false;               // Есть ли активная цель
    private float lastTargetUpdateTime;           // Время последнего обновления цели
    
    // Текущие углы вращения
    private float currentTurretAngle = 0f;        // Текущий угол башни
    private float currentWeaponAngle = 0f;        // Текущий угол оружия
    
    // Целевые углы
    private float targetTurretAngle = 0f;         // Целевой угол башни
    private float targetWeaponAngle = 0f;         // Целевой угол оружия
    
    // Сглаженные углы
    private float smoothedTurretAngle = 0f;        // Сглаженный угол башни
    private float smoothedWeaponAngle = 0f;        // Сглаженный угол оружия

    void Start()
    {
        // Подписываемся на событие позиции для прицеливания
        GlobalEvents.ShootPosition.AddListener(SetTargetPosition);
        
        // Инициализируем текущие углы
        if (turretBase != null)
        {
            currentTurretAngle = GetCurrentTurretAngle();
            smoothedTurretAngle = currentTurretAngle;
        }
        if (weaponBarrel != null)
        {
            currentWeaponAngle = GetCurrentWeaponAngle();
            smoothedWeaponAngle = currentWeaponAngle;
        }
    }

    void OnDestroy()
    {
        // Отписываемся от события при уничтожении
        GlobalEvents.ShootPosition.RemoveListener(SetTargetPosition);
    }

    void Update()
    {
        // Обновляем прицеливание
        UpdateAiming();
        
        // Применяем вращения
        ApplyRotations();
    }

    void SetTargetPosition(Vector3 targetPos)
    {
        currentTargetPosition = targetPos;
        hasTarget = true;
        lastTargetUpdateTime = Time.time;
        
        // Вычисляем целевые углы
        CalculateTargetAngles(targetPos);
    }

    void CalculateTargetAngles(Vector3 targetPos)
    {
        if (turretBase == null || weaponBarrel == null) return;

        // Вычисляем направление к цели относительно башни
        Vector3 directionToTarget = targetPos - turretBase.position;
        directionToTarget.y = 0; // Игнорируем вертикальную составляющую для горизонтального вращения
        
        if (directionToTarget.magnitude > 0.1f)
        {
            // Вычисляем угол поворота башни относительно мировых координат
            float worldAngle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
            targetTurretAngle = worldAngle;
        }

        // Вычисляем угол подъема оружия
        Vector3 weaponDirection = targetPos - weaponBarrel.position;
        float distance = weaponDirection.magnitude;
        
        if (distance > 0.1f)
        {
            // Вычисляем угол подъема относительно горизонта
            float elevationAngle = Mathf.Asin(weaponDirection.y / distance) * Mathf.Rad2Deg;
            
            // Применяем настраиваемую корректировку угла
            elevationAngle += weaponAngleCorrection;
            
            // Ограничиваем угол в пределах min/max
            targetWeaponAngle = Mathf.Clamp(elevationAngle, minWeaponAngle, maxWeaponAngle);
        }
    }

    void UpdateAiming()
    {
        // Проверяем, нужно ли обновить цель
        if (hasTarget && Time.time - lastTargetUpdateTime > 1f / targetUpdateRate)
        {
            CalculateTargetAngles(currentTargetPosition);
        }
    }

    void ApplyRotations()
    {
        float dt = Time.deltaTime;
        
        // Вращение башни с сглаживанием
        if (turretBase != null && hasTarget)
        {
            float angleDifference = Mathf.DeltaAngle(currentTurretAngle, targetTurretAngle);
            
            if (Mathf.Abs(angleDifference) > minMovementThreshold)
            {
                float rotationStep = turretRotationSpeed * dt;
                if (Mathf.Abs(angleDifference) < rotationStep)
                {
                    currentTurretAngle = targetTurretAngle;
                }
                else
                {
                    currentTurretAngle += Mathf.Sign(angleDifference) * rotationStep;
                }
                
                // Применяем сглаживание
                smoothedTurretAngle = Mathf.LerpAngle(smoothedTurretAngle, currentTurretAngle, smoothingFactor);
                ApplyTurretRotation();
            }
        }
        
        // Вращение оружия с сглаживанием
        if (weaponBarrel != null && hasTarget)
        {
            float angleDifference = targetWeaponAngle - currentWeaponAngle;
            
            if (Mathf.Abs(angleDifference) > minMovementThreshold)
            {
                float rotationStep = weaponRotationSpeed * dt;
                if (Mathf.Abs(angleDifference) < rotationStep)
                {
                    currentWeaponAngle = targetWeaponAngle;
                }
                else
                {
                    currentWeaponAngle += Mathf.Sign(angleDifference) * rotationStep;
                }
                
                // Применяем сглаживание
                smoothedWeaponAngle = Mathf.Lerp(smoothedWeaponAngle, currentWeaponAngle, smoothingFactor);
                ApplyWeaponRotation();
            }
        }
    }

    void ApplyTurretRotation()
    {
        // Применяем сглаженное вращение башни вокруг оси Y (горизонтальное вращение)
        Quaternion rotation = Quaternion.Euler(0, smoothedTurretAngle, 0);
        turretBase.rotation = rotation;
    }

    void ApplyWeaponRotation()
    {
        // Применяем сглаженное вращение оружия вокруг заданной оси
        Quaternion rotation = Quaternion.AngleAxis(smoothedWeaponAngle, weaponRotationAxis);
        weaponBarrel.localRotation = rotation;
    }

    float GetCurrentTurretAngle()
    {
        if (turretBase == null) return 0f;
        
        // Извлекаем угол вращения из Transform
        Vector3 forward = turretBase.forward;
        forward.y = 0;
        forward.Normalize();
        
        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        return angle;
    }

    float GetCurrentWeaponAngle()
    {
        if (weaponBarrel == null) return 0f;
        
        // Извлекаем угол вращения вокруг заданной оси
        Vector3 forward = weaponBarrel.TransformDirection(Vector3.forward);
        return Vector3.SignedAngle(Vector3.forward, forward, weaponRotationAxis);
    }

    // Публичные методы для внешнего доступа
    public void SetTurretTarget(Vector3 targetPosition)
    {
        SetTargetPosition(targetPosition);
    }

    public bool IsAiming()
    {
        if (!hasTarget) return false;
        
        float turretDiff = Mathf.Abs(Mathf.DeltaAngle(currentTurretAngle, targetTurretAngle));
        float weaponDiff = Mathf.Abs(targetWeaponAngle - currentWeaponAngle);
        
        return turretDiff > aimThreshold || weaponDiff > aimThreshold;
    }

    public Vector3 GetCurrentTargetPosition()
    {
        return hasTarget ? currentTargetPosition : Vector3.zero;
    }

    public float GetTurretAngle()
    {
        return currentTurretAngle;
    }

    public float GetWeaponAngle()
    {
        return currentWeaponAngle;
    }
}
