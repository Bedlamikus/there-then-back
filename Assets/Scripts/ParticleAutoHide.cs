using UnityEngine;

/// <summary>
/// Автоматически скрывает партикл после завершения проигрывания
/// Используется внутри пула для возврата партикла в неактивное состояние
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleAutoHide : MonoBehaviour
{
    private ParticleSystem ps;
    
    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }
    
    void OnEnable()
    {
        if (ps != null)
        {
            // Запускаем корутину для скрытия после завершения
            StartCoroutine(HideAfterPlay());
        }
    }
    
    /// <summary>
    /// Скрывает партикл после завершения проигрывания
    /// </summary>
    private System.Collections.IEnumerator HideAfterPlay()
    {
        // Ждем пока партикл проиграется
        yield return new WaitForSeconds(ps.main.duration + ps.main.startLifetime.constantMax);
        
        // Дополнительно проверяем что все частицы исчезли
        while (ps.IsAlive())
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Скрываем партикл
        gameObject.SetActive(false);
    }
}

