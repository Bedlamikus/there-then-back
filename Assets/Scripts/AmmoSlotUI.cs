using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI элемент для отображения одного патрона в магазине
/// </summary>
public class AmmoSlotUI : MonoBehaviour
{
    [Header("Components")]
    public Image ammoImage;  // Изображение патрона
    
    [Header("Visual States")]
    public Color loadedColor = Color.white;      // Цвет заряженного патрона
    public Color emptyColor = Color.gray;        // Цвет пустого слота
    public Color reloadingColor = Color.yellow;  // Цвет во время перезарядки
    
    [Header("Animation")]
    public bool useScaleAnimation = true;
    public float scaleAnimationSpeed = 5f;
    public float targetScale = 1f;
    
    private bool isLoaded = false;
    private bool isReloading = false;
    private float currentScale = 0f;
    
    void Start()
    {
        if (ammoImage == null)
        {
            ammoImage = GetComponent<Image>();
        }
        
        currentScale = isLoaded ? targetScale : 0f;
        UpdateVisual();
    }
    
    void Update()
    {
        // Плавная анимация масштаба
        if (useScaleAnimation)
        {
            float desiredScale = isLoaded ? targetScale : 0.5f;
            currentScale = Mathf.Lerp(currentScale, desiredScale, scaleAnimationSpeed * Time.deltaTime);
            
            if (ammoImage != null)
            {
                ammoImage.transform.localScale = Vector3.one * currentScale;
            }
        }
    }
    
    /// <summary>
    /// Устанавливает состояние патрона
    /// </summary>
    public void SetState(bool loaded, bool reloading = false)
    {
        isLoaded = loaded;
        isReloading = reloading;
        UpdateVisual();
    }
    
    /// <summary>
    /// Обновляет визуальное отображение
    /// </summary>
    void UpdateVisual()
    {
        if (ammoImage == null) return;
        
        if (isReloading)
        {
            ammoImage.color = reloadingColor;
        }
        else if (isLoaded)
        {
            ammoImage.color = loadedColor;
        }
        else
        {
            ammoImage.color = emptyColor;
        }
    }
    
    /// <summary>
    /// Устанавливает спрайт патрона
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        if (ammoImage != null)
        {
            ammoImage.sprite = sprite;
        }
    }
}

