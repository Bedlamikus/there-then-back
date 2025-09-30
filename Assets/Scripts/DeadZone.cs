using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("Dead Zone Settings")]
    public LayerMask playerLayerMask = 1; // Слой игрока
    
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что это игрок
        if (((1 << other.gameObject.layer) & playerLayerMask) != 0)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                // Уведомляем сервис автоспавна о попадании в зону смерти
                AutoSpawnService.Instance?.OnPlayerEnterDeadZone(player);
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
