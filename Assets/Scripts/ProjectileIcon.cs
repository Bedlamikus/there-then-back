using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ProjectileIcon : MonoBehaviour, IPointerClickHandler
{
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;             // Префаб снаряда
    public string projectileName = "Default";       // Название снаряда
    public Sprite projectileIcon;                   // Иконка для отображения
    
    [Header("UI Elements")]
    public Image iconImage;                         // Изображение иконки
    public Text nameText;                           // Текст названия
    public Button selectButton;                     // Кнопка выбора (опционально)
    
    [Header("Visual Settings")]
    public Color selectedColor = Color.yellow;      // Цвет выбранной иконки
    public Color normalColor = Color.white;         // Цвет обычной иконки
    public float selectedScale = 1.1f;              // Масштаб выбранной иконки
    public float normalScale = 1f;                  // Масштаб обычной иконки
    
    private bool isSelected = false;                // Выбрана ли иконка
    private Vector3 originalScale;                  // Исходный масштаб
    
    void Start()
    {
        // Инициализируем UI элементы
        InitializeIcon();
        
        // Сохраняем исходный масштаб
        originalScale = transform.localScale;
        
        // Настраиваем кнопку если она есть
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(SelectProjectile);
        }
    }
    
    void InitializeIcon()
    {
        // Устанавливаем иконку
        if (iconImage != null && projectileIcon != null)
        {
            iconImage.sprite = projectileIcon;
        }
        
        // Устанавливаем название
        if (nameText != null)
        {
            nameText.text = projectileName;
        }
        
        // Устанавливаем нормальный цвет
        if (iconImage != null)
        {
            iconImage.color = normalColor;
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        SelectProjectile();
    }
    
    public void SelectProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"ProjectileIcon: Префаб снаряда не назначен для {projectileName}");
            return;
        }
        
        // Отправляем событие выбора снаряда
        GlobalEvents.ProjectileSelected.Invoke(projectilePrefab);
        
        // Уведомляем инвентарь о выборе этой иконки
        ProjectileInventory inventory = FindObjectOfType<ProjectileInventory>();
        if (inventory != null)
        {
            inventory.SelectIcon(this);
        }
        
    }
    
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();
    }
    
    void UpdateVisualState()
    {
        if (iconImage != null)
        {
            iconImage.color = isSelected ? selectedColor : normalColor;
        }
        
        float targetScale = isSelected ? selectedScale : normalScale;
        transform.localScale = originalScale * targetScale;
    }
    
    // Публичные методы для настройки
    public void SetProjectilePrefab(GameObject prefab)
    {
        projectilePrefab = prefab;
    }
    
    public void SetProjectileName(string name)
    {
        projectileName = name;
        if (nameText != null)
        {
            nameText.text = projectileName;
        }
    }
    
    public void SetProjectileIcon(Sprite icon)
    {
        projectileIcon = icon;
        if (iconImage != null)
        {
            iconImage.sprite = projectileIcon;
        }
    }
    
    // Получение информации
    public GameObject GetProjectilePrefab() => projectilePrefab;
    public string GetProjectileName() => projectileName;
    public bool IsSelected() => isSelected;
}
