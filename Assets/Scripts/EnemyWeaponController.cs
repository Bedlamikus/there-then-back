using UnityEngine;
using System.Collections.Generic;

public class EnemyWeaponController : MonoBehaviour
{
    [Header("Projectiles")]
    [Tooltip("Список префабов снарядов, которыми может стрелять бот")]
    public List<GameObject> availableProjectiles = new List<GameObject>();
    
    [Header("Shooting Settings (DEPRECATED - используйте ProjectileAmmoData на префабах)")]
    [Tooltip("Интервал между выстрелами (секунды) - используется если на снаряде нет ProjectileAmmoData")]
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
    
    // Магазины для каждого типа снаряда
    private Dictionary<GameObject, WeaponMagazine> magazines = new Dictionary<GameObject, WeaponMagazine>();
    
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
        
        // Инициализируем магазины для каждого типа снаряда
        InitializeMagazines();
        
        lastShootTime = Time.time;
    }
    
    /// <summary>
    /// Инициализация магазинов для всех снарядов
    /// </summary>
    void InitializeMagazines()
    {
        magazines.Clear();
        
        foreach (GameObject projectilePrefab in availableProjectiles)
        {
            if (projectilePrefab == null) continue;
            
            // Получаем данные о боеприпасах
            ProjectileAmmoData ammoData = projectilePrefab.GetComponent<ProjectileAmmoData>();
            
            if (ammoData != null)
            {
                // Создаем магазин для этого снаряда
                magazines[projectilePrefab] = new WeaponMagazine(ammoData);
            }
        }
    }
    
    void Update()
    {
        // Обновляем все магазины (автоматическая перезарядка)
        UpdateMagazines();
        
        // Поворачиваем турель и ствол к цели
        AimAtTarget();
        
        // Стреляем если в состоянии Attack
        if (bot.GetCurrentState() == AIState.Attack)
        {
            TryShoot();
        }
    }
    
    /// <summary>
    /// Обновление всех магазинов
    /// </summary>
    void UpdateMagazines()
    {
        foreach (var magazine in magazines.Values)
        {
            magazine.Update(Time.deltaTime);
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
        
        Vector3 directionToTarget = targetPosition - turret.position;
        directionToTarget.y = 0; // Только горизонтальное вращение
        
        if (directionToTarget.sqrMagnitude > 0.01f)
        {
            // Получаем текущий локальный угол Z и нормализуем его
            float currentLocalZ = turret.localEulerAngles.z;
            if (currentLocalZ > 180f) currentLocalZ -= 360f; // Нормализуем: [0, 360] → [-180, 180]
            
            // Вычисляем целевой локальный угол Z
            Vector3 localDirection = turret.parent != null 
                ? turret.parent.InverseTransformDirection(directionToTarget) 
                : directionToTarget;
            
            // Для вращения вокруг Z-оси: используем X (вправо) и Y (вперед) компоненты
            float targetLocalZ = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            
            // Добавляем смещение 90° для корректировки модели
            targetLocalZ += 90f;
            
            // Нормализуем целевой угол: [0, 360] → [-180, 180]
            if (targetLocalZ > 180f) targetLocalZ -= 360f;
            if (targetLocalZ < -180f) targetLocalZ += 360f;
            
            // Плавно поворачиваем турель по локальной Z оси
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
        
        // Вычисляем pitch относительно горизонтали (в мировых координатах)
        // Горизонтальное расстояние (XZ плоскость)
        float horizontalDistance = new Vector3(directionToTarget.x, 0, directionToTarget.z).magnitude;
        
        // Вертикальная составляющая
        float verticalDistance = directionToTarget.y;
        
        // Вычисляем угол наклона (инвертирован для правильного направления)
        float targetPitch = -Mathf.Atan2(verticalDistance, horizontalDistance) * Mathf.Rad2Deg;
        
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
        // Проверяем наличие снарядов
        if (availableProjectiles.Count == 0) return;
        
        // Получаем текущий снаряд
        GameObject projectilePrefab = GetCurrentProjectile();
        if (projectilePrefab == null) return;
        
        // Проверяем магазин для этого снаряда
        if (magazines.TryGetValue(projectilePrefab, out WeaponMagazine magazine))
        {
            // Используем магазин - проверяем патроны и кулдаун
            if (!magazine.CanShoot()) return;
        }
        else
        {
            // Fallback на старую систему (если нет ProjectileAmmoData на префабе)
            if (Time.time - lastShootTime < shootInterval) return;
        }
        
        // ВАЖНО: Проверяем что бот полностью остановился
        if (!IsBotStopped()) return;
        
        // Проверяем что турель направлена на цель
        if (!IsAimedAtTarget()) return;
        
        // Стреляем
        Shoot(projectilePrefab, magazine);
        
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
    void Shoot(GameObject projectilePrefab, WeaponMagazine magazine)
    {
        Transform shootPoint = bot.GetShootPoint();
        
        if (shootPoint == null)
        {
            Debug.LogWarning($"[Bot Weapon] ShootPoint не установлен для бота {bot.GetSpawnableID()}");
            return;
        }
        
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[Bot Weapon] Нет доступных снарядов для бота {bot.GetSpawnableID()}");
            return;
        }
        
        // Используем патрон из магазина
        if (magazine != null)
        {
            if (!magazine.TryShoot())
            {
                // Не удалось выстрелить (нет патронов или кулдаун)
                return;
            }
        }
        
        // Создаем снаряд В ТОЧКЕ ВЫСТРЕЛА (на конце ствола)
        // Снаряд сам знает как ему лететь (использует свои параметры)
        GameObject projectile = Instantiate(projectilePrefab, shootPoint.position, shootPoint.rotation);
    }
    
    /// <summary>
    /// Получает текущий снаряд для стрельбы (умный выбор)
    /// </summary>
    GameObject GetCurrentProjectile()
    {
        if (availableProjectiles.Count == 0) return null;
        
        Transform target = bot.GetTarget();
        if (target == null)
        {
            // Нет цели - используем первый доступный
            return availableProjectiles[0];
        }
        
        // Вычисляем угол стрельбы
        Transform barrel = bot.GetWeaponBarrel();
        if (barrel == null)
        {
            return availableProjectiles[0];
        }
        
        Vector3 directionToTarget = target.position - barrel.position;
        float verticalAngle = Vector3.SignedAngle(Vector3.forward, directionToTarget, Vector3.right);
        
        // Проверяем есть ли путь до игрока
        EnemyPathfindingService pathfinding = bot.GetPathfindingService();
        bool hasPath = pathfinding != null && pathfinding.CurrentPath != null && pathfinding.CurrentPath.Count > 0;
        
        // СТРАТЕГИЯ 1: Стрельба вниз - используем стандартный снаряд (пушечное ядро)
        if (verticalAngle < -15f) // Стреляем вниз больше чем на 15 градусов
        {
            GameObject standardProjectile = FindProjectileByType("Dinamit");
            if (standardProjectile != null)
            {
                return standardProjectile;
            }
        }
        
        // СТРАТЕГИЯ 2: Нет пути до игрока, но игрок в зоне видимости - пробуем динамит с шансом
        if (!hasPath && Random.value < 0.3f) // 30% шанс
        {
            GameObject dinamitProjectile = FindProjectileByType("Dinamit");
            if (dinamitProjectile != null)
            {
                return dinamitProjectile;
            }
        }
        
        // СТРАТЕГИЯ 3: Стрельба вверх или прямо - используем ракету или пулю
        GameObject rocketProjectile = FindProjectileByType("Rocket");
        if (rocketProjectile != null)
        {
            return rocketProjectile;
        }
        
        GameObject pistolProjectile = FindProjectileByType("Pistol");
        if (pistolProjectile != null)
        {
            return pistolProjectile;
        }
        
        // Fallback - первый доступный
        return availableProjectiles[0];
    }
    
    /// <summary>
    /// Ищет снаряд по типу (по имени префаба)
    /// </summary>
    GameObject FindProjectileByType(string typeName)
    {
        foreach (GameObject projectile in availableProjectiles)
        {
            if (projectile != null && projectile.name.Contains(typeName))
            {
                return projectile;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Устанавливает текущий тип снаряда
    /// </summary>
    public void SetProjectileType(int index)
    {
        if (index >= 0 && index < availableProjectiles.Count)
        {
            currentProjectileIndex = index;
        }
    }
    
    /// <summary>
    /// Получает количество доступных типов снарядов
    /// </summary>
    public int GetProjectileCount()
    {
        return availableProjectiles.Count;
    }
    
    /// <summary>
    /// Получает магазин для конкретного снаряда
    /// </summary>
    public WeaponMagazine GetMagazine(GameObject projectilePrefab)
    {
        if (magazines.TryGetValue(projectilePrefab, out WeaponMagazine magazine))
        {
            return magazine;
        }
        return null;
    }
    
    /// <summary>
    /// Получает магазин текущего снаряда
    /// </summary>
    public WeaponMagazine GetCurrentMagazine()
    {
        GameObject currentProjectile = GetCurrentProjectile();
        if (currentProjectile == null) return null;
        
        return GetMagazine(currentProjectile);
    }
}
