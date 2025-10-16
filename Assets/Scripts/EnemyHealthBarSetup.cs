using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вспомогательный компонент для автоматической настройки UI здоровья врага
/// </summary>
public class EnemyHealthBarSetup : MonoBehaviour
{
    [Header("Auto Setup")]
    [Tooltip("Автоматически создать UI при старте")]
    public bool autoSetupOnStart = true;
    
    [Tooltip("Префаб полоски здоровья")]
    public GameObject healthBarPrefab;
    
    [Header("Manual Setup")]
    [Tooltip("Canvas для UI")]
    public Canvas healthCanvas;
    
    [Tooltip("Image полоски здоровья")]
    public Image healthBarImage;
    
    [Tooltip("Image фона полоски")]
    public Image backgroundImage;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupHealthBarUI();
        }
    }
    
    /// <summary>
    /// Настраивает UI полоски здоровья
    /// </summary>
    public void SetupHealthBarUI()
    {
        // Если есть префаб, используем его
        if (healthBarPrefab != null)
        {
            GameObject healthBarObject = Instantiate(healthBarPrefab, transform);
            EnemyHealthBar healthBar = healthBarObject.GetComponent<EnemyHealthBar>();
            
            if (healthBar == null)
            {
                Debug.LogError($"[EnemyHealthBarSetup] HealthBarPrefab doesn't have EnemyHealthBar component!");
                return;
            }
            
            Debug.Log($"[EnemyHealthBarSetup] Created health bar from prefab for {gameObject.name}");
            return;
        }
        
        // Создаем UI вручную
        CreateHealthBarUI();
    }
    
    /// <summary>
    /// Создает UI полоски здоровья вручную
    /// </summary>
    void CreateHealthBarUI()
    {
        // Создаем Canvas
        if (healthCanvas == null)
        {
            GameObject canvasObject = new GameObject("HealthCanvas");
            canvasObject.transform.SetParent(transform);
            canvasObject.transform.localPosition = Vector3.up * 2f; // Над врагом
            
            healthCanvas = canvasObject.AddComponent<Canvas>();
            healthCanvas.renderMode = RenderMode.WorldSpace;
            healthCanvas.worldCamera = Camera.main;
            
            // Добавляем CanvasScaler
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            
            // Добавляем GraphicRaycaster
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        
        // Создаем фоновую полоску
        if (backgroundImage == null)
        {
            GameObject backgroundObject = new GameObject("HealthBackground");
            backgroundObject.transform.SetParent(healthCanvas.transform);
            backgroundObject.transform.localPosition = Vector3.zero;
            
            backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Темно-серый
            
            RectTransform bgRect = backgroundImage.rectTransform;
            bgRect.sizeDelta = new Vector2(2f, 0.2f);
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
        }
        
        // Создаем полоску здоровья
        if (healthBarImage == null)
        {
            GameObject healthObject = new GameObject("HealthBar");
            healthObject.transform.SetParent(healthCanvas.transform);
            healthObject.transform.localPosition = Vector3.zero;
            
            healthBarImage = healthObject.AddComponent<Image>();
            healthBarImage.color = Color.green;
            healthBarImage.type = Image.Type.Filled;
            healthBarImage.fillMethod = Image.FillMethod.Horizontal;
            
            RectTransform healthRect = healthBarImage.rectTransform;
            healthRect.sizeDelta = new Vector2(2f, 0.2f);
            healthRect.anchorMin = new Vector2(0.5f, 0.5f);
            healthRect.anchorMax = new Vector2(0.5f, 0.5f);
            healthRect.pivot = new Vector2(0.5f, 0.5f);
        }
        
        // Добавляем EnemyHealthBar компонент
        EnemyHealthBar healthBar = gameObject.GetComponent<EnemyHealthBar>();
        if (healthBar == null)
        {
            healthBar = gameObject.AddComponent<EnemyHealthBar>();
        }
        
        // Настраиваем ссылки
        healthBar.healthBarImage = healthBarImage;
        healthBar.healthCanvas = healthCanvas;
        
        Debug.Log($"[EnemyHealthBarSetup] Created health bar UI for {gameObject.name}");
    }
}
