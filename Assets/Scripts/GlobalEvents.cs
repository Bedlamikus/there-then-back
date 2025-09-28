using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GlobalEvents
{
    public static UnityEvent PlayerJump = new();
    public static UnityEvent<Vector2> PlayerMove = new();
    public static UnityEvent Shoot = new();
    public static UnityEvent<int> AmmoReloaded = new();  // Событие пополнения патрона (количество в магазине)
}
