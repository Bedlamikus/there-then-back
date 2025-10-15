using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Пул партиклов для одного типа снаряда
/// </summary>
[System.Serializable]
public class ExplosionParticlePool
{
    [Header("Pool Settings")]
    [Tooltip("Тип снаряда для этого пула")]
    public ProjectileType projectileType;
    
    [Tooltip("Префаб партикла взрыва")]
    public GameObject particlePrefab;
    
    [Tooltip("Начальный размер пула")]
    public int initialPoolSize = 5;
    
    [Tooltip("Максимальный размер пула")]
    public int maxPoolSize = 20;
    
    // Внутренний список партиклов
    private List<GameObject> pool = new List<GameObject>();
    private Transform poolContainer;
    
    /// <summary>
    /// Инициализация пула
    /// </summary>
    public void Initialize(Transform container)
    {
        poolContainer = container;
        
        // Создаем начальное количество партиклов
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewParticle();
        }
        
        Debug.Log($"[Particle Pool] Инициализирован пул для {projectileType}: {initialPoolSize} партиклов");
    }
    
    /// <summary>
    /// Создает новый партикл в пуле
    /// </summary>
    private GameObject CreateNewParticle()
    {
        if (particlePrefab == null)
        {
            Debug.LogError($"[Particle Pool] Префаб партикла не назначен для типа {projectileType}!");
            return null;
        }
        
        GameObject particle = Object.Instantiate(particlePrefab, poolContainer);
        particle.name = $"{projectileType}_Explosion_{pool.Count}";
        particle.SetActive(false);
        
        // Добавляем компонент автоскрытия если его нет
        if (particle.GetComponent<ParticleAutoHide>() == null)
        {
            particle.AddComponent<ParticleAutoHide>();
        }
        
        pool.Add(particle);
        return particle;
    }
    
    /// <summary>
    /// Получить партикл из пула
    /// </summary>
    public GameObject GetParticle()
    {
        // Ищем неактивный партикл
        foreach (GameObject particle in pool)
        {
            if (particle != null && !particle.activeInHierarchy)
            {
                return particle;
            }
        }
        
        // Если все заняты, создаем новый (если не достигли лимита)
        if (pool.Count < maxPoolSize)
        {
            return CreateNewParticle();
        }
        
        // Если достигли лимита, берем первый (принудительно)
        Debug.LogWarning($"[Particle Pool] Пул {projectileType} переполнен! Переиспользуем партикл.");
        return pool[0];
    }
    
    /// <summary>
    /// Проиграть партикл в указанной позиции
    /// </summary>
    public void PlayAt(Vector3 position, Quaternion rotation)
    {
        GameObject particle = GetParticle();
        if (particle == null) return;
        
        // Позиционируем партикл
        particle.transform.position = position;
        particle.transform.rotation = rotation;
        
        // Получаем ParticleSystem
        ParticleSystem ps = particle.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();
        }
        
        // Активируем партикл (ParticleAutoHide автоматически скроет его после проигрывания)
        particle.SetActive(true);
    }
    
    /// <summary>
    /// Очистить пул
    /// </summary>
    public void Clear()
    {
        foreach (GameObject particle in pool)
        {
            if (particle != null)
            {
                Object.Destroy(particle);
            }
        }
        pool.Clear();
    }
}

