using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public CinemachineVirtualCamera virtualCamera;  // Ссылка на Cinemachine Virtual Camera
    public Transform playerTransform;               // Трансформ игрока (PlayerController)
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 90f;               // Скорость вращения камеры (градусы/сек)
    public float smoothingFactor = 5f;              // Фактор сглаживания вращения
    
    [Header("Camera Distance")]
    public float cameraDistance = 10f;              // Расстояние от игрока до камеры
    public float cameraHeight = 5f;                 // Высота камеры над игроком
    
    [Header("Limits")]
    public float minVerticalAngle = -60f;           // Минимальный вертикальный угол
    public float maxVerticalAngle = 60f;            // Максимальный вертикальный угол
    
    [Header("Joystick Input")]
    public FloatingJoystick cameraJoystick;        // Джойстик для управления камерой
    
    // Приватные переменные
    private float currentHorizontalAngle = 0f;      // Текущий горизонтальный угол
    private float currentVerticalAngle = 0f;        // Текущий вертикальный угол
    private Vector3 cameraOffset;                   // Смещение камеры от игрока
    
    void Start()
    {
        // Находим игрока если не назначен
        if (playerTransform == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
                playerTransform = player.transform;
        }
        
        // Находим Virtual Camera если не назначена
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }
        
        // Инициализируем начальное смещение камеры
        InitializeCameraPosition();
    }
    
    void Update()
    {
        if (virtualCamera == null || playerTransform == null || cameraJoystick == null)
            return;
        
        // Получаем ввод от джойстика
        Vector2 joystickInput = cameraJoystick.Direction;
        
        // Обновляем углы камеры
        UpdateCameraAngles(joystickInput);
        
        // Применяем вращение камеры
        ApplyCameraRotation();
    }
    
    void InitializeCameraPosition()
    {
        if (playerTransform != null)
        {
            // Устанавливаем начальное смещение камеры
            cameraOffset = new Vector3(0, cameraHeight, -cameraDistance);
            
            // Устанавливаем начальную позицию камеры
            if (virtualCamera != null)
            {
                Transform cameraTransform = virtualCamera.transform;
                cameraTransform.position = playerTransform.position + cameraOffset;
                cameraTransform.LookAt(playerTransform.position);
            }
        }
    }
    
    void UpdateCameraAngles(Vector2 joystickInput)
    {
        float dt = Time.deltaTime;
        
        // Горизонтальное вращение (X ось джойстика)
        currentHorizontalAngle += joystickInput.x * rotationSpeed * dt;
        
        // Вертикальное вращение (Y ось джойстика) с ограничениями
        float verticalInput = joystickInput.y * rotationSpeed * dt;
        float newVerticalAngle = currentVerticalAngle - verticalInput; // Инвертируем для интуитивности
        
        currentVerticalAngle = Mathf.Clamp(newVerticalAngle, minVerticalAngle, maxVerticalAngle);
    }
    
    void ApplyCameraRotation()
    {
        if (virtualCamera == null || playerTransform == null)
            return;
        
        // Создаем вращение на основе углов
        Quaternion horizontalRotation = Quaternion.AngleAxis(currentHorizontalAngle, Vector3.up);
        Quaternion verticalRotation = Quaternion.AngleAxis(currentVerticalAngle, Vector3.right);
        
        // Комбинируем вращения
        Quaternion combinedRotation = horizontalRotation * verticalRotation;
        
        // Применяем вращение к смещению камеры
        Vector3 rotatedOffset = combinedRotation * new Vector3(0, 0, -cameraDistance);
        rotatedOffset.y += cameraHeight; // Добавляем высоту
        
        // Вычисляем новую позицию камеры
        Vector3 targetPosition = playerTransform.position + rotatedOffset;
        
        // Сглаженно перемещаем камеру
        Transform cameraTransform = virtualCamera.transform;
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, smoothingFactor * Time.deltaTime);
        
        // Направляем камеру на игрока
        Vector3 lookDirection = playerTransform.position - cameraTransform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, smoothingFactor * Time.deltaTime);
        }
    }
    
    // Публичные методы для внешнего управления
    public void ResetCamera()
    {
        currentHorizontalAngle = 0f;
        currentVerticalAngle = 0f;
        InitializeCameraPosition();
    }
    
    public void SetCameraDistance(float distance)
    {
        cameraDistance = distance;
        InitializeCameraPosition();
    }
    
    public void SetCameraHeight(float height)
    {
        cameraHeight = height;
        InitializeCameraPosition();
    }
    
    // Методы для получения текущих углов
    public float GetHorizontalAngle() => currentHorizontalAngle;
    public float GetVerticalAngle() => currentVerticalAngle;
    
    void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            // Рисуем линию от игрока к камере
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(playerTransform.position, transform.position);
            
            // Рисуем сферу в позиции игрока
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerTransform.position, 1f);
        }
    }
}
