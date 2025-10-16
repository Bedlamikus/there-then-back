using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Projectile Type")]
    [Tooltip("Тип снаряда для выбора эффекта взрыва")]
    public ProjectileType projectileType = ProjectileType.Pistol;
    
    [Header("Force Settings")]
    public float force = 35f;
    
    [Header("Aiming Settings")]
    [Tooltip("Оптимальная дистанция для прицельной стрельбы")]
    public float aimDistance = 15f;

    [Header("Explosion Settings")]
    public float radius = 6f;
    public float maxDamage = 10f;
    public bool destroyOnHit = true;

    [Header("Timer Settings")]
    public float explosionDelay = 3f;              // Задержка до взрыва (секунды)
    public float blinkInterval = 0.2f;             // Интервал мигания (секунды)
    public float whiteIntensity = 0.5f;            // Интенсивность белого цвета (0-1)
    public AnimationCurve blinkCurve;              // Кривая мигания для плавности

    [Header("Camera Shake")]
    [Tooltip("Вызывать встряску камеры при взрыве")]
    public bool cameraShake = false;

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
    
    // Защита от мгновенного столкновения с игроком
    private bool canCollideWithPlayer = false;     // Флаг разрешения столкновений с игроком
    private float collisionEnableDelay = 0.1f;     // Задержка включения столкновений (секунды)

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
        
        // Запускаем корутину для разрешения столкновений с игроком через задержку
        StartCoroutine(EnablePlayerCollisionAfterDelay());
        
        // Вызываем событие звука выстрела
        GlobalEvents.WeaponFired?.Invoke(transform.position, GetShootSoundType());
    }
    
    /// <summary>
    /// Получает тип звука выстрела на основе типа снаряда
    /// </summary>
    ProjectileType GetShootSoundType()
    {
        switch (projectileType)
        {
            case ProjectileType.Pistol:
                return ProjectileType.ShootPistol;
            case ProjectileType.Rocket:
                return ProjectileType.ShootRocket;
            case ProjectileType.Dinamit:
                return ProjectileType.ShootDinamit;
            case ProjectileType.Rock:
                return ProjectileType.ShootRock;
            default:
                return ProjectileType.ShootPistol; // По умолчанию
        }
    }
    
    /// <summary>
    /// Получает тип звука взрыва на основе типа снаряда
    /// </summary>
    ProjectileType GetExplosionSoundType()
    {
        switch (projectileType)
        {
            case ProjectileType.Pistol:
                return ProjectileType.ExplosionPistol;
            case ProjectileType.Rocket:
                return ProjectileType.ExplosionRocket;
            case ProjectileType.Dinamit:
                return ProjectileType.ExplosionDinamit;
            case ProjectileType.Rock:
                return ProjectileType.ExplosionRock;
            default:
                return ProjectileType.ExplosionPistol; // По умолчанию
        }
    }
    
    /// <summary>
    /// Включает столкновения с игроком через небольшую задержку
    /// </summary>
    protected virtual IEnumerator EnablePlayerCollisionAfterDelay()
    {
        yield return new WaitForSeconds(collisionEnableDelay);
        canCollideWithPlayer = true;
    }

    protected virtual void OnCollisionEnter(Collision c)
    {
        // Проверяем, столкнулись ли с другим снарядом
        Projectile otherProjectile = c.gameObject.GetComponent<Projectile>();
        if (otherProjectile != null)
        {
            return; // Игнорируем столкновения между снарядами
        }
        
        // Проверяем, столкнулись ли с игроком
        PlayerController player = c.gameObject.GetComponent<PlayerController>();
        if (player != null && !canCollideWithPlayer)
        {
            return; // Игнорируем столкновения с игроком в первые 0.1 секунды
        }
        
        // Проверяем наличие компонента здоровья для прямого попадания
        HealthComponent health = c.gameObject.GetComponent<HealthComponent>();
        if (health != null && canCollideWithPlayer)
        {
            // Наносим урон при прямом попадании
            health.TakeDamage(maxDamage, gameObject);
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
        
        // Проверяем, попали ли в игрока
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && !canCollideWithPlayer)
        {
            return; // Игнорируем триггеры с игроком в первые 0.1 секунды
        }
        
        // Проверяем наличие компонента здоровья для прямого попадания
        HealthComponent health = other.GetComponent<HealthComponent>();
        if (health != null && canCollideWithPlayer)
        {
            // Наносим урон при прямом попадании
            health.TakeDamage(maxDamage, gameObject);
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
        
        // Вызываем событие взрыва для проигрывания партиклов
        GlobalEvents.ProjectileExploded?.Invoke(hitPoint, projectileType);
        
        // Вызываем событие звука взрыва
        GlobalEvents.WeaponFired?.Invoke(hitPoint, GetExplosionSoundType());
        
        // Вызываем событие встряски камеры (если включена)
        if (cameraShake)
        {
            Debug.Log($"[Projectile] Triggering camera shake! Explosion at: {hitPoint}, Type: {projectileType}");
            GlobalEvents.CameraShake?.Invoke(hitPoint);
        }
        else
        {
            Debug.Log($"[Projectile] Camera shake disabled for projectile type: {projectileType}");
        }
        
        // Наносим урон воксельным блокам
        if (VoxelWorld.Instance != null)
        {
            VoxelWorld.Instance.DamageSphere(hitPoint, radius, maxDamage);
        }

        // Наносим урон всем сущностям в радиусе взрыва
        DamageEntitiesInRadius(hitPoint, radius, maxDamage);

        // Запускаем цепную реакцию
        StartChainReaction();

        if (destroyOnHit) 
        {
            StartCoroutine(DestroyAfterChainReaction());
        }
    }
    
    /// <summary>
    /// Наносит урон всем сущностям с компонентом HealthComponent в радиусе взрыва
    /// Урон уменьшается с расстоянием от центра взрыва
    /// </summary>
    protected virtual void DamageEntitiesInRadius(Vector3 center, float damageRadius, float damage)
    {
        // Находим все коллайдеры в радиусе взрыва
        Collider[] colliders = Physics.OverlapSphere(center, damageRadius);
        
        foreach (Collider col in colliders)
        {
            // Проверяем наличие компонента здоровья
            HealthComponent health = col.GetComponent<HealthComponent>();
            if (health == null || health.IsDead)
                continue;
            
            // Игнорируем снаряды
            if (col.GetComponent<Projectile>() != null)
                continue;
            
            // Вычисляем расстояние от центра взрыва
            float distance = Vector3.Distance(center, col.transform.position);
            
            // Вычисляем урон на основе расстояния (линейное затухание)
            float damageMultiplier = 1f - (distance / damageRadius);
            damageMultiplier = Mathf.Clamp01(damageMultiplier);
            
            float actualDamage = damage * damageMultiplier;
            
            // Наносим урон
            if (actualDamage > 0)
            {
                health.TakeDamage(actualDamage, gameObject);
            }
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

    protected virtual void DoForce()
    {
        GetComponent<Rigidbody>().AddForce(transform.forward * force, ForceMode.Impulse);
    }

    protected virtual IEnumerator LifetimeCoroutine()
    {
        yield return null;

        DoForce();

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