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

    [Header("Camera Distance")]
    public float maxCameraDistance = 15f;           // Максимальное расстояние от игрока
    public float minCameraDistance = 2f;            // Минимальное расстояние до игрока
    public float cameraMoveSpeed = 5f;              // Скорость приближения к игроку
    public float cameraReturnSpeed = 3f;            // Скорость возврата к максимальному расстоянию

    [Header("Collision Detection")]
    public LayerMask collisionLayerMask = -1;      // Слои для проверки коллизий
    public float hysteresisDistance = 1f;           // Расстояние для проверки гистерезиса
    public LayerMask playerLayerMask = -1;          // Слои игрока для исключения из коллизий
    
    [Header("Aim Point")]
    public float aimRaycastDistance = 100f;         // Максимальная дистанция рейкаста для прицеливания
    public LayerMask aimLayerMask = -1;             // Слои для рейкаста прицеливания

    [Header("Joystick Input")]
    public FloatingJoystick cameraJoystick;        // Джойстик для управления камерой

    // Приватные переменные
    private float currentHorizontalAngle = 0f;      // Текущий горизонтальный угол
    private float currentVerticalAngle = 0f;        // Текущий вертикальный угол
    private float currentCameraDistance;            // Текущее расстояние до игрока
    private Vector3 lastValidCameraPosition;        // Последняя валидная позиция камеры

    // Переменные для новой логики коллизий
    private bool isMovingTowardsPlayer = false;     // Флаг движения к игроку
    private bool isReturningToMaxDistance = false;  // Флаг возврата к максимальному расстоянию

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
        if (virtualCamera == null || PlayerTransform == null || cameraJoystick == null)
            return;

        // Получаем ввод от джойстика
        Vector2 joystickInput = cameraJoystick.Direction;

        // Обновляем углы камеры
        UpdateCameraAngles(joystickInput);

        // Применяем вращение камеры
        ApplyCameraRotation();
        
        // Публикуем точку прицеливания в центре экрана
        PublishAimPoint();
    }

    void InitializeCameraPosition()
    {
        if (PlayerTransform != null)
        {
            currentCameraDistance = maxCameraDistance;
            isMovingTowardsPlayer = false;
            isReturningToMaxDistance = false;

            // Устанавливаем начальную позицию камеры
            if (virtualCamera != null)
            {
                Vector3 initialPosition = GetDesiredCameraPosition();
                virtualCamera.transform.position = initialPosition;

                // Направляем камеру на точку выше игрока
                Vector3 initialLookTarget = GetLookTarget();
                virtualCamera.transform.LookAt(initialLookTarget);

                lastValidCameraPosition = initialPosition;
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
        if (virtualCamera == null || PlayerTransform == null)
            return;

        // Вычисляем желаемую позицию камеры на основе углов
        Vector3 desiredPosition = GetDesiredCameraPosition();

        // Применяем новую логику коллизий
        Vector3 finalPosition = CheckCameraCollision(desiredPosition);

        // Плавно перемещаем камеру
        Transform cameraTransform = virtualCamera.transform;
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, finalPosition, smoothingFactor * Time.deltaTime);

        // Направляем камеру на точку выше игрока
        Vector3 lookTarget = GetLookTarget();
        Vector3 lookDirection = lookTarget - cameraTransform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, smoothingFactor * Time.deltaTime);
        }

        // Сохраняем валидную позицию
        lastValidCameraPosition = cameraTransform.position;
    }

    Vector3 GetDesiredCameraPosition()
    {
        // Создаем вращение на основе углов
        Quaternion horizontalRotation = Quaternion.AngleAxis(currentHorizontalAngle, Vector3.up);
        Quaternion verticalRotation = Quaternion.AngleAxis(currentVerticalAngle, Vector3.right);
        Quaternion combinedRotation = horizontalRotation * verticalRotation;

        // Применяем вращение к смещению камеры
        Vector3 rotatedOffset = combinedRotation * new Vector3(0, 0, -currentCameraDistance);
        rotatedOffset.y += cameraHeight; // Добавляем высоту

        return playerTransform.position + rotatedOffset;
    }

    Vector3 CheckCameraCollision(Vector3 desiredPosition)
    {
        if (playerTransform == null) return desiredPosition;

        Vector3 lookTarget = GetLookTarget();
        LayerMask effectiveCollisionMask = collisionLayerMask & ~playerLayerMask;

        // Проверяем луч от точки взгляда к желаемой позиции камеры
        Vector3 directionToCamera = (desiredPosition - lookTarget).normalized;
        float distanceToCamera = Vector3.Distance(lookTarget, desiredPosition);

        RaycastHit hit;
        bool hasCollision = Physics.Raycast(lookTarget, directionToCamera, out hit, distanceToCamera, effectiveCollisionMask);

        if (hasCollision)
        {
            // Есть коллизия - начинаем движение к игроку
            isMovingTowardsPlayer = true;
            isReturningToMaxDistance = false;

            // Плавно приближаемся к игроку
            float newDistance = currentCameraDistance - cameraMoveSpeed * Time.deltaTime;
            currentCameraDistance = Mathf.Max(newDistance, minCameraDistance);
        }
        else
        {
            // Нет коллизии
            if (isMovingTowardsPlayer)
            {
                // Мы приближались к игроку, теперь нужно проверить гистерезис
                // Проверяем луч от точки взгляда за камерой по направлению к игроку
                Vector3 currentCameraPos = virtualCamera != null ? virtualCamera.transform.position : desiredPosition;
                Vector3 directionToPlayer = (lookTarget - currentCameraPos).normalized;
                float hysteresisCheckDistance = hysteresisDistance;

                Vector3 hysteresisCheckPoint = currentCameraPos + directionToPlayer * hysteresisCheckDistance;
                float distanceToHysteresisPoint = Vector3.Distance(lookTarget, hysteresisCheckPoint);

                RaycastHit hysteresisHit;
                bool hasHysteresisCollision = Physics.Raycast(lookTarget, directionToPlayer, out hysteresisHit, distanceToHysteresisPoint, effectiveCollisionMask);

                if (!hasHysteresisCollision)
                {
                    // Гистерезис прошел - начинаем возврат к максимальному расстоянию
                    isMovingTowardsPlayer = false;
                    isReturningToMaxDistance = true;
                }
            }

            if (isReturningToMaxDistance)
            {
                // Возвращаемся к максимальному расстоянию
                float newDistance = currentCameraDistance + cameraReturnSpeed * Time.deltaTime;
                currentCameraDistance = Mathf.Min(newDistance, maxCameraDistance);

                // Проверяем, не столкнулись ли мы снова при возврате
                Vector3 returnPosition = GetDesiredCameraPosition();
                Vector3 returnDirection = (returnPosition - lookTarget).normalized;
                float returnDistance = Vector3.Distance(lookTarget, returnPosition);

                RaycastHit returnHit;
                bool hasReturnCollision = Physics.Raycast(lookTarget, returnDirection, out returnHit, returnDistance, effectiveCollisionMask);

                if (hasReturnCollision)
                {
                    // Снова столкнулись - начинаем движение к игроку
                    isReturningToMaxDistance = false;
                    isMovingTowardsPlayer = true;
                }
                else if (currentCameraDistance >= maxCameraDistance)
                {
                    // Успешно вернулись к максимальному расстоянию
                    isReturningToMaxDistance = false;
                }
            }
        }

        // Вычисляем финальную позицию камеры
        return GetDesiredCameraPosition();
    }

    // Публичные методы для внешнего управления
    public void ResetCamera()
    {
        currentHorizontalAngle = 0f;
        currentVerticalAngle = 0f;
        currentCameraDistance = maxCameraDistance;
        isMovingTowardsPlayer = false;
        isReturningToMaxDistance = false;
        InitializeCameraPosition();
    }

    public void SetMaxCameraDistance(float distance)
    {
        maxCameraDistance = distance;
        if (currentCameraDistance > maxCameraDistance)
        {
            currentCameraDistance = maxCameraDistance;
        }
    }

    public void SetCameraHeight(float height)
    {
        cameraHeight = height;
        InitializeCameraPosition();
    }

    // Методы для получения текущих углов
    public float GetHorizontalAngle() => currentHorizontalAngle;
    public float GetVerticalAngle() => currentVerticalAngle;

    // Методы для получения информации о состоянии камеры
    public bool IsMovingTowardsPlayer() => isMovingTowardsPlayer;
    public bool IsReturningToMaxDistance() => isReturningToMaxDistance;
    public float GetCurrentCameraDistance() => currentCameraDistance;

    /// <summary>
    /// Вычисляет и публикует точку прицеливания в центре экрана
    /// </summary>
    void PublishAimPoint()
    {
        if (virtualCamera == null) return;
        
        // Получаем позицию и направление камеры
        Transform camTransform = virtualCamera.transform;
        Vector3 origin = camTransform.position;
        Vector3 direction = camTransform.forward;
        
        // Исключаем слой игрока из проверки
        LayerMask effectiveAimMask = aimLayerMask & ~playerLayerMask;
        
        RaycastHit hit;
        Vector3 aimPoint;
        
        // Делаем raycast из центра камеры (без учета слоя игрока)
        if (Physics.Raycast(origin, direction, out hit, aimRaycastDistance, effectiveAimMask))
        {
            // Попали в что-то - используем точку попадания
            aimPoint = hit.point;
        }
        else
        {
            // Не попали - используем точку на максимальной дистанции
            aimPoint = origin + direction * aimRaycastDistance;
        }
        
        // Публикуем точку прицеливания
        GlobalEvents.CameraAimPoint.Invoke(aimPoint);
    }
    
    // Методы для получения информации о камере
    public Vector3 GetLookTarget()
    {
        if (playerTransform == null) return Vector3.zero;
        return playerTransform.position + Vector3.up * cameraHeight;
    }
    
    /// <summary>
    /// Получает текущую точку прицеливания (публичный метод)
    /// </summary>
    public Vector3 GetAimPoint()
    {
        if (virtualCamera == null) return Vector3.zero;
        
        Transform camTransform = virtualCamera.transform;
        Vector3 origin = camTransform.position;
        Vector3 direction = camTransform.forward;
        
        // Исключаем слой игрока из проверки
        LayerMask effectiveAimMask = aimLayerMask & ~playerLayerMask;
        
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, aimRaycastDistance, effectiveAimMask))
        {
            return hit.point;
        }
        else
        {
            return origin + direction * aimRaycastDistance;
        }
    }

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

            // Рисуем точку взгляда камеры
            Vector3 lookTarget = playerTransform.position + Vector3.up * cameraHeight;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lookTarget, 0.5f);

            // Рисуем линию от игрока к точке взгляда
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(playerTransform.position, lookTarget);

            // Рисуем информацию о состоянии камеры
            if (virtualCamera != null)
            {
                // Цвет камеры в зависимости от состояния
                if (isMovingTowardsPlayer)
                    Gizmos.color = Color.red;
                else if (isReturningToMaxDistance)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.yellow;

                Gizmos.DrawWireSphere(virtualCamera.transform.position, 0.5f);

                // Рисуем луч от точки взгляда к камере
                Vector3 gizmoLookTarget = GetLookTarget();
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(gizmoLookTarget, virtualCamera.transform.position);

                // Рисуем гистерезис если движемся к игроку
                if (isMovingTowardsPlayer)
                {
                    Vector3 directionToPlayer = (gizmoLookTarget - virtualCamera.transform.position).normalized;
                    Vector3 hysteresisPoint = virtualCamera.transform.position + directionToPlayer * hysteresisDistance;
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(hysteresisPoint, 0.3f);
                    Gizmos.DrawLine(virtualCamera.transform.position, hysteresisPoint);
                }
            }

            // Рисуем минимальное расстояние
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerTransform.position, minCameraDistance);

            // Рисуем желаемое расстояние
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerTransform.position, cameraDistance);

            // Рисуем максимальное расстояние
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerTransform.position, maxCameraDistance);
        }
    }

    private Transform PlayerTransform
    {
        get
        {
            if (playerTransform == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (!player) return playerTransform;

                playerTransform = player.transform;
            }
            return playerTransform;
        }
    }
}
