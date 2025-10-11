using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class KinematicProjectile : Projectile
{
    [Header("Kinematic Settings")]
    public float speed = 20f;                       // Скорость полета снаряда
    public bool ignoreGravity = true;               // Игнорировать гравитацию
    public float directHitRadius = 1.0f;            // Радиус прямого попадания
    
    private Rigidbody rb;
    private Vector3 velocity;
    private bool hasHit = false;                    // Флаг попадания
    
    protected override void Start()
    {
        // Получаем Rigidbody
        rb = GetComponent<Rigidbody>();
        
        // Принудительно устанавливаем минимальный радиус для видимости
        if (directHitRadius < 0.5f)
        {
            directHitRadius = 1.0f;
        }
        
        // Устанавливаем время жизни для кинематического снаряда
        this.maxLifetime = 8f;
        
        // Настраиваем физику для кинематического полета
        SetupKinematicPhysics();
        
        // Запускаем полет
        StartFlight();
        
        // Вызываем базовый Start для инициализации материалов
        base.Start();
    }
    
    void SetupKinematicPhysics()
    {
        if (rb != null)
        {
            // Отключаем гравитацию если нужно
            if (ignoreGravity)
            {
                rb.useGravity = false;
            }
            
            // Устанавливаем режим Rigidbody
            rb.isKinematic = false; // Оставляем физику для коллизий
            rb.drag = 0f;           // Убираем сопротивление воздуха
            rb.angularDrag = 0f;    // Убираем сопротивление вращению
        }
    }
    
    void StartFlight()
    {
        if (rb != null)
        {
            // Устанавливаем скорость в направлении forward
            velocity = transform.forward * speed;
            rb.velocity = velocity;
        }
    }
    
    void Update()
    {
        // Если снаряд уже попал, не обновляем полет
        if (hasHit) return;
        
        // Поддерживаем постоянную скорость
        if (rb != null && !rb.useGravity)
        {
            // Корректируем скорость чтобы она была постоянной
            Vector3 currentVelocity = rb.velocity;
            float currentSpeed = currentVelocity.magnitude;
            
            if (Mathf.Abs(currentSpeed - speed) > 0.1f)
            {
                rb.velocity = velocity.normalized * speed;
            }
        }
    }
    
    protected override void OnCollisionEnter(Collision c)
    {
        if (hasHit) return;
        
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
            // Обрабатываем попадание в VoxelChunk16
            HandleHit(c.GetContact(0).point);
        }
        else if (destroyOnHit)
        {
            // Обрабатываем попадание в другие объекты (если включено)
            HandleHit(c.GetContact(0).point);
        }
    }
    
    protected override void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        
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
            // Обрабатываем попадание в VoxelChunk16
            HandleHit(transform.position);
        }
        else if (destroyOnHit)
        {
            // Обрабатываем попадание в другие объекты (если включено)
            HandleHit(transform.position);
        }
    }
    
    void HandleHit(Vector3 hitPoint)
    {
        hasHit = true;
        
        // Останавливаем движение
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        // Взрываем снаряд сразу в точке попадания
        DoDirectDamage(hitPoint);
    }
    
    void DoDirectDamage(Vector3 hitPoint)
    {
        // Вызываем базовый метод с минимальным радиусом
        float originalRadius = radius;
        radius = directHitRadius;
        
        DoDamage(hitPoint);
        
        // Восстанавливаем оригинальный радиус
        radius = originalRadius;
    }
    
    // Переопределяем базовые методы чтобы отключить ненужную логику
    
    protected override void StartExplosionTimer(Vector3 hitPoint)
    {
        // Кинематический снаряд взрывается сразу, без таймера
        HandleHit(hitPoint);
    }
    
    // Отключаем цепную реакцию для кинематических снарядов
    protected override void StartChainReaction()
    {
        // Кинематические снаряды не создают цепных реакций
        // Просто уничтожаем снаряд
        StartCoroutine(DestroyAfterChainReaction());
    }
    
    // Переопределяем метод уничтожения
    protected override IEnumerator DestroyAfterChainReaction()
    {
        // Кинематический снаряд уничтожается сразу после взрыва
        yield return new WaitForSeconds(0.1f); // Небольшая задержка для эффектов
        Destroy(gameObject);
    }
    
    // Отключаем мигание для кинематических снарядов
    protected override IEnumerator BlinkCoroutine()
    {
        yield break; // Не мигаем
    }
    
    protected override IEnumerator ExplosionTimerCoroutine()
    {
        yield break; // Нет таймера взрыва
    }

    protected override void DoForce()
    {
        //base.DoForce();
    }

    void OnDrawGizmosSelected()
    {
        // Визуализация радиуса прямого попадания
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, directHitRadius);
        
        // Визуализация направления полета
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}
