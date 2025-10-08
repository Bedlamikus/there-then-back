using UnityEngine;
using System.Collections.Generic;

public class EnemyWeaponController : MonoBehaviour
{
    [Header("Projectiles")]
    [Tooltip("Список префабов снарядов, которыми может стрелять бот")]
    public List<GameObject> availableProjectiles = new List<GameObject>();
    
    [Header("Shooting Settings")]
    [Tooltip("Интервал между выстрелами (секунды)")]
    public float shootInterval = 1f;
    
    [Tooltip("Скорость поворота турели (градусов в секунду)")]
    public float turretRotationSpeed = 180f;
    
    [Header("Aiming")]
    [Tooltip("Максимальный угол отклонения для стрельбы (градусы)")]
    public float maxAimAngle = 15f;
    
    [Tooltip("Скорость поворота ствола вверх/вниз (градусов в секунду)")]
    public float barrelPitchSpeed = 90f;
    
    [Tooltip("Минимальный угол наклона ствола (градусы)")]
    public float minBarrelPitch = -10f;
    
    [Tooltip("Максимальный угол наклона ствола (градусы)")]
    public float maxBarrelPitch = 45f;
    
    [Tooltip("Упреждение цели (учитывать скорость движения)")]
    public bool useLeadTarget = true;
    
    [Tooltip("Коэффициент упреждения")]
    public float leadAmount = 0.5f;
    
    // Компоненты
    private EnemyBot bot;
    
    // Состояние стрельбы
    private float lastShootTime;
    private int currentProjectileIndex = 0;
    
    void Start()
    {
        bot = GetComponent<EnemyBot>();
        
        if (bot == null)
        {
            Debug.LogError("[Bot Weapon] EnemyBot component not found!");
            enabled = false;
            return;
        }
        
        // Проверяем наличие снарядов
        if (availableProjectiles.Count == 0)
        {
            Debug.LogWarning($"[Bot Weapon] Бот {bot.GetSpawnableID()} не имеет снарядов!");
        }
        
        lastShootTime = Time.time;
    }
    
    void Update()
    {
        // Поворачиваем турель и ствол к цели
        AimAtTarget();
        
        // Стреляем если в состоянии Attack
        if (bot.GetCurrentState() == AIState.Attack)
        {
            TryShoot();
        }
    }
    
    /// <summary>
    /// Прицеливание турели и ствола на цель
    /// </summary>
    void AimAtTarget()
    {
        Transform target = bot.GetTarget();
        if (target == null) return;
        
        // Поворачиваем турель горизонтально
        RotateTurretToTarget(target);
        
        // Поворачиваем ствол вертикально
        RotateBarrelToTarget(target);
    }
    
    /// <summary>
    /// Поворачивает турель к цели (горизонтально)
    /// </summary>
    void RotateTurretToTarget(Transform target)
    {
        Transform turret = bot.GetTurret();
        
        if (turret == null || target == null) return;
        
        // Вычисляем направление к цели
        Vector3 targetPosition = target.position;
        
        // Упреждение цели (если включено)
        if (useLeadTarget)
        {
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                // Предсказываем позицию цели
                targetPosition += targetRb.velocity * leadAmount;
            }
        }
        
        Vector3 direction = targetPosition - turret.position;
        direction.y = 0; // Только горизонтальное вращение
        
        if (direction.sqrMagnitude > 0.01f)
        {
            // Вычисляем целевой угол в мировых координатах
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // ВАЖНО: Вращаем только по локальной оси Z (модель повернута)
            // Получаем текущий локальный угол Z
            float currentLocalZ = turret.localEulerAngles.z;
            
            // Вычисляем целевой локальный угол Z
            Vector3 localDirection = turret.parent != null 
                ? turret.parent.InverseTransformDirection(direction) 
                : direction;
            float targetLocalZ = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            
            // Добавляем смещение 90° для корректировки модели
            targetLocalZ += 90f;
            
            // Плавно поворачиваем по локальной Z
            float newLocalZ = Mathf.MoveTowardsAngle(currentLocalZ, targetLocalZ, turretRotationSpeed * Time.deltaTime);
            turret.localEulerAngles = new Vector3(0, 0, newLocalZ);
        }
    }
    
    /// <summary>
    /// Поворачивает ствол к цели (вертикально)
    /// </summary>
    void RotateBarrelToTarget(Transform target)
    {
        Transform barrel = bot.GetWeaponBarrel();
        
        if (barrel == null || target == null) return;
        
        // Вычисляем направление к цели
        Vector3 targetPosition = target.position;
        
        // Упреждение цели (если включено)
        if (useLeadTarget)
        {
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetPosition += targetRb.velocity * leadAmount;
            }
        }
        
        Vector3 directionToTarget = targetPosition - barrel.position;
        
        // Вычисляем угол наклона (pitch) в локальных координатах
        Vector3 localDirection = barrel.parent != null 
            ? barrel.parent.InverseTransformDirection(directionToTarget) 
            : directionToTarget;
        
        // Вычисляем целевой угол наклона
        float horizontalDistance = new Vector3(localDirection.x, 0, localDirection.z).magnitude;
        float targetPitch = -Mathf.Atan2(localDirection.y, horizontalDistance) * Mathf.Rad2Deg;
        
        // Ограничиваем угол наклона
        targetPitch = Mathf.Clamp(targetPitch, minBarrelPitch, maxBarrelPitch);
        
        // Получаем текущий угол наклона
        float currentPitch = barrel.localEulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f; // Нормализуем угол
        
        // Плавно поворачиваем ствол
        float newPitch = Mathf.MoveTowardsAngle(currentPitch, targetPitch, barrelPitchSpeed * Time.deltaTime);
        barrel.localEulerAngles = new Vector3(newPitch, 0, 0);
    }
    
    /// <summary>
    /// Пытается выстрелить
    /// </summary>
    void TryShoot()
    {
        // Проверяем интервал стрельбы
        if (Time.time - lastShootTime < shootInterval) return;
        
        // Проверяем наличие снарядов
        if (availableProjectiles.Count == 0) return;
        
        // ВАЖНО: Проверяем что бот полностью остановился
        if (!IsBotStopped()) return;
        
        // Проверяем что турель направлена на цель
        if (!IsAimedAtTarget()) return;
        
        // Стреляем
        Shoot();
        
        lastShootTime = Time.time;
    }
    
    /// <summary>
    /// Проверяет что бот полностью остановился
    /// </summary>
    bool IsBotStopped()
    {
        CharacterController controller = bot.GetComponent<CharacterController>();
        
        if (controller == null) return true;
        
        // Проверяем скорость движения
        Vector3 velocity = controller.velocity;
        velocity.y = 0; // Игнорируем вертикальную скорость
        
        // Считаем остановившимся если горизонтальная скорость < 0.1 м/с
        return velocity.magnitude < 0.1f;
    }
    
    /// <summary>
    /// Проверяет направлена ли турель на цель
    /// </summary>
    bool IsAimedAtTarget()
    {
        Transform turret = bot.GetTurret();
        Transform target = bot.GetTarget();
        
        if (turret == null || target == null) return false;
        
        Vector3 directionToTarget = (target.position - turret.position).normalized;
        directionToTarget.y = 0;
        
        Vector3 turretForward = turret.forward;
        turretForward.y = 0;
        turretForward.Normalize();
        
        float angle = Vector3.Angle(turretForward, directionToTarget);
        
        return angle <= maxAimAngle;
    }
    
    /// <summary>
    /// Выполняет выстрел
    /// </summary>
    void Shoot()
    {
        Transform shootPoint = bot.GetShootPoint();
        
        if (shootPoint == null)
        {
            Debug.LogWarning($"[Bot Weapon] ShootPoint не установлен для бота {bot.GetSpawnableID()}");
            return;
        }
        
        // Получаем текущий снаряд
        GameObject projectilePrefab = GetCurrentProjectile();
        
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[Bot Weapon] Нет доступных снарядов для бота {bot.GetSpawnableID()}");
            return;
        }
        
        // Создаем снаряд В ТОЧКЕ ВЫСТРЕЛА (на конце ствола)
        // Снаряд сам знает как ему лететь (использует свои параметры)
        GameObject projectile = Instantiate(projectilePrefab, shootPoint.position, shootPoint.rotation);
        
        Debug.Log($"[Bot Weapon] Бот {bot.GetSpawnableID()} выстрелил снарядом {projectilePrefab.name}");
    }
    
    /// <summary>
    /// Получает текущий снаряд для стрельбы
    /// </summary>
    GameObject GetCurrentProjectile()
    {
        if (availableProjectiles.Count == 0) return null;
        
        // Пока просто возвращаем текущий по индексу
        // Логика выбора будет добавлена позже
        currentProjectileIndex = Mathf.Clamp(currentProjectileIndex, 0, availableProjectiles.Count - 1);
        return availableProjectiles[currentProjectileIndex];
    }
    
    /// <summary>
    /// Устанавливает текущий тип снаряда
    /// </summary>
    public void SetProjectileType(int index)
    {
        if (index >= 0 && index < availableProjectiles.Count)
        {
            currentProjectileIndex = index;
            Debug.Log($"[Bot Weapon] Бот {bot.GetSpawnableID()} выбрал снаряд #{index}: {availableProjectiles[index].name}");
        }
    }
    
    /// <summary>
    /// Получает количество доступных типов снарядов
    /// </summary>
    public int GetProjectileCount()
    {
        return availableProjectiles.Count;
    }
}
