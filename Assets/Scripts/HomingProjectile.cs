using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HomingProjectile : Projectile
{
    [Header("Homing Settings")]
    public float speed = 15f;                       // Скорость полета снаряда
    public float turnSpeed = 90f;                   // Скорость поворота к цели (градусы/сек)
    public float maxTurnAngle = 45f;                // Максимальный угол поворота за кадр
    public float homingRadius = 50f;                // Радиус поиска цели
    public LayerMask targetLayerMask = -1;          // Слой целей
    public string targetTag = "Enemy";              // Тег цели
    
    [Header("No Target Behavior")]
    public bool searchContinuously = true;          // Поискать цель постоянно
    public float searchInterval = 0.5f;             // Интервал поиска цели (секунды)
    public bool selfDestructIfNoTarget = false;     // Уничтожаться если нет цели
    public float selfDestructTime = 10f;            // Время до самоуничтожения (секунды)
    
    [Header("Fixed Target (Optional)")]
    public Transform fixedTarget;                   // Фиксированная цель (если назначена, игнорирует поиск)
    
    [Header("Explosion Settings")]
    public float explosionRadius = 3f;              // Радиус взрыва при попадании
    public bool explodeOnProximity = true;          // Взрываться при приближении к цели
    public float proximityDistance = 2f;            // Расстояние для взрыва приближения
    
    private Rigidbody rb;
    private Transform target;                       // Текущая цель
    private Vector3 velocity;
    private bool hasTarget = false;                 // Есть ли цель
    private float lastSearchTime;                   // Время последнего поиска цели
    private float spawnTime;                        // Время создания снаряда
    
    protected override void Start()
    {
        // Получаем Rigidbody
        rb = GetComponent<Rigidbody>();
        
        // Запоминаем время создания
        spawnTime = Time.time;
        lastSearchTime = Time.time;
        
        // Устанавливаем время жизни для самонаводящегося снаряда
        this.maxLifetime = 12f;
        
        // Настраиваем физику
        SetupPhysics();
        
        // Ищем цель (или используем фиксированную)
        if (fixedTarget != null)
        {
            target = fixedTarget;
            hasTarget = true;
        }
        else
        {
            FindTarget();
        }
        
        // Запускаем полет
        StartFlight();
        
        // Вызываем базовый Start
        base.Start();
    }
    
    void SetupPhysics()
    {
        if (rb != null)
        {
            rb.useGravity = false;                  // Отключаем гравитацию
            rb.isKinematic = false;
            rb.drag = 0f;                           // Убираем сопротивление воздуха
            rb.angularDrag = 0f;                    // Убираем сопротивление вращению
        }
    }
    
    void FindTarget()
    {
        // Ищем ближайшую цель в радиусе
        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, homingRadius, targetLayerMask);
        
        float closestDistance = float.MaxValue;
        Transform closestTarget = null;
        
        foreach (Collider col in potentialTargets)
        {
            if (col.CompareTag(targetTag))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = col.transform;
                }
            }
        }
        
        if (closestTarget != null)
        {
            target = closestTarget;
            hasTarget = true;
        }
    }
    
    void StartFlight()
    {
        if (rb != null)
        {
            // Устанавливаем начальную скорость в направлении forward
            velocity = transform.forward * speed;
            rb.velocity = velocity;
        }
    }
    
    void Update()
    {
        if (hasExploded) return;
        
        // Проверяем самоуничтожение если нет цели
        if (selfDestructIfNoTarget && !hasTarget && Time.time - spawnTime >= selfDestructTime)
        {
            ExplodeAtTarget();
            return;
        }
        
        // Если нет цели и нет фиксированной цели, пытаемся найти новую (с интервалом)
        if ((!hasTarget || target == null) && fixedTarget == null && searchContinuously)
        {
            if (Time.time - lastSearchTime >= searchInterval)
            {
                FindTarget();
                lastSearchTime = Time.time;
            }
        }
        
        // Если есть цель, наводимся на неё
        if (hasTarget && target != null)
        {
            HomingToTarget();
            
            // Проверяем взрыв при приближении
            if (explodeOnProximity)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget <= proximityDistance)
                {
                    ExplodeAtTarget();
                    return;
                }
            }
        }
        
        // Поддерживаем скорость (летим прямо если нет цели)
        MaintainSpeed();
    }
    
    void HomingToTarget()
    {
        if (rb == null || target == null) return;
        
        // Вычисляем направление к цели
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        
        // Вычисляем угол между текущим направлением и направлением к цели
        Vector3 currentDirection = rb.velocity.normalized;
        float angleToTarget = Vector3.Angle(currentDirection, directionToTarget);
        
        // Если цель впереди, поворачиваем к ней
        if (angleToTarget > 1f) // Минимальный угол для поворота
        {
            // Ограничиваем скорость поворота
            float maxTurnThisFrame = maxTurnAngle * Time.deltaTime;
            float turnAmount = Mathf.Min(angleToTarget, maxTurnThisFrame);
            
            // Вычисляем новое направление
            Vector3 newDirection = Vector3.RotateTowards(currentDirection, directionToTarget, 
                turnAmount * Mathf.Deg2Rad, 0f);
            
            // Обновляем скорость
            rb.velocity = newDirection * speed;
            
            // Поворачиваем снаряд в направлении полета
            if (rb.velocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(rb.velocity);
            }
        }
    }
    
    void MaintainSpeed()
    {
        if (rb != null)
        {
            // Поддерживаем постоянную скорость
            float currentSpeed = rb.velocity.magnitude;
            if (Mathf.Abs(currentSpeed - speed) > 0.1f)
            {
                rb.velocity = rb.velocity.normalized * speed;
            }
        }
    }
    
    void ExplodeAtTarget()
    {
        if (hasExploded) return;
        
        hasExploded = true;
        
        // Останавливаем движение
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        // Взрываемся в текущей позиции
        DoDamage(transform.position);
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
        
        // Проверяем, попали ли в цель
        if (c.gameObject.CompareTag(targetTag))
        {
            // Попадание в цель - взрываемся
            ExplodeAtTarget();
        }
        else
        {
            // Попадание в препятствие - тоже взрываемся
            ExplodeAtTarget();
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
        
        // Проверяем, попали ли в цель
        if (other.CompareTag(targetTag))
        {
            // Попадание в цель - взрываемся
            ExplodeAtTarget();
        }
        else
        {
            // Попадание в препятствие - тоже взрываемся
            ExplodeAtTarget();
        }
    }
    
    protected override void DoDamage(Vector3 hitPoint)
    {
        // Временно меняем радиус взрыва
        float originalRadius = radius;
        radius = explosionRadius;
        
        // Вызываем базовый метод
        base.DoDamage(hitPoint);
        
        // Восстанавливаем оригинальный радиус
        radius = originalRadius;
    }
    
    // Переопределяем методы чтобы отключить ненужную логику
    protected override void StartExplosionTimer(Vector3 hitPoint)
    {
        // Самонаводящийся снаряд взрывается сразу
        ExplodeAtTarget();
    }
    
    protected override IEnumerator ExplosionTimerCoroutine()
    {
        yield break; // Нет таймера взрыва
    }
    
    protected override IEnumerator BlinkCoroutine()
    {
        yield break; // Не мигаем
    }
    
    // Публичные методы для управления целью
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasTarget = (target != null);
        fixedTarget = newTarget; // Сохраняем как фиксированную цель
    }
    
    public void ClearTarget()
    {
        target = null;
        hasTarget = false;
        fixedTarget = null;
    }
    
    public Transform GetCurrentTarget() => target;
    public bool HasTarget() => hasTarget && target != null;
    
    void OnDrawGizmosSelected()
    {
        // Визуализация радиуса поиска цели
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, homingRadius);
        
        // Визуализация радиуса взрыва
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
        // Визуализация направления полета
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
        
        // Визуализация линии к цели
        if (hasTarget && target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
        
        // Визуализация фиксированной цели
        if (fixedTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(fixedTarget.position, 1f);
        }
    }
}
