using UnityEngine;

public class PlayerVisualRotator : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Трансформ камеры/пивота для ориентации WASD")]
    public Transform cameraPivot;
    [Tooltip("Трансформ визуальной части игрока (моделька)")]
    public Transform visual;

    [Header("Settings")]
    [Tooltip("Скорость поворота визуала (град/сек)")]
    public float rotationSpeed = 720f;

    void Update()
    {
        // Читаем WASD
        float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        Vector2 input = new Vector2(x, y);
        if (input.sqrMagnitude < 0.0001f) return; // нет ввода — не вращаем

        // Направление в мире относительно камеры
        Vector3 fwd = cameraPivot.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = cameraPivot.right; right.y = 0f; right.Normalize();
        Vector3 dir = (right * input.x + fwd * input.y).normalized;

        // Целевой поворот
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        visual.rotation = Quaternion.RotateTowards(visual.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }
}
