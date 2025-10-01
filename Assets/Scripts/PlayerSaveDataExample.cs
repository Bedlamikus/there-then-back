using UnityEngine;
using System;

/// <summary>
/// Пример структуры данных для сохранения состояния игрока
/// ВАЖНО: Класс должен быть помечен [Serializable] для работы с JsonUtility
/// </summary>
[Serializable]
public class PlayerData
{
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
    public int health = 100;
    public int score = 0;
    
    // Конструктор по умолчанию обязателен!
    public PlayerData()
    {
        health = 100;
        score = 0;
    }
    
    public Vector3 GetPosition()
    {
        return new Vector3(posX, posY, posZ);
    }
    
    public void SetPosition(Vector3 pos)
    {
        posX = pos.x;
        posY = pos.y;
        posZ = pos.z;
    }
    
    public Quaternion GetRotation()
    {
        return new Quaternion(rotX, rotY, rotZ, rotW);
    }
    
    public void SetRotation(Quaternion rot)
    {
        rotX = rot.x;
        rotY = rot.y;
        rotZ = rot.z;
        rotW = rot.w;
    }
}

/// <summary>
/// Пример компонента для сохранения состояния игрока
/// Показывает как использовать SaveData<T> в MonoBehaviour
/// </summary>
public class PlayerSaveDataExample : MonoBehaviour
{
    private SaveData<PlayerData> saveData;
    
    [Header("Save Settings")]
    public bool autoSave = true;
    public float autoSaveInterval = 5f;
    private float lastSaveTime;
    
    void Start()
    {
        // Создаем SaveData с именем этого GameObject
        saveData = new SaveData<PlayerData>(gameObject.name);
        
        // Загружаем сохраненные данные или используем текущие
        if (saveData.Exists())
        {
            LoadPlayer();
        }
        else
        {
            Debug.Log($"[{gameObject.name}] Сохранение не найдено, используем текущее состояние");
        }
        
        lastSaveTime = Time.time;
    }
    
    void Update()
    {
        // Автосохранение
        if (autoSave && Time.time - lastSaveTime >= autoSaveInterval)
        {
            SavePlayer();
            lastSaveTime = Time.time;
        }
    }
    
    void OnApplicationQuit()
    {
        // Сохраняем при выходе
        if (autoSave)
        {
            SavePlayer();
        }
    }
    
    /// <summary>
    /// Сохранить текущее состояние игрока
    /// </summary>
    [ContextMenu("Save Player")]
    public void SavePlayer()
    {
        // Получаем данные
        var data = saveData.Data;
        
        // Обновляем данные текущим состоянием
        data.SetPosition(transform.position);
        data.SetRotation(transform.rotation);
        // data.health = ... (если есть компонент здоровья)
        // data.score = ... (если есть система очков)
        
        // Сохраняем
        saveData.Save();
        
        Debug.Log($"[{gameObject.name}] Состояние игрока сохранено: позиция {transform.position}");
    }
    
    /// <summary>
    /// Загрузить сохраненное состояние игрока
    /// </summary>
    [ContextMenu("Load Player")]
    public void LoadPlayer()
    {
        var data = saveData.Load();
        
        // Применяем загруженные данные
        transform.position = data.GetPosition();
        transform.rotation = data.GetRotation();
        // health = data.health;
        // score = data.score;
        
        Debug.Log($"[{gameObject.name}] Состояние игрока загружено: позиция {transform.position}");
    }
    
    /// <summary>
    /// Удалить сохранение
    /// </summary>
    [ContextMenu("Delete Save")]
    public void DeleteSave()
    {
        saveData.Delete();
        Debug.Log($"[{gameObject.name}] Сохранение удалено");
    }
    
    /// <summary>
    /// Пример изменения данных напрямую
    /// </summary>
    public void AddScore(int points)
    {
        var data = saveData.Data;
        data.score += points;
        saveData.MarkDirty(); // Помечаем как измененные
        saveData.SaveIfDirty(); // Сохраняем если изменены
    }
}

