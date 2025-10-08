using UnityEngine;

[CreateAssetMenu(fileName = "EnemyMovementConfig", menuName = "Enemy/Movement Config")]
public class EnemyMovementConfig : ScriptableObject
{
    [Header("Basic Movement")]
    public float moveSpeed = 5f;                    // Скорость движения
    public float turnSpeed = 10f;                   // Скорость поворота
    public float turnThreshold = 0.1f;             // Порог для поворота
    
    [Header("Jump System")]
    public float jumpHeight = 3f;                   // Высота прыжка
    public float jumpPrepareTime = 0.1f;           // Время остановки перед прыжком
    public float jumpCooldownTime = 0.5f;          // Время перед возобновлением движения
    
    [Header("Physics")]
    public float gravity = -9.81f;                  // Сила гравитации
    public float groundCheckDistance = 0.2f;       // Расстояние проверки земли
    public float coyoteTime = 0.1f;                // Время "койота" для прыжка
    
    [Header("Animation")]
    public string speedParameter = "Speed";        // Параметр скорости анимации
    public string isGroundedParameter = "IsGrounded"; // Параметр земли
}
