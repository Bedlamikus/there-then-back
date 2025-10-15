using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Менеджер пулов партиклов взрывов
/// Один эффект на тип снаряда используется для взрывов И вспышек выстрелов
/// Полностью работает через событийную систему (GlobalEvents)
/// </summary>
public class ExplosionParticleManager : MonoBehaviour
{
    [Header("Effect Pools")]
    [Tooltip("Список пулов партиклов для каждого типа снаряда (один эффект = взрыв + вспышка)")]
    public List<ExplosionParticlePool> particlePools = new List<ExplosionParticlePool>();
    
    [Header("Settings")]
    [Tooltip("Сохранять между сценами (DontDestroyOnLoad)")]
    public bool persistBetweenScenes = false;
    
    [Header("Container")]
    [Tooltip("Контейнер для хранения партиклов")]
    private Transform poolContainer;
    
    // Словарь для быстрого доступа к пулам по типу эффекта
    private Dictionary<ProjectileType, ExplosionParticlePool> poolDictionary = new Dictionary<ProjectileType, ExplosionParticlePool>();
    
    void Awake()
    {
        // Опционально сохраняем между сценами
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        // Создаем контейнер для пула
        poolContainer = new GameObject("ExplosionParticlePool").transform;
        poolContainer.SetParent(transform);
        
        // Инициализируем пулы
        InitializePools();
        
        Debug.Log("[Particle Manager] Инициализирован с " + particlePools.Count + " пулами");
    }
    
    void OnEnable()
    {
        // Подписываемся на событие
        GlobalEvents.ProjectileExploded.AddListener(OnProjectileExploded);
    }
    
    void OnDisable()
    {
        // Отписываемся от события
        GlobalEvents.ProjectileExploded.RemoveListener(OnProjectileExploded);
    }
    
    void OnDestroy()
    {
        // Очищаем пулы
        foreach (var pool in particlePools)
        {
            pool.Clear();
        }
    }
    
    /// <summary>
    /// Инициализация всех пулов
    /// </summary>
    void InitializePools()
    {
        poolDictionary.Clear();
        
        // Инициализируем все пулы эффектов
        foreach (var pool in particlePools)
        {
            pool.Initialize(poolContainer);
            poolDictionary[pool.projectileType] = pool;
        }
        
        Debug.Log($"[Particle Manager] Инициализировано {particlePools.Count} пулов эффектов");
    }
    
    /// <summary>
    /// Обработчик события взрыва снаряда
    /// </summary>
    void OnProjectileExploded(Vector3 position, ProjectileType effectType)
    {
        PlayEffect(position, effectType);
    }
    
    /// <summary>
    /// Проиграть эффект в указанной позиции
    /// </summary>
    public void PlayEffect(Vector3 position, ProjectileType effectType)
    {
        // Ищем соответствующий пул
        if (poolDictionary.TryGetValue(effectType, out ExplosionParticlePool pool))
        {
            pool.PlayAt(position, Quaternion.identity);
            Debug.Log($"[Particle Manager] Проигрывается эффект {effectType} в позиции {position}");
        }
        else
        {
            Debug.LogWarning($"[Particle Manager] Пул для типа эффекта {effectType} не найден!");
        }
    }
    
    /// <summary>
    /// Добавить новый пул во время выполнения
    /// </summary>
    public void AddPool(ExplosionParticlePool pool)
    {
        if (pool == null) return;
        
        // Инициализируем пул
        pool.Initialize(poolContainer);
        
        // Добавляем в списки
        particlePools.Add(pool);
        poolDictionary[pool.projectileType] = pool;
        
        Debug.Log($"[Particle Manager] Добавлен пул для {pool.projectileType}");
    }
    
    /// <summary>
    /// Получить пул по типу снаряда
    /// </summary>
    public ExplosionParticlePool GetPool(ProjectileType projectileType)
    {
        poolDictionary.TryGetValue(projectileType, out ExplosionParticlePool pool);
        return pool;
    }
}

