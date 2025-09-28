using UnityEngine;
using UnityEngine.EventSystems;

public class TurretJoystick : FloatingJoystick
{
    private Vector2 lastTurretDirection = Vector2.zero;

    void Update()
    {
        // Отправляем событие вращения башни в Update для стабильности
        Vector2 currentDirection = Direction;
        if (currentDirection != lastTurretDirection)
        {
            GlobalEvents.TurretRotation.Invoke(currentDirection);
            lastTurretDirection = currentDirection;
        }
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        lastTurretDirection = Vector2.zero; // Сбрасываем для корректной отправки события остановки
        
        // Сразу отправляем событие остановки вращения
        GlobalEvents.TurretRotation.Invoke(Vector2.zero);
    }
}
