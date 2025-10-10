using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("Dead Zone Settings")]
    public LayerMask spawnableLayerMask = 1; // Слои для игрока и ботов
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что это сущность из нужного слоя
        if (((1 << other.gameObject.layer) & spawnableLayerMask) != 0)
        {
            // Проверяем наличие ISpawnable интерфейса
            ISpawnable spawnable = other.GetComponent<ISpawnable>();
            if (spawnable != null)
            {
                // Уведомляем сервис автоспавна о попадании в зону смерти
                AutoSpawnService.Instance?.OnEnterDeadZone(spawnable);
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
