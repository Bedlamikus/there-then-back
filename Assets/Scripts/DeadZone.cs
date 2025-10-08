using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("Dead Zone Settings")]
    public LayerMask spawnableLayerMask = 1; // Слои для игрока и ботов
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DeadZone] OnTriggerEnter: {other.gameObject.name}, Layer: {other.gameObject.layer}, Position: {other.transform.position}");
        
        // Проверяем, что это сущность из нужного слоя
        if (((1 << other.gameObject.layer) & spawnableLayerMask) != 0)
        {
            // Проверяем наличие ISpawnable интерфейса
            ISpawnable spawnable = other.GetComponent<ISpawnable>();
            if (spawnable != null)
            {
                Debug.Log($"[DeadZone] Сущность '{spawnable.GetSpawnableID()}' попала в зону смерти!");
                // Уведомляем сервис автоспавна о попадании в зону смерти
                AutoSpawnService.Instance?.OnEnterDeadZone(spawnable);
            }
            else
            {
                Debug.LogWarning($"[DeadZone] Объект {other.gameObject.name} в нужном слое, но нет ISpawnable!");
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Визуализация зоны смерти в редакторе
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        
        // Показываем направление силы тяжести
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 2f);
    }
}
