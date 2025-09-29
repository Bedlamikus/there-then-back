using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float radius = 6f;
    public float maxDamage = 10f;
    public bool destroyOnHit = true;

    [Header("Timer Settings")]
    public float explosionDelay = 3f;              // Задержка до взрыва (секунды)
    public float blinkInterval = 0.2f;             // Интервал мигания (секунды)
    public float whiteIntensity = 0.5f;            // Интенсивность белого цвета (0-1)
    public AnimationCurve blinkCurve;              // Кривая мигания для плавности

    [Header("Chain Reaction")]
    public float chainReactionRadius = 8f;         // Радиус обнаружения других снарядов
    public LayerMask projectileLayerMask = -1;    // Слой для поиска других снарядов
    public float chainReactionDelay = 0.1f;        // Задержка между цепными взрывами

    [Header("Lifetime Settings")]
    public float maxLifetime = 10f;                // Максимальное время жизни снаряда (секунды)
    public bool destroyOnLifetime = true;          // Уничтожать снаряд по истечении времени жизни
    
    private bool isTimerActive = false;            // Активен ли таймер
    protected bool hasExploded = false;              // Уже взорвался ли снаряд
    private Coroutine timerCoroutine;              // Корутина таймера
    private Coroutine blinkCoroutine;              // Корутина мигания
    private Coroutine lifetimeCoroutine;           // Корутина времени жизни
    private Renderer projectileRenderer;           // Рендерер снаряда
    private Material originalMaterial;             // Исходный материал
    private Material blinkMaterial;                // Материал для мигания
    private Color originalColor;                   // Исходный цвет
    
    // Уникальный идентификатор снаряда
    private int projectileId;
    private static int nextProjectileId = 1;
    
    // Время жизни
    private float spawnTime;                       // Время создания снаряда

    protected virtual void Start()
    {
        // Назначаем уникальный ID снаряду
        projectileId = nextProjectileId++;
        
        // Запоминаем время создания
        spawnTime = Time.time;
        
        // Получаем компонент рендерера для мигания
        projectileRenderer = GetComponent<Renderer>();
        if (projectileRenderer == null)
        {
            // Пытаемся найти рендерер в дочерних объектах
            projectileRenderer = GetComponentInChildren<Renderer>();
        }
        
        // Инициализируем материалы для мигания
        if (projectileRenderer != null)
        {
            originalMaterial = projectileRenderer.material;
            originalColor = originalMaterial.color;
            
            // Создаем копию материала для мигания
            blinkMaterial = new Material(originalMaterial);
            projectileRenderer.material = blinkMaterial;
            
            // Инициализируем кривую мигания если она не задана
            if (blinkCurve == null || blinkCurve.keys.Length == 0)
            {
                blinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            }
        }
        
        // Запускаем корутину времени жизни
        if (destroyOnLifetime && maxLifetime > 0)
        {
            lifetimeCoroutine = StartCoroutine(LifetimeCoroutine());
        }
    }

    protected virtual void OnCollisionEnter(Collision c)
    {
        // Проверяем, столкнулись ли с другим снарядом
        Projectile otherProjectile = c.gameObject.GetComponent<Projectile>();
        if (otherProjectile != null)
        {
            return; // Игнорируем столкновения между снарядами
        }
        
        // Проверяем, столкнулись ли с VoxelChunk16
        VoxelChunk16 voxelChunk = c.gameObject.GetComponent<VoxelChunk16>();
        
        if (voxelChunk != null && !isTimerActive)
        {
            // Запускаем таймер взрыва (физику не отключаем - пусть катается!)
            StartExplosionTimer(c.GetContact(0).point);
        }
        else if (destroyOnHit && !isTimerActive)
        {
            // Обычное поведение для других объектов
        DoDamage(c.GetContact(0).point);
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Проверяем, попали ли в другой снаряд
        Projectile otherProjectile = other.GetComponent<Projectile>();
        if (otherProjectile != null)
        {
            return; // Игнорируем триггеры между снарядами
        }
        
        // Проверяем, попали ли в VoxelChunk16
        VoxelChunk16 voxelChunk = other.GetComponent<VoxelChunk16>();
        
        if (voxelChunk != null && !isTimerActive)
        {
            // Запускаем таймер взрыва (физику не отключаем - пусть катается!)
            StartExplosionTimer(transform.position);
        }
        else if (destroyOnHit && !isTimerActive)
        {
            // Обычное поведение для других объектов
            DoDamage(transform.position);
        }
    }

    protected virtual void StartExplosionTimer(Vector3 hitPoint)
    {
        if (isTimerActive) return;
        
        isTimerActive = true;
        
        // Запускаем мигание
        if (projectileRenderer != null)
        {
            blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }
        
        // Запускаем таймер взрыва
        timerCoroutine = StartCoroutine(ExplosionTimerCoroutine());
    }

    protected virtual IEnumerator ExplosionTimerCoroutine()
    {
        // Ждем указанное время
        yield return new WaitForSeconds(explosionDelay);
        
        // Взрываем снаряд в текущей позиции (может кататься!)
        DoDamage(transform.position);
    }

    protected virtual IEnumerator BlinkCoroutine()
    {
        while (isTimerActive)
        {
            // Плавное изменение цвета с исходного на белый
            float time = 0f;
            while (time < blinkInterval && isTimerActive)
            {
                if (blinkMaterial != null)
                {
                    float curveValue = blinkCurve.Evaluate(time / blinkInterval);
                    Color currentColor = Color.Lerp(originalColor, Color.white, curveValue * whiteIntensity);
                    blinkMaterial.color = currentColor;
                }
                
                time += Time.deltaTime;
                yield return null;
            }
            
            // Плавное изменение цвета с белого обратно на исходный
            time = 0f;
            while (time < blinkInterval && isTimerActive)
            {
                if (blinkMaterial != null)
                {
                    float curveValue = blinkCurve.Evaluate(1f - (time / blinkInterval));
                    Color currentColor = Color.Lerp(originalColor, Color.white, curveValue * whiteIntensity);
                    blinkMaterial.color = currentColor;
                }
                
                time += Time.deltaTime;
                yield return null;
            }
        }
        
        // Возвращаем исходный цвет
        if (blinkMaterial != null)
        {
            blinkMaterial.color = originalColor;
        }
    }

    protected virtual void DoDamage(Vector3 hitPoint)
    {
        if (hasExploded) return; // Предотвращаем повторные взрывы
        
        hasExploded = true;
        
        if (VoxelWorld.Instance != null)
        {
            VoxelWorld.Instance.DamageSphere(hitPoint, radius, maxDamage);
        }

        // Запускаем цепную реакцию
        StartChainReaction();

        if (destroyOnHit) 
        {
            StartCoroutine(DestroyAfterChainReaction());
        }
    }

    protected virtual void StartChainReaction()
    {
        // Ищем другие снаряды в радиусе
        Collider[] nearbyProjectiles = Physics.OverlapSphere(transform.position, chainReactionRadius, projectileLayerMask);
        
        foreach (Collider col in nearbyProjectiles)
        {
            Projectile otherProjectile = col.GetComponent<Projectile>();
            if (otherProjectile != null && otherProjectile != this && !otherProjectile.hasExploded)
            {
                // Запускаем взрыв другого снаряда с небольшой задержкой
                StartCoroutine(TriggerChainExplosion(otherProjectile));
            }
        }
    }

    IEnumerator TriggerChainExplosion(Projectile targetProjectile)
    {
        // Небольшая задержка для эффекта цепной реакции
        yield return new WaitForSeconds(chainReactionDelay);
        
        if (targetProjectile == null || targetProjectile.hasExploded)
        {
            yield break;
        }
        
        // Принудительно взрываем другой снаряд
        targetProjectile.DoDamage(targetProjectile.transform.position);
    }

    protected virtual IEnumerator DestroyAfterChainReaction()
    {
        // Ждем время цепной реакции + небольшая задержка
        float waitTime = chainReactionDelay + 0.1f;
        
        yield return new WaitForSeconds(waitTime);

        Destroy(gameObject);
    }

    protected virtual IEnumerator LifetimeCoroutine()
    {
        // Ждем истечения времени жизни
        yield return new WaitForSeconds(maxLifetime);
        
        // Если снаряд еще не взорвался, взрываем его
        if (!hasExploded)
        {
            DoDamage(transform.position);
        }
    }

    void OnDestroy()
    {
        // Останавливаем все корутины при уничтожении
        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);
        
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
            
        if (lifetimeCoroutine != null)
            StopCoroutine(lifetimeCoroutine);
        
        // Возвращаем исходный материал и очищаем память
        if (projectileRenderer != null && originalMaterial != null)
        {
            projectileRenderer.material = originalMaterial;
        }
        
        if (blinkMaterial != null)
        {
            DestroyImmediate(blinkMaterial);
        }
    }

    // Публичные методы для времени жизни
    public float GetRemainingLifetime()
    {
        if (maxLifetime <= 0) return float.MaxValue;
        return Mathf.Max(0, maxLifetime - (Time.time - spawnTime));
    }
    
    public float GetLifetimeProgress()
    {
        if (maxLifetime <= 0) return 0f;
        return Mathf.Clamp01((Time.time - spawnTime) / maxLifetime);
    }

    void OnDrawGizmosSelected()
    {
        // Визуализация радиуса взрыва
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // Визуализация радиуса цепной реакции
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chainReactionRadius);
    }
}