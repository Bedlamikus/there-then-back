using UnityEngine;
using System;

/// <summary>
/// Данные для сохранения одного чанка (переработанная версия)
/// Каждый чанк сохраняется отдельно для оптимизации
/// </summary>
[Serializable]
public class SingleChunkData
{
    public int cx;
    public int cz;
    public int[] blockData;     // Плоский массив блоков
    public short[] hpData;      // Плоский массив HP
    public long lastModified;   // Время последнего изменения
    
    public SingleChunkData()
    {
        blockData = new int[0];
        hpData = new short[0];
        lastModified = DateTime.Now.Ticks;
    }
    
    /// <summary>
    /// Упаковка 3D массивов в плоские для сериализации
    /// </summary>
    public void PackData(int chunkX, int chunkZ, int[,,] data, short[,,] hp)
    {
        cx = chunkX;
        cz = chunkZ;
        
        int W = VoxelChunk16.WIDTH;
        int H = VoxelChunk16.HEIGHT;
        int D = VoxelChunk16.DEPTH;
        
        blockData = new int[W * H * D];
        hpData = new short[W * H * D];
        
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
        
        lastModified = DateTime.Now.Ticks;
    }
    
    /// <summary>
    /// Распаковка плоских массивов в 3D
    /// </summary>
    public void UnpackData(out int[,,] data, out short[,,] hp)
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
/// Глобальные настройки для первого запуска
/// </summary>
[Serializable]
public class GameSettingsData
{
    public bool isFirstRun = true;
    public long firstRunTimestamp;
    public int worldChunksX;
    public int worldChunksZ;
    
    public GameSettingsData()
    {
        isFirstRun = true;
        firstRunTimestamp = DateTime.Now.Ticks;
    }
}

