using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Данные бота в воксельной системе
/// </summary>
[System.Serializable]
public class VoxelBotData
{
    public string botId;
    public Vector3 worldPosition;
    public Vector3Int voxelPosition;
    public Vector3 velocity;
    public bool isGrounded;
    public bool isJumping;
    public float currentHeight; // Текущая высота над вокселем
    public bool isDead; // Состояние смерти
    public VoxelBotConfig config;
    
    // Для проверки попаданий
    public Bounds bounds;
    
    public VoxelBotData(string id, Vector3 position, VoxelBotConfig botConfig)
    {
        botId = id;
        worldPosition = position;
        voxelPosition = VoxelWorld.WorldToVoxel(position);
        velocity = Vector3.zero;
        isGrounded = true;
        isJumping = false;
        currentHeight = 0f;
        isDead = false;
        config = botConfig;
        
        // Проверяем что конфиг назначен
        if (config == null)
        {
            Debug.LogError($"[VoxelBotData] VoxelBotConfig is null for bot {id}! Cannot initialize bounds.");
            return;
        }
        
        // Вычисляем границы для проверки попаданий
        UpdateBounds();
    }
    
    public void UpdateBounds()
    {
        if (config == null)
        {
            Debug.LogError($"[VoxelBotData] Cannot update bounds - config is null for bot {botId}!");
            return;
        }
        
        Vector3 center = worldPosition + Vector3.up * (config.botHeightVoxels * 0.5f);
        Vector3 size = new Vector3(config.botDiameterVoxels, config.botHeightVoxels, config.botDiameterVoxels);
        bounds = new Bounds(center, size);
    }
    
    public void UpdatePosition(Vector3 newPosition)
    {
        worldPosition = newPosition;
        voxelPosition = VoxelWorld.WorldToVoxel(newPosition);
        UpdateBounds();
    }
}

/// <summary>
/// База данных всех ботов в воксельной системе
/// </summary>
public class VoxelBotDatabase : MonoBehaviour
{
    private static VoxelBotDatabase _instance;
    public static VoxelBotDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<VoxelBotDatabase>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("VoxelBotDatabase");
                    _instance = go.AddComponent<VoxelBotDatabase>();
                }
            }
            return _instance;
        }
    }
    
    private Dictionary<string, VoxelBotData> bots = new Dictionary<string, VoxelBotData>();
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Регистрирует бота в базе данных
    /// </summary>
    public void RegisterBot(string botId, Vector3 position, VoxelBotConfig config)
    {
        VoxelBotData botData = new VoxelBotData(botId, position, config);
        bots[botId] = botData;
        
        Debug.Log($"[VoxelBotDatabase] Registered bot: {botId} at {position}");
    }
    
    /// <summary>
    /// Удаляет бота из базы данных
    /// </summary>
    public void UnregisterBot(string botId)
    {
        if (bots.ContainsKey(botId))
        {
            bots.Remove(botId);
            Debug.Log($"[VoxelBotDatabase] Unregistered bot: {botId}");
        }
    }
    
    /// <summary>
    /// Получает данные бота
    /// </summary>
    public VoxelBotData GetBotData(string botId)
    {
        bots.TryGetValue(botId, out VoxelBotData data);
        return data;
    }
    
    /// <summary>
    /// Обновляет позицию бота
    /// </summary>
    public void UpdateBotPosition(string botId, Vector3 newPosition)
    {
        if (bots.TryGetValue(botId, out VoxelBotData data))
        {
            data.UpdatePosition(newPosition);
        }
    }
    
    /// <summary>
    /// Обновляет данные бота
    /// </summary>
    public void UpdateBotData(string botId, VoxelBotData newData)
    {
        if (bots.ContainsKey(botId))
        {
            bots[botId] = newData;
        }
    }
    
    /// <summary>
    /// Получает всех ботов в радиусе
    /// </summary>
    public List<VoxelBotData> GetBotsInRadius(Vector3 center, float radius)
    {
        List<VoxelBotData> result = new List<VoxelBotData>();
        
        foreach (var botData in bots.Values)
        {
            float distance = Vector3.Distance(center, botData.worldPosition);
            if (distance <= radius)
            {
                result.Add(botData);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Проверяет попадание в бота
    /// </summary>
    public VoxelBotData CheckHit(Vector3 hitPoint, float radius)
    {
        foreach (var botData in bots.Values)
        {
            if (botData.bounds.Contains(hitPoint) || 
                Vector3.Distance(hitPoint, botData.bounds.ClosestPoint(hitPoint)) <= radius)
            {
                return botData;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Получает количество зарегистрированных ботов
    /// </summary>
    public int GetBotCount()
    {
        return bots.Count;
    }
    
    /// <summary>
    /// Получает всех ботов
    /// </summary>
    public Dictionary<string, VoxelBotData> GetAllBots()
    {
        return new Dictionary<string, VoxelBotData>(bots);
    }
}
