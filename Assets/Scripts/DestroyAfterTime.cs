using UnityEngine;

/// <summary>
/// Уничтожает объект через заданное время
/// </summary>
public class DestroyAfterTime : MonoBehaviour
{
    [Tooltip("Время жизни объекта в секундах")]
    public float lifetime = 15f;
    
    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}

