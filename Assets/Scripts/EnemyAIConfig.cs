using UnityEngine;

[CreateAssetMenu(fileName = "EnemyAIConfig", menuName = "Enemy/AI Config")]
public class EnemyAIConfig : ScriptableObject
{
    [Header("Detection")]
    public float detectionRange = 15f;              // Радиус обнаружения цели
    public float attackRange = 3f;                  // Радиус атаки
    
    [Header("Patrol Settings")]
    public float patrolRadius = 10f;               // Радиус патрулирования
    public float minPatrolDistance = 3f;           // Минимальная дистанция до новой точки патруля
    public float patrolWaitTime = 2f;              // Время ожидания на точке патруля
    
    [Header("Patrol Rest System")]
    public int minPatrolPointsBeforeRest = 3;      // Минимум точек патруля перед отдыхом
    public int maxPatrolPointsBeforeRest = 6;       // Максимум точек патруля перед отдыхом
    public float minIdleTime = 30f;                // Минимальное время отдыха
    public float maxIdleTime = 120f;               // Максимальное время отдыха
    
    [Header("Return to Start")]
    public float returnToStartThreshold = 1.5f;    // Порог для возврата на исходную позицию
    
    [Header("Fall Recovery")]
    public float maxFallingTime = 10f;             // Максимальное время падения перед респавном
}
