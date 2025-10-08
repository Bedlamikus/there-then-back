using UnityEngine;

/// <summary>
/// Интерфейс для всех сущностей, которые могут респавниться (игрок, боты)
/// </summary>
public interface ISpawnable
{
    /// <summary>
    /// Уникальный ID сущности для системы спавна
    /// </summary>
    string GetSpawnableID();
    
    /// <summary>
    /// Получить Transform сущности
    /// </summary>
    Transform GetTransform();
    
    /// <summary>
    /// Получить GameObject сущности
    /// </summary>
    GameObject GetGameObject();
    
    /// <summary>
    /// Проверить, находится ли сущность на земле
    /// </summary>
    bool IsGrounded();
}

