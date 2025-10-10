using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GlobalEvents
{
    public static UnityEvent PlayerJump = new();
    public static UnityEvent<Vector2> PlayerMove = new();
    public static UnityEvent Shoot = new();  // DEPRECATED - используйте ShootPressed/ShootReleased
    public static UnityEvent ShootPressed = new();   // Кнопка стрельбы нажата
    public static UnityEvent ShootReleased = new();  // Кнопка стрельбы отпущена
    public static UnityEvent<int> AmmoReloaded = new();  // Событие пополнения патрона (количество в магазине)
    public static UnityEvent<Vector3> ShootPosition = new();  // Позиция для прицеливания башни
    public static UnityEvent<Vector2> TurretRotation = new();  // Вращение башни от джойстика
    public static UnityEvent<GameObject> ProjectileSelected = new();  // Выбран префаб снаряда
    public static UnityEvent<Vector3> CameraAimPoint = new();  // Точка прицеливания в центре экрана (мировые координаты)
}
