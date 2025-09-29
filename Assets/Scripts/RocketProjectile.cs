using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class RocketProjectile : Projectile
{
    [Header("Rocket Flight Settings")]
    public float speed = 25f;                       // Скорость полета ракеты
    public bool ignoreGravity = false;              // Игнорировать гравитацию
    public float acceleration = 5f;                 // Ускорение ракеты (м/с²)
    public float maxSpeed = 35f;                    // Максимальная скорость
    
    [Header("Deviation Settings")]
    public float deviationStrength = 0.5f;          // Сила отклонений (0-2)
    public float deviationFrequency = 2f;           // Частота отклонений (Гц)
    public float deviationSmoothing = 5f;           // Сглаживание отклонений
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 180f;              // Скорость вращения вокруг оси (град/сек)
    public bool randomizeRotation = true;           // Случайное направление вращения
    
    [Header("Visual Effects")]
    public GameObject trailEffect;                  // Эффект следа ракеты
    public GameObject engineEffect;                 // Эффект двигателя
    public Light rocketLight;                       // Свет от ракеты
    
    [Header("Explosion Settings")]
    public float explosionRadius = 4f;              // Радиус взрыва ракеты
    public float impactForce = 50f;                 // Сила удара при взрыве
    
    private Rigidbody rb;
    private Vector3 initialDirection;               // Начальное направление полета
    private Vector3 targetDirection;                // Целевое направление полета
    private Vector3 currentDirection;               // Текущее направление полета
    private float deviationTimer;                   // Таймер для отклонений
    private float currentSpeed;                     // Текущая скорость
    
    // Отклонения
    private Vector3 deviationOffset = Vector3.zero; // Текущее отклонение
    private Vector3 targetDeviation = Vector3.zero; // Целевое отклонение
    
    // Вращение
    private Vector3 rotationAxis = Vector3.up;      // Ось вращения
    private float rotationAngle = 0f;               // Угол вращения
    
    protected override void Start()
    {
        // Получаем Rigidbody
        rb = GetComponent<Rigidbody>();
        
        // Принудительно устанавливаем минимальные значения для видимости
        if (explosionRadius < 2f)
        {
            explosionRadius = 3f;
        }
        if (maxDamage <= 0)
        {
            maxDamage = 15f;
        }
        
        // Устанавливаем время жизни для ракеты
        this.maxLifetime = 15f;
        
        // Запоминаем начальное направление
        initialDirection = transform.forward;
        currentDirection = initialDirection;
        targetDirection = initialDirection;
        
        // Настраиваем физику
        SetupPhysics();
        
        // Настраиваем визуальные эффекты
        SetupVisualEffects();
        
        // Запускаем полет
        StartFlight();
        
        // Вызываем базовый Start
        base.Start();
    }
    
    void SetupPhysics()
    {
        if (rb != null)
        {
            // Отключаем гравитацию если нужно
            if (ignoreGravity)
            {
                rb.useGravity = false;
            }
            
            rb.isKinematic = false;
            rb.drag = 0.1f;                         // Небольшое сопротивление воздуха
            rb.angularDrag = 0.5f;                  // Сопротивление вращению
            
            // Устанавливаем начальную скорость
            currentSpeed = speed;
        }
    }
    
    void SetupVisualEffects()
    {
        // Активируем эффекты
        if (trailEffect != null)
            trailEffect.SetActive(true);
            
        if (engineEffect != null)
            engineEffect.SetActive(true);
            
        if (rocketLight != null)
            rocketLight.enabled = true;
    }
    
    void StartFlight()
    {
        if (rb != null)
        {
            // Устанавливаем начальную скорость
            rb.velocity = currentDirection * currentSpeed;
            
            // Случайное направление вращения
            if (randomizeRotation)
            {
                rotationAxis = Random.Range(0, 2) == 0 ? Vector3.up : Vector3.down;
                rotationSpeed *= Random.Range(0.8f, 1.2f); // Небольшая вариация скорости
            }
        }
    }
    
    void Update()
    {
        if (hasExploded) return;
        
        // Обновляем отклонения
        UpdateDeviation();
        
        // Обновляем направление полета
        UpdateFlightDirection();
        
        // Обновляем скорость (ускорение)
        UpdateSpeed();
        
        // Обновляем вращение
        UpdateRotation();
        
        // Применяем движение
        ApplyMovement();
    }
    
    void UpdateDeviation()
    {
        // Генерируем новые отклонения
        if (Time.time - deviationTimer >= 1f / deviationFrequency)
        {
            // Случайные отклонения в перпендикулярных направлениях
            Vector3 right = Vector3.Cross(currentDirection, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(currentDirection, right).normalized;
            
            targetDeviation = (right * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * deviationStrength;
            deviationTimer = Time.time;
        }
        
        // Плавно переходим к целевому отклонению
        deviationOffset = Vector3.Lerp(deviationOffset, targetDeviation, deviationSmoothing * Time.deltaTime);
    }
    
    void UpdateFlightDirection()
    {
        // Применяем отклонения к направлению полета
        targetDirection = (initialDirection + deviationOffset).normalized;
        
        // Плавно поворачиваем в целевом направлении
        currentDirection = Vector3.Lerp(currentDirection, targetDirection, 2f * Time.deltaTime);
    }
    
    void UpdateSpeed()
    {
        // Ускоряемся до максимальной скорости
        if (currentSpeed < maxSpeed)
        {
            currentSpeed += acceleration * Time.deltaTime;
            currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
        }
    }
    
    void UpdateRotation()
    {
        // Вращаемся вокруг оси движения
        rotationAngle += rotationSpeed * Time.deltaTime;
        
        // Нормализуем угол
        if (rotationAngle >= 360f)
            rotationAngle -= 360f;
    }
    
    void ApplyMovement()
    {
        if (rb != null)
        {
            // Устанавливаем скорость в текущем направлении
            rb.velocity = currentDirection * currentSpeed;
            
            // Поворачиваем ракету в направлении движения
            if (currentDirection.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(currentDirection);
                
                // Применяем вращение вокруг оси
                transform.Rotate(rotationAxis, rotationAngle, Space.Self);
            }
        }
    }
    
    protected override void OnCollisionEnter(Collision c)
    {
        if (hasExploded) return;
        
        // Проверяем, столкнулись ли с другим снарядом
        Projectile otherProjectile = c.gameObject.GetComponent<Projectile>();
        if (otherProjectile != null)
        {
            return; // Игнорируем столкновения между снарядами
        }
        
        // Проверяем, столкнулись ли с VoxelChunk16
        VoxelChunk16 voxelChunk = c.gameObject.GetComponent<VoxelChunk16>();
        if (voxelChunk != null)
        {
            // Взрываемся при попадании в VoxelChunk16
            ExplodeAtContact(c.GetContact(0).point, c.relativeVelocity);
        }
        else if (destroyOnHit)
        {
            // Взрываемся при попадании в другие объекты (если включено)
            ExplodeAtContact(c.GetContact(0).point, c.relativeVelocity);
        }
    }
    
    protected override void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        
        // Проверяем, попали ли в другой снаряд
        Projectile otherProjectile = other.GetComponent<Projectile>();
        if (otherProjectile != null)
        {
            return; // Игнорируем триггеры между снарядами
        }
        
        // Проверяем, попали ли в VoxelChunk16
        VoxelChunk16 voxelChunk = other.GetComponent<VoxelChunk16>();
        if (voxelChunk != null)
        {
            // Взрываемся при попадании в VoxelChunk16
            ExplodeAtContact(transform.position, rb.velocity);
        }
        else if (destroyOnHit)
        {
            // Взрываемся при попадании в другие объекты (если включено)
            ExplodeAtContact(transform.position, rb.velocity);
        }
    }
    
    void ExplodeAtContact(Vector3 contactPoint, Vector3 impactVelocity)
    {
        if (hasExploded) return; // Предотвращаем повторные взрывы
        
        
        // Останавливаем движение
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        // Отключаем визуальные эффекты
        DisableVisualEffects();
        
        // Взрываемся (DoDamage сам установит hasExploded = true)
        DoRocketDamage(contactPoint, impactVelocity);
    }
    
    void DoRocketDamage(Vector3 hitPoint, Vector3 impactVelocity)
    {
        // Временно меняем радиус взрыва
        float originalRadius = radius;
        radius = explosionRadius;
        
        // Вызываем базовый метод взрыва
        DoDamage(hitPoint);
        
        // Восстанавливаем оригинальный радиус
        radius = originalRadius;
        
        // Применяем силу удара к объектам в радиусе
        ApplyImpactForce(hitPoint, impactVelocity);
    }
    
    void ApplyImpactForce(Vector3 explosionPoint, Vector3 impactVelocity)
    {
        // Ищем все объекты в радиусе взрыва
        Collider[] objectsInRange = Physics.OverlapSphere(explosionPoint, explosionRadius);
        
        foreach (Collider col in objectsInRange)
        {
            Rigidbody objRb = col.GetComponent<Rigidbody>();
            if (objRb != null)
            {
                // Вычисляем направление от взрыва к объекту
                Vector3 direction = (col.transform.position - explosionPoint).normalized;
                
                // Вычисляем расстояние для затухания силы
                float distance = Vector3.Distance(explosionPoint, col.transform.position);
                float forceMultiplier = 1f - (distance / explosionRadius);
                
                // Применяем силу
                Vector3 force = direction * impactForce * forceMultiplier;
                objRb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
    
    void DisableVisualEffects()
    {
        if (trailEffect != null)
            trailEffect.SetActive(false);
            
        if (engineEffect != null)
            engineEffect.SetActive(false);
            
        if (rocketLight != null)
            rocketLight.enabled = false;
    }
    
    // Переопределяем методы чтобы отключить ненужную логику
    protected override void StartExplosionTimer(Vector3 hitPoint)
    {
        // Ракета взрывается сразу
        ExplodeAtContact(hitPoint, rb.velocity);
    }
    
    protected override IEnumerator ExplosionTimerCoroutine()
    {
        yield break; // Нет таймера взрыва
    }
    
    protected override IEnumerator BlinkCoroutine()
    {
        yield break; // Не мигаем
    }
    
    protected override void StartChainReaction()
    {
        // Ракеты не создают цепных реакций
        StartCoroutine(DestroyAfterChainReaction());
    }
    
    protected override IEnumerator DestroyAfterChainReaction()
    {
        // Ракета уничтожается сразу после взрыва
        yield return new WaitForSeconds(0.1f);
        Destroy(gameObject);
    }
    
    void OnDrawGizmosSelected()
    {
        // Визуализация радиуса взрыва
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
        // Визуализация направления полета
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, currentDirection * 3f);
        
        // Визуализация отклонений
        if (deviationOffset.magnitude > 0.1f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, deviationOffset * 2f);
        }
        
        // Визуализация оси вращения
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, rotationAxis * 2f);
    }
}
