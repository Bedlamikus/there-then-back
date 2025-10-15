using UnityEngine;

/// <summary>
/// Типы снарядов для идентификации партиклов и звуков
/// </summary>
public enum ProjectileType
{
    // Снаряды (для совместимости)
    Pistol,      // Пистолет
    Rocket,      // Ракета
    Dinamit,     // Динамит
    Rock,        // Камень
    
    // Звуки выстрелов
    ShootPistol,     // Звук выстрела пистолета
    ShootRocket,     // Звук выстрела ракеты
    ShootDinamit,    // Звук выстрела динамита
    ShootRock,       // Звук выстрела камня
    
    // Звуки взрывов
    ExplosionPistol,    // Звук взрыва пистолета
    ExplosionRocket,    // Звук взрыва ракеты
    ExplosionDinamit,   // Звук взрыва динамита
    ExplosionRock,      // Звук взрыва камня
    
    // Эффекты
    MuzzleFlash, // Вспышка выстрела (одинаковая для всех типов оружия)
    Custom       // Кастомный
}

