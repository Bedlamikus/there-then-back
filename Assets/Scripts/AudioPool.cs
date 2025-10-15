using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Пул аудио источников для переиспользования звуков взрывов
/// </summary>
[System.Serializable]
public class AudioPool
{
    [Header("Audio Settings")]
    [Tooltip("Тип снаряда для которого используется этот пул")]
    public ProjectileType projectileType;
    
    [Tooltip("Звук взрыва")]
    public AudioClip explosionSound;
    
    [Tooltip("Количество аудио источников в пуле")]
    public int poolSize = 5;
    
    [Tooltip("Громкость звука")]
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Tooltip("Случайное изменение высоты тона")]
    [Range(0f, 0.5f)]
    public float pitchVariation = 0.1f;
    
    [Tooltip("Базовый тон звука")]
    [Range(0.1f, 3f)]
    public float basePitch = 1f;
    
    [Tooltip("Максимальное расстояние слышимости")]
    public float maxDistance = 50f;
    
    [Tooltip("Минимальное расстояние для полной громкости")]
    public float minDistance = 1f;
    
    // Приватные переменные
    private List<AudioSource> audioSources = new List<AudioSource>();
    private Queue<AudioSource> availableSources = new Queue<AudioSource>();
    private Transform poolParent;
    
    /// <summary>
    /// Инициализирует пул аудио источников
    /// </summary>
    public void Initialize(Transform parent)
    {
        poolParent = parent;
        
        // Создаем GameObject для пула
        GameObject poolObject = new GameObject($"AudioPool_{projectileType}");
        poolObject.transform.SetParent(parent);
        poolParent = poolObject.transform;
        
        // Создаем аудио источники
        for (int i = 0; i < poolSize; i++)
        {
            CreateAudioSource();
        }
        
        Debug.Log($"[AudioPool] Initialized pool for {projectileType} with {poolSize} sources");
    }
    
    /// <summary>
    /// Создает новый аудио источник
    /// </summary>
    private void CreateAudioSource()
    {
        GameObject audioObject = new GameObject($"AudioSource_{audioSources.Count}");
        audioObject.transform.SetParent(poolParent);
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = explosionSound;
        audioSource.volume = volume;
        audioSource.pitch = basePitch;
        audioSource.maxDistance = maxDistance;
        audioSource.minDistance = minDistance;
        audioSource.spatialBlend = 1f; // 3D звук
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        // Деактивируем объект
        audioObject.SetActive(false);
        
        audioSources.Add(audioSource);
        availableSources.Enqueue(audioSource);
    }
    
    /// <summary>
    /// Проигрывает звук в указанной позиции
    /// </summary>
    public void PlayAt(Vector3 position)
    {
        if (explosionSound == null)
        {
            Debug.LogWarning($"[AudioPool] No explosion sound assigned for {projectileType}");
            return;
        }
        
        // Получаем доступный аудио источник
        AudioSource audioSource = GetAvailableSource();
        if (audioSource == null)
        {
            Debug.LogWarning($"[AudioPool] No available audio sources for {projectileType}");
            return;
        }
        
        // Настраиваем позицию и параметры
        audioSource.transform.position = position;
        
        // Добавляем случайное изменение высоты тона
        float randomPitch = basePitch + Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = Mathf.Clamp(randomPitch, 0.1f, 3f);
        
        // Проигрываем звук
        audioSource.Play();
        
        Debug.Log($"[AudioPool] Playing {projectileType} explosion sound at {position}");
    }
    
    /// <summary>
    /// Получает доступный аудио источник из пула
    /// </summary>
    private AudioSource GetAvailableSource()
    {
        // Ищем неактивный источник
        foreach (AudioSource source in audioSources)
        {
            if (!source.isPlaying && !source.gameObject.activeInHierarchy)
            {
                source.gameObject.SetActive(true);
                return source;
            }
        }
        
        // Если все источники заняты, возвращаем первый доступный из очереди
        if (availableSources.Count > 0)
        {
            AudioSource source = availableSources.Dequeue();
            source.gameObject.SetActive(true);
            return source;
        }
        
        return null;
    }
    
    /// <summary>
    /// Возвращает аудио источник в пул после завершения воспроизведения
    /// </summary>
    public void ReturnToPool(AudioSource audioSource)
    {
        if (audioSource != null)
        {
            audioSource.gameObject.SetActive(false);
            availableSources.Enqueue(audioSource);
        }
    }
    
    /// <summary>
    /// Проверяет и возвращает завершенные источники в пул
    /// </summary>
    public void UpdatePool()
    {
        foreach (AudioSource source in audioSources)
        {
            if (source.isPlaying && source.gameObject.activeInHierarchy && !source.isPlaying)
            {
                ReturnToPool(source);
            }
        }
    }
    
    /// <summary>
    /// Очищает пул
    /// </summary>
    public void Clear()
    {
        foreach (AudioSource source in audioSources)
        {
            if (source != null && source.gameObject != null)
            {
                Object.Destroy(source.gameObject);
            }
        }
        
        audioSources.Clear();
        availableSources.Clear();
    }
}
