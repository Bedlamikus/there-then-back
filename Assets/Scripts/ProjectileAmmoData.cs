using UnityEngine;

/// <summary>
/// Данные о боеприпасах для снаряда
/// Добавьте этот компонент на префаб снаряда
/// </summary>
public class ProjectileAmmoData : MonoBehaviour
{
    [Header("Magazine Settings")]
    [Tooltip("Максимальное количество снарядов в обойме")]
    public int magazineSize = 10;
    
    [Tooltip("Кулдаун между выстрелами (секунды)")]
    public float shootCooldown = 0.5f;
    
    [Tooltip("Время перезарядки одного снаряда в магазин (секунды)")]
    public float reloadTimePerShot = 1.0f;
    
    [Header("Info")]
    [Tooltip("Название снаряда для UI")]
    public string projectileName = "Снаряд";
    
    [Tooltip("Иконка снаряда для UI")]
    public Sprite projectileIcon;
}

