using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Контроллер оружия игрока - управляет вращением турели и ствола к точке прицеливания
/// </summary>
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Projectiles")]
    [Tooltip("Список префабов снарядов, которыми может стрелять игрок")]
    public List<GameObject> availableProjectiles = new List<GameObject>();
    
    [Header("Weapon Components")]
    [Tooltip("Турель (вращается горизонтально вокруг Z оси в локальных координатах)")]
    public Transform turret;
    
    [Tooltip("Ствол оружия (вращается вертикально вокруг X оси в локальных координатах)")]
    public Transform weaponBarrel;
    
    [Tooltip("Точка выстрела на конце ствола")]
    public Transform shootPoint;
    
    [Header("Rotation Settings")]
    [Tooltip("Скорость поворота турели (градусов в секунду)")]
    public float turretRotationSpeed = 180f;
    
    [Tooltip("Скорость поворота ствола вверх/вниз (градусов в секунду)")]
    public float barrelPitchSpeed = 90f;
    
    [Tooltip("Минимальный угол наклона ствола (градусы)")]
    public float minBarrelPitch = -10f;
    
    [Tooltip("Максимальный угол наклона ствола (градусы)")]
    public float maxBarrelPitch = 45f;
    
    [Header("Turret Offset")]
    [Tooltip("Смещение турели от модели игрока (для правильного вращения)")]
    public float turretModelOffset = 90f;
    
    // Текущая точка прицеливания
    private Vector3 currentAimPoint;
    private bool hasAimPoint = false;
    
    // Магазины для каждого типа снаряда
    private Dictionary<GameObject, WeaponMagazine> magazines = new Dictionary<GameObject, WeaponMagazine>();
    private int currentProjectileIndex = 0;
    
    // Состояние стрельбы
    private bool isShootButtonPressed = false;
    
    void Start()
    {
        // Подписываемся на событие обновления точки прицеливания
        GlobalEvents.CameraAimPoint.AddListener(OnAimPointUpdated);
        
        // Подписываемся на событие выстрела (старое, для совместимости)
        GlobalEvents.Shoot.AddListener(OnShootRequested);
        
        // Подписываемся на новые события стрельбы
        GlobalEvents.ShootPressed.AddListener(OnShootPressed);
        GlobalEvents.ShootReleased.AddListener(OnShootReleased);
        
        // Подписываемся на событие выбора снаряда
        GlobalEvents.ProjectileSelected.AddListener(OnProjectileSelected);
        
        if (turret == null)
        {
            Debug.LogWarning("[Player Weapon] Turret не установлен!");
        }
        
        if (weaponBarrel == null)
        {
            Debug.LogWarning("[Player Weapon] WeaponBarrel не установлен!");
        }
        
        if (shootPoint == null)
        {
            Debug.LogWarning("[Player Weapon] ShootPoint не установлен!");
        }
        
        if (availableProjectiles.Count == 0)
        {
            Debug.LogError("[Player Weapon] СПИСОК СНАРЯДОВ ПУСТ! Добавьте префабы снарядов в инспекторе PlayerWeaponController!");
        }
        
        // Инициализируем магазины для всех снарядов
        InitializeMagazines();
        
        // Уведомляем UI о текущем выбранном снаряде с небольшой задержкой
        StartCoroutine(NotifyCurrentProjectileSelectedDelayed());
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
    
    /// <summary>
    /// Уведомить UI о текущем выбранном снаряде с задержкой
    /// </summary>
    System.Collections.IEnumerator NotifyCurrentProjectileSelectedDelayed()
    {
        // Ждем один кадр, чтобы UI успел инициализироваться
        yield return new WaitForEndOfFrame();
        
        GameObject currentProjectile = GetCurrentProjectile();
        if (currentProjectile != null)
        {
            // Отправляем событие выбора текущего снаряда
            GlobalEvents.ProjectileSelected.Invoke(currentProjectile);
            Debug.Log($"[Player Weapon] Уведомление UI о текущем снаряде: {currentProjectile.name}");
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        GlobalEvents.CameraAimPoint.RemoveListener(OnAimPointUpdated);
        GlobalEvents.Shoot.RemoveListener(OnShootRequested);
        GlobalEvents.ShootPressed.RemoveListener(OnShootPressed);
        GlobalEvents.ShootReleased.RemoveListener(OnShootReleased);
        GlobalEvents.ProjectileSelected.RemoveListener(OnProjectileSelected);
    }
    
    void Update()
    {
        // Обновляем все магазины (автоматическая перезарядка)
        UpdateMagazines();
        
        if (!hasAimPoint) return;
        
        // Поворачиваем турель и ствол к точке прицеливания
        AimAtTarget(currentAimPoint);
        
        // Автоматическая стрельба при удержании кнопки
        if (isShootButtonPressed)
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
    /// Обработчик события обновления точки прицеливания
    /// </summary>
    void OnAimPointUpdated(Vector3 aimPoint)
    {
        currentAimPoint = aimPoint;
        hasAimPoint = true;
    }
    
    /// <summary>
    /// Обработчик события выстрела (старое, для совместимости)
    /// </summary>
    void OnShootRequested()
    {
        TryShoot();
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки стрельбы
    /// </summary>
    void OnShootPressed()
    {
        isShootButtonPressed = true;
    }
    
    /// <summary>
    /// Обработчик отпускания кнопки стрельбы
    /// </summary>
    void OnShootReleased()
    {
        isShootButtonPressed = false;
    }
    
    /// <summary>
    /// Обработчик события выбора снаряда
    /// </summary>
    void OnProjectileSelected(GameObject selectedProjectile)
    {
        if (selectedProjectile == null) return;
        
        // Ищем индекс выбранного снаряда в списке
        int index = availableProjectiles.IndexOf(selectedProjectile);
        
        if (index >= 0)
        {
            currentProjectileIndex = index;
        }
    }
    
    /// <summary>
    /// Попытка выстрелить
    /// </summary>
    void TryShoot()
    {
        if (availableProjectiles.Count == 0) return;
        
        // Получаем текущий снаряд
        GameObject projectilePrefab = GetCurrentProjectile();
        if (projectilePrefab == null) return;
        
        // Проверяем магазин для этого снаряда
        if (magazines.TryGetValue(projectilePrefab, out WeaponMagazine magazine))
        {
            // Используем магазин - проверяем патроны и кулдаун
            if (!magazine.CanShoot()) return;
            
            // Выстреливаем
            if (magazine.TryShoot())
            {
                Shoot(projectilePrefab);
            }
        }
        else
        {
            // Fallback - стреляем без ограничений (если нет ProjectileAmmoData)
            Shoot(projectilePrefab);
        }
    }
    
    /// <summary>
    /// Выполняет выстрел
    /// </summary>
    void Shoot(GameObject projectilePrefab)
    {
        if (shootPoint == null)
        {
            Debug.LogWarning("[Player Weapon] ShootPoint не установлен!");
            return;
        }
        
        // Создаем снаряд в точке выстрела
        GameObject projectile = Instantiate(projectilePrefab, shootPoint.position, shootPoint.rotation);
    }
    
    /// <summary>
    /// Получает текущий снаряд для стрельбы
    /// </summary>
    GameObject GetCurrentProjectile()
    {
        if (availableProjectiles.Count == 0) return null;
        
        currentProjectileIndex = Mathf.Clamp(currentProjectileIndex, 0, availableProjectiles.Count - 1);
        return availableProjectiles[currentProjectileIndex];
    }
    
    /// <summary>
    /// Прицеливание турели и ствола на цель
    /// </summary>
    void AimAtTarget(Vector3 targetPoint)
    {
        // Поворачиваем турель горизонтально
        float turretCurrentZ, turretTargetZ, turretNewZ;
        RotateTurretToTarget(targetPoint, out turretCurrentZ, out turretTargetZ, out turretNewZ);
        
        // Поворачиваем ствол вертикально
        float barrelCurrentPitch, barrelTargetPitch, barrelNewPitch;
        RotateBarrelToTarget(targetPoint, out barrelCurrentPitch, out barrelTargetPitch, out barrelNewPitch);
        
        // Логирование отключено для производительности
        // Раскомментируйте если нужна отладка:
        /*
        bool turretChanging = Mathf.Abs(Mathf.DeltaAngle(turretCurrentZ, turretTargetZ)) > 1f;
        bool barrelChanging = Mathf.Abs(Mathf.DeltaAngle(barrelCurrentPitch, barrelTargetPitch)) > 1f;
        
        if (turretChanging || barrelChanging)
        {
            Debug.Log($"[Player Weapon] Цель: {targetPoint} | Турель: {turretCurrentZ:F1}°→{turretTargetZ:F1}° (новый={turretNewZ:F1}°) | Ствол: {barrelCurrentPitch:F1}°→{barrelTargetPitch:F1}° (новый={barrelNewPitch:F1}°)");
        }
        */
    }
    
    /// <summary>
    /// Поворачивает турель к цели (горизонтально)
    /// </summary>
    void RotateTurretToTarget(Vector3 targetPoint, out float currentAngle, out float targetAngle, out float newAngle)
    {
        currentAngle = 0f;
        targetAngle = 0f;
        newAngle = 0f;
        
        if (turret == null) return;
        
        // Вычисляем направление к цели (только по горизонтали)
        Vector3 directionToTarget = targetPoint - turret.position;
        directionToTarget.y = 0; // Только горизонтальное вращение
        
        if (directionToTarget.sqrMagnitude < 0.01f) return;
        
        // Получаем текущий локальный угол Z и нормализуем его
        currentAngle = turret.localEulerAngles.z;
        if (currentAngle > 180f) currentAngle -= 360f; // Нормализуем: [0, 360] → [-180, 180]
        
        // Вычисляем целевой локальный угол Z
        // Для вращения вокруг Z-оси используем X и Y компоненты локального направления
        Vector3 localDirection = turret.parent != null 
            ? turret.parent.InverseTransformDirection(directionToTarget) 
            : directionToTarget;
        
        // Для вращения вокруг Z-оси: используем X (вправо) и Y (вперед) компоненты
        targetAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
        
        // Добавляем смещение для корректировки модели
        targetAngle += turretModelOffset;
        
        // Нормализуем целевой угол: [0, 360] → [-180, 180]
        if (targetAngle > 180f) targetAngle -= 360f;
        if (targetAngle < -180f) targetAngle += 360f;
        
        // Плавно поворачиваем турель по локальной Z оси
        newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turretRotationSpeed * Time.deltaTime);
        turret.localEulerAngles = new Vector3(0, 0, newAngle);
    }
    
    /// <summary>
    /// Поворачивает ствол к цели (вертикально)
    /// </summary>
    void RotateBarrelToTarget(Vector3 targetPoint, out float currentAngle, out float targetAngle, out float newAngle)
    {
        currentAngle = 0f;
        targetAngle = 0f;
        newAngle = 0f;
        
        if (weaponBarrel == null) return;
        
        // Вычисляем направление к цели в мировых координатах
        Vector3 directionToTarget = targetPoint - weaponBarrel.position;
        
        // Вычисляем pitch относительно горизонтали (в мировых координатах)
        // Горизонтальное расстояние (XZ плоскость)
        float horizontalDistance = new Vector3(directionToTarget.x, 0, directionToTarget.z).magnitude;
        
        // Вертикальная составляющая
        float verticalDistance = directionToTarget.y;
        
        // Вычисляем угол наклона (инвертирован для правильного направления)
        // Цель выше (Y > 0) → отрицательный угол → ствол поднимается
        // Цель ниже (Y < 0) → положительный угол → ствол опускается
        targetAngle = -Mathf.Atan2(verticalDistance, horizontalDistance) * Mathf.Rad2Deg;
        
        // Ограничиваем угол наклона
        targetAngle = Mathf.Clamp(targetAngle, minBarrelPitch, maxBarrelPitch);
        
        // Получаем текущий угол наклона
        currentAngle = weaponBarrel.localEulerAngles.x;
        if (currentAngle > 180f) currentAngle -= 360f; // Нормализуем угол
        
        // Плавно поворачиваем ствол
        newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, barrelPitchSpeed * Time.deltaTime);
        weaponBarrel.localEulerAngles = new Vector3(newAngle, 0, 0);
    }
    
    /// <summary>
    /// Получает текущую точку прицеливания
    /// </summary>
    public Vector3 GetAimPoint()
    {
        return currentAimPoint;
    }
    
    /// <summary>
    /// Получает турель
    /// </summary>
    public Transform GetTurret()
    {
        return turret;
    }
    
    /// <summary>
    /// Получает ствол
    /// </summary>
    public Transform GetWeaponBarrel()
    {
        return weaponBarrel;
    }
    
    /// <summary>
    /// Получает точку выстрела
    /// </summary>
    public Transform GetShootPoint()
    {
        return shootPoint;
    }
    
    /// <summary>
    /// Переключает на следующий снаряд
    /// </summary>
    public void SwitchToNextProjectile()
    {
        if (availableProjectiles.Count <= 1) return;
        
        currentProjectileIndex = (currentProjectileIndex + 1) % availableProjectiles.Count;
    }
    
    /// <summary>
    /// Переключает на предыдущий снаряд
    /// </summary>
    public void SwitchToPreviousProjectile()
    {
        if (availableProjectiles.Count <= 1) return;
        
        currentProjectileIndex--;
        if (currentProjectileIndex < 0)
            currentProjectileIndex = availableProjectiles.Count - 1;
    }
    
    /// <summary>
    /// Устанавливает конкретный снаряд
    /// </summary>
    public void SetProjectileIndex(int index)
    {
        if (index >= 0 && index < availableProjectiles.Count)
        {
            currentProjectileIndex = index;
        }
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
    
    /// <summary>
    /// Получает индекс текущего снаряда
    /// </summary>
    public int GetCurrentProjectileIndex()
    {
        return currentProjectileIndex;
    }
    
    /// <summary>
    /// Получает список всех доступных снарядов
    /// </summary>
    public List<GameObject> GetAvailableProjectiles()
    {
        return availableProjectiles;
    }
}

