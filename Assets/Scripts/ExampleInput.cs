using UnityEngine;

public class ExampleInput : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float raycastDistance = 1000f;        // Максимальная дистанция луча
    public LayerMask raycastLayerMask = ~0;      // Слои для проверки луча
    public KeyCode shootKey = KeyCode.Mouse0;    // Клавиша для стрельбы (левая кнопка мыши)
    
    [Header("Debug")]
    public bool showDebugRay = true;             // Показывать луч в Scene View
    public Color rayColor = Color.red;            // Цвет луча для отладки
    public float debugRayDuration = 0.1f;       // Длительность отображения луча

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("ExampleInput: Camera component not found!");
            enabled = false;
        }
    }

    void Update()
    {
        // Проверяем нажатие клавиши стрельбы
        if (Input.GetKeyDown(shootKey))
        {
            ShootRaycast();
        }
    }

    void ShootRaycast()
    {
        if (cam == null) return;

        // Создаем луч из центра экрана (или позиции мыши)
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
        // Выполняем raycast
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask))
        {
            // Проверяем, попал ли луч в объект с компонентом VoxelChunk16
            VoxelChunk16 voxelChunk = hit.collider.GetComponent<VoxelChunk16>();
            
            if (voxelChunk != null)
            {
                // Вызываем событие с позицией попадания
                GlobalEvents.ShootPosition.Invoke(hit.point);
                
                Debug.Log($"ExampleInput: Hit VoxelChunk16 at position {hit.point}");
            }
            else
            {
                Debug.Log($"ExampleInput: Hit object '{hit.collider.name}' but it's not a VoxelChunk16");
            }
            
            // Показываем луч для отладки
            if (showDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, rayColor, debugRayDuration);
            }
        }
        else
        {
            Debug.Log("ExampleInput: Raycast didn't hit anything");
            
            // Показываем луч для отладки (до максимальной дистанции)
            if (showDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * raycastDistance, rayColor, debugRayDuration);
            }
        }
    }

    // Альтернативный метод для стрельбы из центра экрана
    public void ShootFromScreenCenter()
    {
        if (cam == null) return;

        // Создаем луч из центра экрана
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
        
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask))
        {
            VoxelChunk16 voxelChunk = hit.collider.GetComponent<VoxelChunk16>();
            
            if (voxelChunk != null)
            {
                GlobalEvents.ShootPosition.Invoke(hit.point);
                Debug.Log($"ExampleInput: Hit VoxelChunk16 at center screen position {hit.point}");
            }
        }
    }

    // Метод для стрельбы в определенную позицию
    public void ShootAtPosition(Vector3 worldPosition)
    {
        GlobalEvents.ShootPosition.Invoke(worldPosition);
        Debug.Log($"ExampleInput: Shooting at specified position {worldPosition}");
    }

    void OnDrawGizmos()
    {
        if (!showDebugRay || cam == null) return;

        // Рисуем луч в Scene View для отладки
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Gizmos.color = rayColor;
        Gizmos.DrawRay(ray.origin, ray.direction * raycastDistance);
    }
}
