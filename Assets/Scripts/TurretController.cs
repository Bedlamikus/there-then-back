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
    public float weaponAngleCorrection = 0f;      // Корректировка начального угла оружия (градусы)
    
    [Header("Joystick Control")]
    public float joystickSensitivity = 1f;       // Чувствительность джойстика
    public float maxRotationSpeed = 180f;         // Максимальная скорость вращения (градусы/сек)
    
    [Header("Smoothing")]
    public float smoothingFactor = 0.1f;           // Фактор сглаживания (0.01-0.5, меньше = плавнее)
    public float minMovementThreshold = 0.5f;      // Минимальный порог движения для обновления (градусы)
    
    private Vector2 joystickInput = Vector2.zero; // Ввод от джойстика
    private bool hasJoystickInput = false;        // Есть ли ввод от джойстика
    
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
        // Подписываемся на событие вращения башни от джойстика
        GlobalEvents.TurretRotation.AddListener(SetJoystickInput);
        
        // Инициализируем текущие углы с учетом начального поворота
        if (turretBase != null)
        {
            currentTurretAngle = GetCurrentTurretAngle();
            smoothedTurretAngle = currentTurretAngle;
            targetTurretAngle = currentTurretAngle; // Устанавливаем целевую позицию равной текущей
        }
        if (weaponBarrel != null)
        {
            currentWeaponAngle = GetCurrentWeaponAngle() + weaponAngleCorrection;
            smoothedWeaponAngle = currentWeaponAngle;
            targetWeaponAngle = currentWeaponAngle; // Устанавливаем целевую позицию равной текущей
        }
    }

    void OnDestroy()
    {
        // Отписываемся от события при уничтожении
        GlobalEvents.TurretRotation.RemoveListener(SetJoystickInput);
    }

    void Update()
    {
        // Обновляем вращение башни
        UpdateTurretRotation();
        
        // Применяем вращения
        ApplyRotations();
    }

    void SetJoystickInput(Vector2 input)
    {
        joystickInput = input;
        hasJoystickInput = input.magnitude > 0.1f;
    }

    void UpdateTurretRotation()
    {
        if (!hasJoystickInput) return;

        // Горизонтальное вращение башни (X ось джойстика) - относительное вращение
        if (turretBase != null)
        {
            float turretRotationSpeed = joystickInput.x * joystickSensitivity * maxRotationSpeed;
            targetTurretAngle = currentTurretAngle + turretRotationSpeed * Time.deltaTime;
        }

        // Вертикальное вращение оружия (Y ось джойстика) - относительное вращение
        if (weaponBarrel != null)
        {
            float weaponRotationSpeed = joystickInput.y * joystickSensitivity * maxRotationSpeed;
            float newWeaponAngle = currentWeaponAngle + weaponRotationSpeed * Time.deltaTime;
            
            // Ограничиваем угол оружия относительно начального положения с учетом корректировки
            float correctedMinAngle = minWeaponAngle + weaponAngleCorrection;
            float correctedMaxAngle = maxWeaponAngle + weaponAngleCorrection;
            targetWeaponAngle = Mathf.Clamp(newWeaponAngle, correctedMinAngle, correctedMaxAngle);
        }
    }


    void ApplyRotations()
    {
        float dt = Time.deltaTime;
        
        // Вращение башни с сглаживанием (относительные углы)
        if (turretBase != null && hasJoystickInput)
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
        
        // Вращение оружия с сглаживанием (относительные углы)
        if (weaponBarrel != null && hasJoystickInput)
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
        // Применяем сглаженное вращение башни в локальных углах вокруг заданной оси
        Quaternion rotation = Quaternion.AngleAxis(smoothedTurretAngle, turretRotationAxis);
        turretBase.localRotation = rotation;
    }

    void ApplyWeaponRotation()
    {
        // Применяем сглаженное вращение оружия в локальных углах вокруг заданной оси
        Quaternion rotation = Quaternion.AngleAxis(smoothedWeaponAngle, weaponRotationAxis);
        weaponBarrel.localRotation = rotation;
    }

    float GetCurrentTurretAngle()
    {
        if (turretBase == null) return 0f;
        
        // Извлекаем локальный угол вращения башни вокруг заданной оси
        Vector3 localEuler = turretBase.localEulerAngles;
        
        // Определяем угол в зависимости от оси вращения
        float angle = 0f;
        if (turretRotationAxis == Vector3.right) // X ось
            angle = localEuler.x;
        else if (turretRotationAxis == Vector3.up) // Y ось
            angle = localEuler.y;
        else if (turretRotationAxis == Vector3.forward) // Z ось
            angle = localEuler.z;
        
        // Нормализуем угол в диапазон -180..180
        if (angle > 180f) angle -= 360f;
        
        return angle;
    }

    float GetCurrentWeaponAngle()
    {
        if (weaponBarrel == null) return 0f;
        
        // Извлекаем локальный угол вращения оружия вокруг заданной оси
        Vector3 localEuler = weaponBarrel.localEulerAngles;
        
        // Определяем угол в зависимости от оси вращения
        float angle = 0f;
        if (weaponRotationAxis == Vector3.right) // X ось
            angle = localEuler.x;
        else if (weaponRotationAxis == Vector3.up) // Y ось
            angle = localEuler.y;
        else if (weaponRotationAxis == Vector3.forward) // Z ось
            angle = localEuler.z;
        
        // Нормализуем угол в диапазон -180..180
        if (angle > 180f) angle -= 360f;
        
        return angle;
    }


    // Публичные методы для внешнего доступа
    public bool IsRotating()
    {
        return hasJoystickInput;
    }

    public float GetTurretAngle()
    {
        return currentTurretAngle;
    }

    public float GetWeaponAngle()
    {
        return currentWeaponAngle;
    }

    public Vector2 GetJoystickInput()
    {
        return joystickInput;
    }
}
