using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Структура данных для сохранения одного чанка
/// </summary>
[Serializable]
public class ChunkSaveData
{
    public int cx;
    public int cz;
    public int[] blockData;     // Плоский массив блоков (data[x,y,z] → blockData[x + y*W + z*W*H])
    public short[] hpData;      // Плоский массив HP
    
    public ChunkSaveData()
    {
        blockData = new int[0];
        hpData = new short[0];
    }
    
    public ChunkSaveData(int chunkX, int chunkZ, int[,,] data, short[,,] hp)
    {
        cx = chunkX;
        cz = chunkZ;
        
        int W = VoxelChunk16.WIDTH;
        int H = VoxelChunk16.HEIGHT;
        int D = VoxelChunk16.DEPTH;
        
        blockData = new int[W * H * D];
        hpData = new short[W * H * D];
        
        // Конвертируем 3D массив в плоский для сериализации
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                for (int z = 0; z < D; z++)
                {
                    int index = x + y * W + z * W * H;
                    blockData[index] = data[x, y, z];
                    hpData[index] = hp[x, y, z];
                }
            }
        }
    }
    
    /// <summary>
    /// Восстанавливает 3D массивы из плоских
    /// </summary>
    public void ExtractArrays(out int[,,] data, out short[,,] hp)
    {
        int W = VoxelChunk16.WIDTH;
        int H = VoxelChunk16.HEIGHT;
        int D = VoxelChunk16.DEPTH;
        
        data = new int[W, H, D];
        hp = new short[W, H, D];
        
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                for (int z = 0; z < D; z++)
                {
                    int index = x + y * W + z * W * H;
                    if (index < blockData.Length)
                        data[x, y, z] = blockData[index];
                    if (index < hpData.Length)
                        hp[x, y, z] = hpData[index];
                }
            }
        }
    }
}

/// <summary>
/// Данные для сохранения всего воксельного мира
/// </summary>
[Serializable]
public class VoxelWorldData
{
    public int chunksX;
    public int chunksZ;
    public List<ChunkSaveData> chunks = new List<ChunkSaveData>();
    public long saveTimestamp; // Время сохранения для отладки
    
    public VoxelWorldData()
    {
        chunks = new List<ChunkSaveData>();
        saveTimestamp = DateTime.Now.Ticks;
    }
}

