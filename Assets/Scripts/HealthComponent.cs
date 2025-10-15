using UnityEngine;
using System;

/// <summary>
/// Компонент здоровья для игрока и врагов
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("Damage Settings")]
    [SerializeField] private bool invulnerable = false;          // Неуязвимость
    [SerializeField] private float invulnerabilityTime = 0.5f;   // Время неуязвимости после получения урона
    private float lastDamageTime = -999f;                        // Время последнего урона
    
    [Header("Death Settings")]
    [SerializeField] private bool destroyOnDeath = false;        // Уничтожать объект при смерти (false для разлета частей)
    [SerializeField] private float destroyDelay = 5f;            // Задержка перед уничтожением (время жизни трупа)
    
    // События
    public event Action<float, float> OnHealthChanged;           // (currentHealth, maxHealth)
    public event Action<float> OnDamageTaken;                    // (damage)
    public event Action OnDeath;                                  // Смерть
    
    // Свойства
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsAlive => currentHealth > 0;
    public bool IsDead => currentHealth <= 0;
    public bool IsInvulnerable => invulnerable || (Time.time - lastDamageTime < invulnerabilityTime);
    
    void Awake()
    {
        // Инициализируем здоровье
        currentHealth = maxHealth;
    }
    
    void Start()
    {
        // Уведомляем о текущем здоровье
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    /// <summary>
    /// Нанести урон
    /// </summary>
    public void TakeDamage(float damage, GameObject damageSource = null)
    {
        // Проверка неуязвимости
        if (IsInvulnerable || IsDead)
            return;
        
        // Применяем урон
        float actualDamage = Mathf.Min(damage, currentHealth);
        currentHealth = Mathf.Max(0, currentHealth - damage);
        lastDamageTime = Time.time;
        
        // Уведомляем о получении урона
        OnDamageTaken?.Invoke(actualDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        // Глобальное событие урона
        GlobalEvents.EntityDamaged?.Invoke(gameObject, actualDamage, damageSource);
        
        Debug.Log($"[Health] {gameObject.name} получил {actualDamage} урона. Осталось здоровья: {currentHealth}/{maxHealth}");
        
        // Проверяем смерть
        if (currentHealth <= 0)
        {
            Die(damageSource);
        }
    }
    
    /// <summary>
    /// Восстановить здоровье
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead)
            return;
        
        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        
        if (currentHealth != oldHealth)
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Debug.Log($"[Health] {gameObject.name} восстановил {currentHealth - oldHealth} здоровья. Здоровье: {currentHealth}/{maxHealth}");
        }
    }
    
    /// <summary>
    /// Установить максимальное здоровье
    /// </summary>
    public void SetMaxHealth(float newMaxHealth, bool healToFull = false)
    {
        maxHealth = newMaxHealth;
        
        if (healToFull)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    /// <summary>
    /// Восстановить здоровье до максимума
    /// </summary>
    public void HealToFull()
    {
        Heal(maxHealth);
    }
    
    /// <summary>
    /// Мгновенная смерть
    /// </summary>
    public void Kill(GameObject killer = null)
    {
        if (IsDead)
            return;
        
        currentHealth = 0;
        Die(killer);
    }
    
    /// <summary>
    /// Обработка смерти
    /// </summary>
    private void Die(GameObject killer)
    {
        Debug.Log($"[Health] {gameObject.name} погиб!");
        
        // Уведомляем о смерти
        OnDeath?.Invoke();
        
        // Глобальное событие смерти
        GlobalEvents.EntityDied?.Invoke(gameObject, killer);
        
        // Уничтожаем объект если нужно
        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
    
    /// <summary>
    /// Установить неуязвимость
    /// </summary>
    public void SetInvulnerable(bool value)
    {
        invulnerable = value;
    }
    
    /// <summary>
    /// Временная неуязвимость
    /// </summary>
    public void SetTemporaryInvulnerability(float duration)
    {
        lastDamageTime = Time.time;
        invulnerabilityTime = duration;
    }
}

