using UnityEngine;
using System;

/// <summary>
/// Система сохранения данных для отдельной сущности.
/// Каждая сущность создает свой экземпляр через конструктор.
/// Использует JSON сериализацию и PlayerPrefs для хранения.
/// </summary>
/// <typeparam name="T">Тип данных для сохранения (должен быть сериализуемым)</typeparam>
public class SaveData<T> where T : class, new()
{
    private string saveKey;
    private T cachedData;
    private bool isDirty = false;
    
    /// <summary>
    /// Создает новый экземпляр SaveData для сущности
    /// </summary>
    /// <param name="entityName">Имя сущности (обычно gameObject.name)</param>
    public SaveData(string entityName)
    {
        if (string.IsNullOrEmpty(entityName))
        {
            Debug.LogError("SaveData: entityName не может быть пустым!");
            entityName = "UnknownEntity_" + Guid.NewGuid().ToString();
        }
        
        saveKey = $"SaveData_{entityName}";
        cachedData = new T();
        
        Debug.Log($"SaveData создан для сущности: {entityName}, ключ: {saveKey}");
    }
    
    /// <summary>
    /// Получить текущие данные (кешированные или загруженные)
    /// </summary>
    public T Data
    {
        get
        {
            if (cachedData == null)
            {
                cachedData = Load();
            }
            return cachedData;
        }
        set
        {
            cachedData = value;
            isDirty = true;
        }
    }
    
    /// <summary>
    /// Сохранить данные в PlayerPrefs
    /// </summary>
    public void Save()
    {
        if (cachedData == null)
        {
            Debug.LogWarning($"SaveData [{saveKey}]: Попытка сохранить null данные");
            return;
        }
        
        try
        {
            string json = JsonUtility.ToJson(cachedData, prettyPrint: false);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save(); // Принудительно записываем на диск
            
            isDirty = false;
            
            Debug.Log($"SaveData [{saveKey}]: Данные сохранены успешно ({json.Length} символов)");
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveData [{saveKey}]: Ошибка при сохранении: {e.Message}");
        }
    }
    
    /// <summary>
    /// Сохранить данные, только если они изменились
    /// </summary>
    public void SaveIfDirty()
    {
        if (isDirty)
        {
            Save();
        }
    }
    
    /// <summary>
    /// Загрузить данные из PlayerPrefs
    /// </summary>
    public T Load()
    {
        if (!PlayerPrefs.HasKey(saveKey))
        {
            Debug.Log($"SaveData [{saveKey}]: Сохранение не найдено, создаем новые данные");
            cachedData = new T();
            return cachedData;
        }
        
        try
        {
            string json = PlayerPrefs.GetString(saveKey);
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"SaveData [{saveKey}]: Пустые данные, создаем новые");
                cachedData = new T();
                return cachedData;
            }
            
            cachedData = JsonUtility.FromJson<T>(json);
            
            if (cachedData == null)
            {
                Debug.LogWarning($"SaveData [{saveKey}]: Не удалось десериализовать, создаем новые данные");
                cachedData = new T();
            }
            else
            {
                Debug.Log($"SaveData [{saveKey}]: Данные загружены успешно");
            }
            
            isDirty = false;
            return cachedData;
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveData [{saveKey}]: Ошибка при загрузке: {e.Message}");
            cachedData = new T();
            return cachedData;
        }
    }
    
    /// <summary>
    /// Проверить, существует ли сохранение
    /// </summary>
    public bool Exists()
    {
        return PlayerPrefs.HasKey(saveKey);
    }
    
    /// <summary>
    /// Удалить сохранение
    /// </summary>
    public void Delete()
    {
        if (PlayerPrefs.HasKey(saveKey))
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            Debug.Log($"SaveData [{saveKey}]: Сохранение удалено");
        }
        
        cachedData = new T();
        isDirty = false;
    }
    
    /// <summary>
    /// Принудительно пометить данные как измененные
    /// </summary>
    public void MarkDirty()
    {
        isDirty = true;
    }
    
    /// <summary>
    /// Проверить, были ли данные изменены с момента последнего сохранения
    /// </summary>
    public bool IsDirty()
    {
        return isDirty;
    }
    
    /// <summary>
    /// Получить ключ сохранения
    /// </summary>
    public string GetSaveKey()
    {
        return saveKey;
    }
    
    /// <summary>
    /// Получить JSON представление данных (для отладки)
    /// </summary>
    public string ToJson(bool prettyPrint = true)
    {
        if (cachedData == null)
            return "null";
        
        return JsonUtility.ToJson(cachedData, prettyPrint);
    }
}

/// <summary>
/// Глобальный менеджер сохранений для управления всеми SaveData
/// </summary>
public static class SaveManager
{
    /// <summary>
    /// Удалить все сохранения игры
    /// </summary>
    public static void DeleteAllSaves()
    {
        Debug.Log("SaveManager: Удаление всех сохранений...");
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("SaveManager: Все сохранения удалены");
    }
    
    /// <summary>
    /// Удалить сохранение по имени сущности
    /// </summary>
    public static void DeleteSave(string entityName)
    {
        string saveKey = $"SaveData_{entityName}";
        if (PlayerPrefs.HasKey(saveKey))
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            Debug.Log($"SaveManager: Сохранение '{entityName}' удалено");
        }
        else
        {
            Debug.LogWarning($"SaveManager: Сохранение '{entityName}' не найдено");
        }
    }
    
    /// <summary>
    /// Проверить существование сохранения по имени сущности
    /// </summary>
    public static bool HasSave(string entityName)
    {
        string saveKey = $"SaveData_{entityName}";
        return PlayerPrefs.HasKey(saveKey);
    }
    
    /// <summary>
    /// Получить список всех ключей сохранений (только для отладки, медленно)
    /// </summary>
    public static void DebugPrintAllSaves()
    {
        Debug.Log("SaveManager: Список всех сохранений в PlayerPrefs:");
        
        // PlayerPrefs не предоставляет прямого способа получить все ключи
        // Можно только проверять известные ключи
        Debug.Log("Для получения всех ключей требуется платформо-специфичный код");
        Debug.Log("Сохранения хранятся с префиксом 'SaveData_'");
    }
}

