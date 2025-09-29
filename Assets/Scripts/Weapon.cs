using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    public Transform shootPosition;               // Точка откуда стреляем
    public GameObject projectilePrefab;           // Префаб снаряда Projectile
    public float shootForce = 10f;                // Сила выстрела
    
    [Header("Magazine System")]
    public int maxAmmo = 3;                       // Максимальное количество патронов в магазине
    public float reloadTime = 0.5f;              // Время перезарядки одного патрона
    
    private int currentAmmo;                       // Текущее количество патронов в магазине
    private float lastReloadTime;                 // Время последней перезарядки
    private bool isReloading = false;             // Флаг процесса перезарядки

    void Start()
    {
        // Подписываемся на событие стрельбы
        GlobalEvents.Shoot.AddListener(OnShoot);
        
        // Подписываемся на событие выбора снаряда
        GlobalEvents.ProjectileSelected.AddListener(OnProjectileSelected);
        
        // Если не указана точка стрельбы, используем позицию оружия
        if (shootPosition == null)
            shootPosition = transform;
            
        // Инициализируем магазин полным количеством патронов
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        // Обрабатываем перезарядку
        HandleReload();
    }

    void OnDestroy()
    {
        // Отписываемся от события при уничтожении
        GlobalEvents.Shoot.RemoveListener(OnShoot);
        GlobalEvents.ProjectileSelected.RemoveListener(OnProjectileSelected);
    }

    void OnShoot()
    {
        // Проверяем наличие патронов
        if (currentAmmo <= 0)
        {
            Debug.Log("Weapon: Нет патронов в магазине!");
            return;
        }

        // Проверяем наличие префаба снаряда
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Weapon: projectilePrefab не назначен!");
            return;
        }

        // Создаем снаряд
        GameObject projectile = Instantiate(projectilePrefab, shootPosition.position, shootPosition.rotation);
        
        // Применяем силу к снаряду
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            projectileRb.AddForce(shootPosition.forward * shootForce, ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning("Weapon: Projectile не имеет Rigidbody!");
        }

        // Тратим патрон
        currentAmmo--;
        
        // Начинаем перезарядку если магазин пуст
        if (currentAmmo <= 0)
        {
            StartReload();
        }
    }

    void OnProjectileSelected(GameObject newProjectilePrefab)
    {
        if (newProjectilePrefab == null)
        {
            Debug.LogWarning("Weapon: Получен пустой префаб снаряда!");
            return;
        }

        // Меняем префаб снаряда
        projectilePrefab = newProjectilePrefab;
        
        // Полностью заряжаем магазин при смене снаряда
        currentAmmo = maxAmmo;
        isReloading = false;
        
        Debug.Log($"Weapon: Выбран новый снаряд {newProjectilePrefab.name}, магазин заряжен!");
    }

    void HandleReload()
    {
        // Если идет перезарядка и прошло достаточно времени
        if (isReloading && Time.time - lastReloadTime >= reloadTime)
        {
            // Добавляем один патрон
            currentAmmo++;
            
            // Уведомляем системы о пополнении патрона
            GlobalEvents.AmmoReloaded.Invoke(currentAmmo);
            
            // Если магазин не полный, продолжаем перезарядку
            if (currentAmmo < maxAmmo)
            {
                lastReloadTime = Time.time;
            }
            else
            {
                // Магазин полный, заканчиваем перезарядку
                isReloading = false;
            }
        }
    }

    void StartReload()
    {
        if (!isReloading && currentAmmo < maxAmmo)
        {
            isReloading = true;
            lastReloadTime = Time.time;
        }
    }

    // Публичные методы для внешнего доступа
    public int GetCurrentAmmo() => currentAmmo;
    public int GetMaxAmmo() => maxAmmo;
    public bool IsReloading() => isReloading;
    public bool CanShoot() => currentAmmo > 0 && !isReloading;
}
