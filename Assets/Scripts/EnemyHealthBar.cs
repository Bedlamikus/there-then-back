using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения полоски здоровья врага
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("Image компонент полоски здоровья")]
    public Image healthBarImage;
    
    [Tooltip("Canvas для UI элементов")]
    public Canvas healthCanvas;
    
    [Header("Health Bar Settings")]
    [Tooltip("Скорость анимации изменения полоски")]
    public float animationSpeed = 5f;
    
    [Tooltip("Показывать полоску только при получении урона")]
    public bool showOnDamageOnly = true;
    
    [Tooltip("Время показа полоски после урона (секунды)")]
    public float showDuration = 3f;
    
    [Header("Visual Settings")]
    [Tooltip("Цвет полной полоски здоровья")]
    public Color fullHealthColor = Color.green;
    
    [Tooltip("Цвет пустой полоски здоровья")]
    public Color emptyHealthColor = Color.red;
    
    // Приватные переменные
    private HealthComponent healthComponent;
    private float targetHealthRatio = 1f;
    private float currentHealthRatio = 1f;
    private bool isVisible = false;
    private float lastDamageTime = -999f;
    
    void Start()
    {
        // Получаем HealthComponent от родительского объекта
        healthComponent = GetComponentInParent<HealthComponent>();
        
        if (healthComponent == null)
        {
            Debug.LogError($"[EnemyHealthBar] HealthComponent not found on parent of {gameObject.name}");
            return;
        }
        
        // Подписываемся на события здоровья
        healthComponent.OnHealthChanged += OnHealthChanged;
        healthComponent.OnDamageTaken += OnDamageTaken;
        
        // Инициализируем UI
        InitializeHealthBar();
        
        Debug.Log($"[EnemyHealthBar] Initialized for {gameObject.name}");
    }
    
    /// <summary>
    /// Инициализирует полоску здоровья
    /// </summary>
    void InitializeHealthBar()
    {
        if (healthBarImage == null)
        {
            Debug.LogError($"[EnemyHealthBar] HealthBarImage not assigned on {gameObject.name}");
            return;
        }
        
        // Устанавливаем начальное состояние
        currentHealthRatio = healthComponent.CurrentHealth / healthComponent.MaxHealth;
        targetHealthRatio = currentHealthRatio;
        
        // Обновляем визуал
        UpdateHealthBarVisual();
        
        // Скрываем полоску если нужно
        if (showOnDamageOnly)
        {
            SetHealthBarVisible(false);
        }
    }
    
    /// <summary>
    /// Обработчик изменения здоровья
    /// </summary>
    void OnHealthChanged(float currentHealth, float maxHealth)
    {
        targetHealthRatio = currentHealth / maxHealth;
        
        Debug.Log($"[EnemyHealthBar] Health changed: {currentHealth}/{maxHealth} ({targetHealthRatio:P1})");
    }
    
    /// <summary>
    /// Обработчик получения урона
    /// </summary>
    void OnDamageTaken(float damage)
    {
        lastDamageTime = Time.time;
        
        // Показываем полоску при получении урона
        if (showOnDamageOnly)
        {
            SetHealthBarVisible(true);
        }
        
        Debug.Log($"[EnemyHealthBar] Damage taken: {damage}");
    }
    
    /// <summary>
    /// Обновляет визуал полоски здоровья
    /// </summary>
    void UpdateHealthBarVisual()
    {
        if (healthBarImage == null) return;
        
        // Обновляем размер полоски
        healthBarImage.fillAmount = currentHealthRatio;
        
        // Обновляем цвет (интерполяция между цветами)
        healthBarImage.color = Color.Lerp(emptyHealthColor, fullHealthColor, currentHealthRatio);
    }
    
    /// <summary>
    /// Устанавливает видимость полоски здоровья
    /// </summary>
    void SetHealthBarVisible(bool visible)
    {
        isVisible = visible;
        
        if (healthCanvas != null)
        {
            healthCanvas.gameObject.SetActive(visible);
        }
        else if (healthBarImage != null)
        {
            healthBarImage.gameObject.SetActive(visible);
        }
    }
    
    void Update()
    {
        // Плавная анимация полоски здоровья
        if (Mathf.Abs(currentHealthRatio - targetHealthRatio) > 0.001f)
        {
            currentHealthRatio = Mathf.Lerp(currentHealthRatio, targetHealthRatio, animationSpeed * Time.deltaTime);
            UpdateHealthBarVisual();
        }
        
        // Скрываем полоску через время после урона
        if (showOnDamageOnly && isVisible && Time.time - lastDamageTime > showDuration)
        {
            SetHealthBarVisible(false);
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        if (healthComponent != null)
        {
            healthComponent.OnHealthChanged -= OnHealthChanged;
            healthComponent.OnDamageTaken -= OnDamageTaken;
        }
    }
}
