using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Скрипт для кнопки регенерации мира в UI
/// </summary>
[RequireComponent(typeof(Button))]
public class RegenerateWorldButton : MonoBehaviour
{
    [Header("Confirmation")]
    [Tooltip("Требовать подтверждение перед регенерацией")]
    public bool requireConfirmation = true;
    
    [Tooltip("Текст подтверждения")]
    public string confirmationMessage = "Вы уверены, что хотите перегенерировать мир? Весь прогресс будет потерян!";
    
    private Button button;
    
    void Awake()
    {
        button = GetComponent<Button>();
        
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogError("RegenerateWorldButton: Button component not found!");
        }
    }
    
    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
    
    /// <summary>
    /// Обработчик нажатия на кнопку
    /// </summary>
    private void OnButtonClick()
    {
        if (requireConfirmation)
        {
            // В реальном проекте здесь должен быть UI диалог подтверждения
            // Для простоты используем Debug.Log
            Debug.Log($"RegenerateWorldButton: {confirmationMessage}");
            
            // Временно: сразу вызываем регенерацию
            // TODO: Добавить UI диалог подтверждения
            RegenerateWorld();
        }
        else
        {
            RegenerateWorld();
        }
    }
    
    /// <summary>
    /// Запустить регенерацию мира
    /// </summary>
    public void RegenerateWorld()
    {
        Debug.Log("RegenerateWorldButton: Запуск регенерации мира через GlobalEvents...");
        
        // Вызываем глобальное событие
        GlobalEvents.RegenerateWorld.Invoke();
    }
    
    /// <summary>
    /// Публичный метод для вызова из UI (для совместимости с UnityEvent)
    /// </summary>
    public void OnRegenerateWorldButtonPressed()
    {
        OnButtonClick();
    }
}

