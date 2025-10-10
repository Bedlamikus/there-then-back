using UnityEngine;
using UnityEngine.EventSystems;

public class ShootButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // Старое событие для совместимости
        GlobalEvents.Shoot.Invoke();
        
        // Новое событие - кнопка нажата
        GlobalEvents.ShootPressed.Invoke();
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // Кнопка отпущена
        GlobalEvents.ShootReleased.Invoke();
    }
}
