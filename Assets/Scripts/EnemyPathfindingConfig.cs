using UnityEngine;

[CreateAssetMenu(fileName = "EnemyPathfindingConfig", menuName = "Enemy/Pathfinding Config")]
public class EnemyPathfindingConfig : ScriptableObject
{
    [Header("Pathfinding Settings")]
    public bool usePathfinding = true;              // Использовать поиск пути
    public float pathUpdateInterval = 1f;           // Интервал обновления пути (секунды)
    public int maxPathLength = 50;                  // Максимальная длина пути
    public float waypointReachDistance = 1.5f;     // Дистанция достижения точки пути
    
    [Header("Stuck Detection")]
    public float stuckCheckTime = 2f;              // Время для проверки застревания
    public float stuckDistanceThreshold = 1f;      // Минимальное расстояние для незастревания
    public float stuckAreaSize = 4f;               // Размер области застревания (4x4 по горизонтали)
    public int maxStuckAttempts = 3;               // Максимум попыток выбраться
    public float unstuckPatrolTime = 5f;           // Время патруля после застревания
    
    [Header("Oscillation Detection")]
    public float oscillationThreshold = 1.5f;     // Порог для обнаружения колебаний около цели
}
