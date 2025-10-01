using UnityEngine;
using System;

/// <summary>
/// Данные позиции игрока для сохранения
/// </summary>
[Serializable]
public class PlayerPositionData
{
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;
    public long lastSaveTime;
    
    public PlayerPositionData()
    {
        posX = posY = posZ = 0;
        rotX = rotY = rotZ = 0;
        rotW = 1;
        lastSaveTime = DateTime.Now.Ticks;
    }
    
    public Vector3 GetPosition()
    {
        return new Vector3(posX, posY, posZ);
    }
    
    public void SetPosition(Vector3 pos)
    {
        posX = pos.x;
        posY = pos.y;
        posZ = pos.z;
        lastSaveTime = DateTime.Now.Ticks;
    }
    
    public Quaternion GetRotation()
    {
        return new Quaternion(rotX, rotY, rotZ, rotW);
    }
    
    public void SetRotation(Quaternion rot)
    {
        rotX = rot.x;
        rotY = rot.y;
        rotZ = rot.z;
        rotW = rot.w;
    }
}

