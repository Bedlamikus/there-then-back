using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Менеджер аудио системы для управления пулами звуков взрывов
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("Audio Pools")]
    [Tooltip("Список пулов аудио для разных типов снарядов")]
    public List<AudioPool> audioPools = new List<AudioPool>();
    
    [Header("Settings")]
    [Tooltip("Автоматическое обновление пулов")]
    public bool autoUpdatePools = true;
    
    [Tooltip("Интервал обновления пулов (секунды)")]
    public float updateInterval = 0.1f;
    
    // Приватные переменные
    private Dictionary<ProjectileType, AudioPool> poolDictionary = new Dictionary<ProjectileType, AudioPool>();
    private float lastUpdateTime;
    
    void Start()
    {
        InitializeAudioPools();
        SubscribeToEvents();
    }
    
    void Update()
    {
        if (autoUpdatePools && Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateAllPools();
            lastUpdateTime = Time.time;
        }
    }
    
    /// <summary>
    /// Инициализирует все аудио пулы
    /// </summary>
    void InitializeAudioPools()
    {
        Debug.Log("[AudioManager] Initializing audio pools...");
        
        foreach (AudioPool pool in audioPools)
        {
            if (pool != null)
            {
                pool.Initialize(transform);
                poolDictionary[pool.projectileType] = pool;
                Debug.Log($"[AudioManager] Initialized pool for {pool.projectileType}");
            }
        }
        
        Debug.Log($"[AudioManager] Initialized {poolDictionary.Count} audio pools");
    }
    
    /// <summary>
    /// Подписывается на события взрывов и выстрелов
    /// </summary>
    void SubscribeToEvents()
    {
        GlobalEvents.ProjectileExploded.AddListener(OnProjectileExploded);
        GlobalEvents.WeaponFired.AddListener(OnWeaponFired);
        Debug.Log("[AudioManager] Subscribed to ProjectileExploded and WeaponFired events");
    }
    
    /// <summary>
    /// Обработчик события взрыва снаряда
    /// </summary>
    void OnProjectileExploded(Vector3 position, ProjectileType projectileType)
    {
        PlayExplosionSound(position, projectileType);
    }
    
    /// <summary>
    /// Обработчик события выстрела оружия
    /// </summary>
    void OnWeaponFired(Vector3 position, ProjectileType projectileType)
    {
        PlayFireSound(position, projectileType);
    }
    
    /// <summary>
    /// Проигрывает звук взрыва для указанного типа снаряда
    /// </summary>
    public void PlayExplosionSound(Vector3 position, ProjectileType projectileType)
    {
        if (poolDictionary.TryGetValue(projectileType, out AudioPool pool))
        {
            pool.PlayAt(position);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] No audio pool found for projectile type: {projectileType}");
        }
    }
    
    /// <summary>
    /// Проигрывает звук выстрела для указанного типа снаряда
    /// </summary>
    public void PlayFireSound(Vector3 position, ProjectileType projectileType)
    {
        if (poolDictionary.TryGetValue(projectileType, out AudioPool pool))
        {
            pool.PlayAt(position);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] No audio pool found for projectile type: {projectileType}");
        }
    }
    
    /// <summary>
    /// Обновляет все пулы
    /// </summary>
    void UpdateAllPools()
    {
        foreach (AudioPool pool in audioPools)
        {
            if (pool != null)
            {
                pool.UpdatePool();
            }
        }
    }
    
    /// <summary>
    /// Добавляет новый пул аудио
    /// </summary>
    public void AddAudioPool(AudioPool newPool)
    {
        if (newPool != null && !poolDictionary.ContainsKey(newPool.projectileType))
        {
            audioPools.Add(newPool);
            newPool.Initialize(transform);
            poolDictionary[newPool.projectileType] = newPool;
            Debug.Log($"[AudioManager] Added new audio pool for {newPool.projectileType}");
        }
    }
    
    /// <summary>
    /// Получает пул для указанного типа снаряда
    /// </summary>
    public AudioPool GetPool(ProjectileType projectileType)
    {
        poolDictionary.TryGetValue(projectileType, out AudioPool pool);
        return pool;
    }
    
    /// <summary>
    /// Устанавливает громкость для всех пулов
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        
        foreach (AudioPool pool in audioPools)
        {
            if (pool != null)
            {
                pool.volume = volume;
            }
        }
        
        Debug.Log($"[AudioManager] Set master volume to {volume}");
    }
    
    /// <summary>
    /// Останавливает все звуки
    /// </summary>
    public void StopAllSounds()
    {
        foreach (AudioPool pool in audioPools)
        {
            if (pool != null)
            {
                pool.Clear();
            }
        }
        
        Debug.Log("[AudioManager] Stopped all sounds");
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        GlobalEvents.ProjectileExploded.RemoveListener(OnProjectileExploded);
        GlobalEvents.WeaponFired.RemoveListener(OnWeaponFired);
        
        // Очищаем все пулы
        foreach (AudioPool pool in audioPools)
        {
            if (pool != null)
            {
                pool.Clear();
            }
        }
        
        Debug.Log("[AudioManager] Destroyed and cleaned up all audio pools");
    }
}
