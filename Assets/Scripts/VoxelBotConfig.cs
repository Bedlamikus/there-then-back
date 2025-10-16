using UnityEngine;

[CreateAssetMenu(fileName = "VoxelBotConfig", menuName = "Voxel Bot/Config")]
public class VoxelBotConfig : ScriptableObject
{
    [Header("Bot Dimensions")]
    [Tooltip("Диаметр бота в единицах")]
    public float botDiameter = 0.7f;
    
    [Tooltip("Высота бота в единицах")]
    public float botHeight = 1.8f;
    
    [Header("Movement Settings")]
    [Tooltip("Скорость движения бота (единиц в секунду)")]
    public float moveSpeed = 3f;
    
    [Tooltip("Высота прыжка в единицах")]
    public float jumpHeight = 1.5f;
    
    [Tooltip("Длина прыжка в единицах")]
    public float jumpDistance = 2.5f;
    
    [Header("Pathfinding Settings")]
    [Tooltip("Максимальная дистанция поиска пути")]
    public float maxPathfindingDistance = 50f;
    
    [Tooltip("Точность достижения цели (в единицах)")]
    public float goalReachDistance = 0.5f;
    
    [Tooltip("Интервал обновления пути (секунды)")]
    public float pathUpdateInterval = 0.5f;
    
    [Header("Detection Settings")]
    [Tooltip("Радиус обнаружения игрока")]
    public float detectionRadius = 15f;
    
    [Tooltip("Максимальная дистанция атаки")]
    public float attackRange = 10f;
    
    [Header("Physics Simulation")]
    [Tooltip("Гравитация для бота")]
    public float gravity = 9.81f;
    
    [Tooltip("Скорость падения")]
    public float fallSpeed = 5f;
    
    [Tooltip("Высота проверки земли под ботом")]
    public float groundCheckHeight = 0.1f;
}
