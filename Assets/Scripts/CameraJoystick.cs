using UnityEngine;
using UnityEngine.EventSystems;

public class CameraJoystick : FloatingJoystick
{
    [Header("Camera Joystick Settings")]
    public bool alwaysVisible = true;               // Всегда видимый джойстик
    public float fadeInSpeed = 2f;                  // Скорость появления
    public float fadeOutSpeed = 2f;                 // Скорость исчезновения
    
    private CanvasGroup canvasGroup;                // Для управления прозрачностью
    private bool isDragging = false;               // Флаг перетаскивания
    
    protected override void Start()
    {
        base.Start();
        
        // Получаем или создаем CanvasGroup для управления прозрачностью
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Устанавливаем начальную видимость
        if (alwaysVisible)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
    
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        
        if (!alwaysVisible)
        {
            isDragging = true;
            FadeIn();
        }
    }
    
    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        
        if (!alwaysVisible)
        {
            isDragging = false;
            FadeOut();
        }
    }
    
    void Update()
    {
        if (!alwaysVisible)
        {
            // Если джойстик не используется, постепенно скрываем его
            if (!isDragging && Direction.magnitude < 0.1f)
            {
                FadeOut();
            }
        }
    }
    
    void FadeIn()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, fadeInSpeed * Time.deltaTime);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
    
    void FadeOut()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, fadeOutSpeed * Time.deltaTime);
            
            // Полностью скрываем когда альфа близка к нулю
            if (canvasGroup.alpha < 0.1f)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
    
    // Публичные методы для внешнего управления
    public void SetAlwaysVisible(bool visible)
    {
        alwaysVisible = visible;
        
        if (alwaysVisible)
        {
            FadeIn();
        }
        else
        {
            FadeOut();
        }
    }
    
    public void ForceShow()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
    
    public void ForceHide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
