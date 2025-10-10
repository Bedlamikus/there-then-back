using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI контроллер для отображения магазина оружия
/// </summary>
public class MagazineUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Контейнер для патронов (с VerticalLayoutGroup)")]
    public Transform ammoContainer;
    
    [Tooltip("Префаб для одного патрона")]
    public GameObject ammoSlotPrefab;
    
    [Tooltip("Контроллер оружия игрока (необязательно, найдется автоматически)")]
    public PlayerWeaponController weaponController;
    
    [Header("Settings")]
    [Tooltip("Интервал обновления UI (секунды)")]
    public float updateInterval = 0.1f;
    
    [Tooltip("Спрайт патрона по умолчанию")]
    public Sprite defaultAmmoSprite;
    
    // Приватные переменные
    private List<AmmoSlotUI> ammoSlots = new List<AmmoSlotUI>();
    private float lastUpdateTime = 0f;
    private GameObject lastProjectile = null;
    private int lastMaxAmmo = 0;
    private float weaponSearchTimer = 0f;
    private float weaponSearchInterval = 0.5f;  // Ищем оружие каждые 0.5 секунды
    
    void Start()
    {
        if (ammoContainer == null)
        {
            Debug.LogError("[Magazine UI] Ammo Container не назначен!");
            enabled = false;
            return;
        }
        
        if (ammoSlotPrefab == null)
        {
            Debug.LogError("[Magazine UI] Ammo Slot Prefab не назначен!");
            enabled = false;
            return;
        }
        
        // Подписываемся на событие выбора снаряда
        GlobalEvents.ProjectileSelected.AddListener(OnProjectileChanged);
        
        // Пытаемся найти оружие сразу
        TryFindWeaponController();
    }
    
    void OnDestroy()
    {
        GlobalEvents.ProjectileSelected.RemoveListener(OnProjectileChanged);
    }
    
    void Update()
    {
        // Если оружие не найдено, пытаемся найти периодически
        if (weaponController == null)
        {
            weaponSearchTimer += Time.deltaTime;
            if (weaponSearchTimer >= weaponSearchInterval)
            {
                TryFindWeaponController();
                weaponSearchTimer = 0f;
            }
            return;
        }
        
        // Обновляем UI с заданным интервалом
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateMagazineDisplay();
            lastUpdateTime = Time.time;
        }
    }
    
    /// <summary>
    /// Пытается найти контроллер оружия игрока
    /// </summary>
    void TryFindWeaponController()
    {
        if (weaponController != null) return;
        
        weaponController = FindObjectOfType<PlayerWeaponController>();
        
        if (weaponController != null)
        {
            Debug.Log("[Magazine UI] PlayerWeaponController найден! UI магазина активирован.");
            // Сразу обновляем отображение
            UpdateMagazineDisplay();
        }
    }
    
    /// <summary>
    /// Обработчик смены снаряда
    /// </summary>
    void OnProjectileChanged(GameObject newProjectile)
    {
        // Принудительно обновляем UI при смене снаряда
        UpdateMagazineDisplay();
    }
    
    /// <summary>
    /// Обновляет отображение магазина
    /// </summary>
    void UpdateMagazineDisplay()
    {
        if (weaponController == null) return;
        
        // Получаем текущий магазин
        WeaponMagazine magazine = weaponController.GetCurrentMagazine();
        
        if (magazine == null)
        {
            // Нет магазина - скрываем все слоты
            HideAllSlots();
            return;
        }
        
        // Получаем текущий снаряд
        GameObject currentProjectile = weaponController.GetAvailableProjectiles()[weaponController.GetCurrentProjectileIndex()];
        
        // Получаем информацию о магазине
        int currentAmmo = magazine.GetCurrentAmmo();
        int maxAmmo = magazine.GetMaxAmmo();
        bool isReloading = magazine.IsReloading();
        
        // Проверяем нужно ли пересоздать слоты (сменился снаряд или размер магазина)
        if (currentProjectile != lastProjectile || maxAmmo != lastMaxAmmo)
        {
            RecreateSlots(maxAmmo, currentProjectile);
            lastProjectile = currentProjectile;
            lastMaxAmmo = maxAmmo;
        }
        
        // Обновляем состояние слотов
        UpdateSlots(currentAmmo, maxAmmo, isReloading);
    }
    
    /// <summary>
    /// Пересоздает слоты для нового магазина
    /// </summary>
    void RecreateSlots(int maxAmmo, GameObject projectile)
    {
        // Удаляем старые слоты
        ClearSlots();
        
        // Получаем спрайт патрона
        Sprite ammoSprite = GetProjectileSprite(projectile);
        
        // Создаем новые слоты
        for (int i = 0; i < maxAmmo; i++)
        {
            GameObject slotObj = Instantiate(ammoSlotPrefab, ammoContainer);
            AmmoSlotUI slot = slotObj.GetComponent<AmmoSlotUI>();
            
            if (slot != null)
            {
                slot.SetSprite(ammoSprite);
                ammoSlots.Add(slot);
            }
        }
    }
    
    /// <summary>
    /// Обновляет состояние существующих слотов
    /// </summary>
    void UpdateSlots(int currentAmmo, int maxAmmo, bool isReloading)
    {
        for (int i = 0; i < ammoSlots.Count && i < maxAmmo; i++)
        {
            bool isLoaded = i < currentAmmo;
            bool isCurrentlyReloading = isReloading && i == currentAmmo; // Следующий патрон заряжается
            
            ammoSlots[i].SetState(isLoaded, isCurrentlyReloading);
        }
    }
    
    /// <summary>
    /// Получает спрайт для снаряда
    /// </summary>
    Sprite GetProjectileSprite(GameObject projectile)
    {
        if (projectile == null) return defaultAmmoSprite;
        
        // Пытаемся получить спрайт из ProjectileAmmoData
        ProjectileAmmoData ammoData = projectile.GetComponent<ProjectileAmmoData>();
        if (ammoData != null && ammoData.projectileIcon != null)
        {
            return ammoData.projectileIcon;
        }
        
        return defaultAmmoSprite;
    }
    
    /// <summary>
    /// Удаляет все слоты
    /// </summary>
    void ClearSlots()
    {
        foreach (var slot in ammoSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        ammoSlots.Clear();
    }
    
    /// <summary>
    /// Скрывает все слоты
    /// </summary>
    void HideAllSlots()
    {
        foreach (var slot in ammoSlots)
        {
            if (slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }
    }
}

