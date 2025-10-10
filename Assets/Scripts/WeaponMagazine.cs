using UnityEngine;

/// <summary>
/// Управление магазином оружия - патроны, перезарядка, кулдауны
/// </summary>
public class WeaponMagazine
{
    private ProjectileAmmoData ammoData;
    private int currentAmmo;
    private float lastShootTime;
    private float reloadTimer;
    private bool isReloading;
    
    public WeaponMagazine(ProjectileAmmoData data)
    {
        ammoData = data;
        currentAmmo = data.magazineSize; // Начинаем с полным магазином
        lastShootTime = -data.shootCooldown; // Можем стрелять сразу
        reloadTimer = 0f;
        isReloading = false;
    }
    
    /// <summary>
    /// Обновление магазина (вызывать каждый кадр)
    /// </summary>
    public void Update(float deltaTime)
    {
        // Автоматическая перезарядка если магазин не полон
        if (currentAmmo < ammoData.magazineSize)
        {
            reloadTimer += deltaTime;
            
            // Когда прошло достаточно времени - добавляем один патрон
            if (reloadTimer >= ammoData.reloadTimePerShot)
            {
                currentAmmo++;
                reloadTimer = 0f;
                isReloading = currentAmmo < ammoData.magazineSize; // Продолжаем если не полон
            }
            else
            {
                isReloading = true;
            }
        }
        else
        {
            isReloading = false;
            reloadTimer = 0f;
        }
    }
    
    /// <summary>
    /// Можно ли выстрелить (есть патроны и прошел кулдаун)
    /// </summary>
    public bool CanShoot()
    {
        // Проверяем кулдаун
        if (Time.time - lastShootTime < ammoData.shootCooldown)
            return false;
        
        // Проверяем наличие патронов
        if (currentAmmo <= 0)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Выстрелить (уменьшает количество патронов)
    /// </summary>
    public bool TryShoot()
    {
        if (!CanShoot())
            return false;
        
        currentAmmo--;
        lastShootTime = Time.time;
        
        return true;
    }
    
    /// <summary>
    /// Получить текущее количество патронов
    /// </summary>
    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }
    
    /// <summary>
    /// Получить максимальное количество патронов
    /// </summary>
    public int GetMaxAmmo()
    {
        return ammoData.magazineSize;
    }
    
    /// <summary>
    /// Идет ли перезарядка
    /// </summary>
    public bool IsReloading()
    {
        return isReloading;
    }
    
    /// <summary>
    /// Получить прогресс перезарядки (0-1)
    /// </summary>
    public float GetReloadProgress()
    {
        if (!isReloading || ammoData.reloadTimePerShot <= 0f)
            return 1f;
        
        return reloadTimer / ammoData.reloadTimePerShot;
    }
    
    /// <summary>
    /// Получить время до следующего выстрела
    /// </summary>
    public float GetCooldownRemaining()
    {
        float remaining = ammoData.shootCooldown - (Time.time - lastShootTime);
        return Mathf.Max(0f, remaining);
    }
    
    /// <summary>
    /// Мгновенно перезарядить магазин (для читов/бонусов)
    /// </summary>
    public void ReloadInstantly()
    {
        currentAmmo = ammoData.magazineSize;
        reloadTimer = 0f;
        isReloading = false;
    }
    
    /// <summary>
    /// Получить данные о боеприпасах
    /// </summary>
    public ProjectileAmmoData GetAmmoData()
    {
        return ammoData;
    }
}

