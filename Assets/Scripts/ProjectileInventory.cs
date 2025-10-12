using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ProjectileInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    public Transform iconContainer;                 // Контейнер для иконок (ScrollView Content)
    public GameObject iconPrefab;                   // Префаб иконки снаряда
    public float iconSpacing = 10f;                 // Расстояние между иконками
    
    [Header("Test Settings")]
    public bool autoSelectFirst = false;            // Отключено - теперь PlayerWeaponController сам уведомляет UI
    public float autoSelectDelay = 1f;              // Задержка автовыбора (секунды)
    
    [Header("Projectile Prefabs")]
    public GameObject[] projectilePrefabs;          // Массив префабов снарядов
    public Sprite[] projectileIcons;                // Массив иконок для снарядов
    
    private List<ProjectileIcon> projectileIconComponents = new List<ProjectileIcon>();
    private ProjectileIcon selectedIcon;
    
    void Start()
    {
        // Подписываемся на событие выбора снаряда
        GlobalEvents.ProjectileSelected.AddListener(OnProjectileSelected);
        
        // Создаем иконки для всех снарядов
        CreateProjectileIcons();
        
        // Автоматически выбираем первый снаряд для тестирования (если включено)
        if (autoSelectFirst && projectilePrefabs.Length > 0)
        {
            StartCoroutine(AutoSelectFirstProjectile());
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от события
        GlobalEvents.ProjectileSelected.RemoveListener(OnProjectileSelected);
    }
    
    /// <summary>
    /// Обработчик события выбора снаряда - синхронизирует UI
    /// </summary>
    void OnProjectileSelected(GameObject selectedProjectile)
    {
        if (selectedProjectile == null) return;
        
        // Находим иконку для этого префаба
        foreach (ProjectileIcon icon in projectileIconComponents)
        {
            if (icon != null && icon.GetProjectilePrefab() == selectedProjectile)
            {
                SelectProjectileIcon(icon);
                Debug.Log($"ProjectileInventory: UI синхронизирован с выбранным снарядом: {selectedProjectile.name}");
                return;
            }
        }
    }
    
    void CreateProjectileIcons()
    {
        if (iconContainer == null || iconPrefab == null)
        {
            Debug.LogWarning("ProjectileInventory: iconContainer или iconPrefab не назначены!");
            return;
        }
        
        // Проверяем соответствие массивов
        ValidateIconArrays();
        
        // Очищаем существующие иконки
        ClearIcons();
        
        // Создаем иконки для каждого префаба снаряда
        for (int i = 0; i < projectilePrefabs.Length; i++)
        {
            GameObject projectilePrefab = projectilePrefabs[i];
            if (projectilePrefab == null) continue;
            
            // Создаем иконку
            GameObject iconObject = Instantiate(iconPrefab, iconContainer);
            ProjectileIcon projectileIcon = iconObject.GetComponent<ProjectileIcon>();
            
            if (projectileIcon == null)
            {
                projectileIcon = iconObject.AddComponent<ProjectileIcon>();
            }
            
            // Настраиваем иконку
            SetupProjectileIcon(projectileIcon, projectilePrefab, i);
            
            // Добавляем в список
            projectileIconComponents.Add(projectileIcon);
        }
        
        Debug.Log($"ProjectileInventory: Создано {projectileIconComponents.Count} иконок снарядов");
    }
    
    void ValidateIconArrays()
    {
        // Проверяем соответствие количества префабов и иконок
        if (projectileIcons != null && projectileIcons.Length != projectilePrefabs.Length)
        {
            Debug.LogWarning($"ProjectileInventory: Количество иконок ({projectileIcons.Length}) не соответствует количеству префабов ({projectilePrefabs.Length})!");
            Debug.LogWarning("Убедитесь, что массив projectileIcons имеет столько же элементов, сколько projectilePrefabs.");
        }
        
        // Проверяем наличие пустых иконок
        if (projectileIcons != null)
        {
            for (int i = 0; i < projectileIcons.Length; i++)
            {
                if (projectileIcons[i] == null)
                {
                    Debug.LogWarning($"ProjectileInventory: Иконка с индексом {i} не назначена!");
                }
            }
        }
    }
    
    void SetupProjectileIcon(ProjectileIcon projectileIcon, GameObject projectilePrefab, int index)
    {
        // Устанавливаем префаб
        projectileIcon.SetProjectilePrefab(projectilePrefab);
        
        // Устанавливаем название (берем из имени префаба)
        string projectileName = projectilePrefab.name.Replace("(Clone)", "").Replace("Prefab", "");
        projectileIcon.SetProjectileName(projectileName);
        
        // Устанавливаем иконку из массива или ищем в префабе
        Sprite iconSprite = GetProjectileIcon(projectilePrefab, index);
        if (iconSprite != null)
        {
            projectileIcon.SetProjectileIcon(iconSprite);
        }
        
        // Позиционируем иконку
        RectTransform iconRect = projectileIcon.GetComponent<RectTransform>();
        if (iconRect != null)
        {
            iconRect.anchoredPosition = new Vector2(index * (iconRect.sizeDelta.x + iconSpacing), 0);
        }
    }
    
    Sprite GetProjectileIcon(GameObject projectilePrefab, int index)
    {
        // Сначала проверяем массив иконок
        if (projectileIcons != null && index >= 0 && index < projectileIcons.Length && projectileIcons[index] != null)
        {
            return projectileIcons[index];
        }
        
        // Если иконка не найдена в массиве, пытаемся найти в префабе
        SpriteRenderer spriteRenderer = projectilePrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.sprite;
        }
        
        // Пытаемся найти в дочерних объектах
        SpriteRenderer childSpriteRenderer = projectilePrefab.GetComponentInChildren<SpriteRenderer>();
        if (childSpriteRenderer != null && childSpriteRenderer.sprite != null)
        {
            return childSpriteRenderer.sprite;
        }
        
        return null; // Иконка не найдена
    }
    
    IEnumerator AutoSelectFirstProjectile()
    {
        yield return new WaitForSeconds(autoSelectDelay);
        
        if (projectileIconComponents.Count > 0)
        {
            SelectProjectileIcon(projectileIconComponents[0]);
        }
    }
    
    void SelectProjectileIcon(ProjectileIcon icon)
    {
        // Снимаем выделение с предыдущей иконки
        if (selectedIcon != null)
        {
            selectedIcon.SetSelected(false);
        }
        
        // Выделяем новую иконку
        selectedIcon = icon;
        if (selectedIcon != null)
        {
            selectedIcon.SetSelected(true);
        }
    }
    
    void ClearIcons()
    {
        foreach (ProjectileIcon icon in projectileIconComponents)
        {
            if (icon != null)
            {
                DestroyImmediate(icon.gameObject);
            }
        }
        projectileIconComponents.Clear();
        selectedIcon = null;
    }
    
    // Публичные методы
    public void AddProjectilePrefab(GameObject projectilePrefab)
    {
        if (projectilePrefab == null) return;
        
        // Добавляем в массив
        GameObject[] newArray = new GameObject[projectilePrefabs.Length + 1];
        projectilePrefabs.CopyTo(newArray, 0);
        newArray[projectilePrefabs.Length] = projectilePrefab;
        projectilePrefabs = newArray;
        
        // Пересоздаем иконки
        CreateProjectileIcons();
    }
    
    public void RemoveProjectilePrefab(GameObject projectilePrefab)
    {
        List<GameObject> prefabList = new List<GameObject>(projectilePrefabs);
        if (prefabList.Remove(projectilePrefab))
        {
            projectilePrefabs = prefabList.ToArray();
            CreateProjectileIcons();
        }
    }
    
    public ProjectileIcon GetSelectedIcon() => selectedIcon;
    public List<ProjectileIcon> GetAllIcons() => new List<ProjectileIcon>(projectileIconComponents);
    
    // Публичные методы для управления иконками
    public void SetProjectileIcon(int index, Sprite icon)
    {
        if (projectileIcons != null && index >= 0 && index < projectileIcons.Length)
        {
            projectileIcons[index] = icon;
            
            // Обновляем иконку если она уже создана
            if (index < projectileIconComponents.Count && projectileIconComponents[index] != null)
            {
                projectileIconComponents[index].SetProjectileIcon(icon);
            }
        }
    }
    
    public Sprite GetProjectileIcon(int index)
    {
        if (projectileIcons != null && index >= 0 && index < projectileIcons.Length)
        {
            return projectileIcons[index];
        }
        return null;
    }
    
    public void RefreshIcons()
    {
        // Пересоздаем все иконки с новыми спрайтами
        CreateProjectileIcons();
    }
    
    // Публичный метод для выбора иконки (вызывается из ProjectileIcon)
    public void SelectIcon(ProjectileIcon icon)
    {
        SelectProjectileIcon(icon);
    }
}
