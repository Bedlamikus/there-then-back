using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Генератор процедурных деревьев для воксельного мира
/// </summary>
public class TreeGenerator
{
    private TreeConfig config;
    private System.Random random;
    private Vector3 treeOrigin; // Позиция основания дерева
    
    // Список блоков для генерации (позиция, тип)
    private List<(Vector3Int pos, int type)> blocksToPlace = new List<(Vector3Int, int)>();
    
    public TreeGenerator(TreeConfig treeConfig)
    {
        config = treeConfig;
    }
    
    /// <summary>
    /// Генерирует дерево и возвращает список блоков для размещения
    /// </summary>
    public List<(Vector3Int pos, int type)> GenerateTree(Vector3Int position, int seed)
    {
        random = new System.Random(seed);
        treeOrigin = new Vector3(position.x, position.y, position.z);
        blocksToPlace.Clear();
        
        // 1. Генерируем ствол
        int trunkHeight = random.Next(config.minTrunkHeight, config.maxTrunkHeight + 1);
        GenerateTrunk(position, trunkHeight);
        
        // 2. Генерируем ветки
        int branchCount = random.Next(config.minBranches, config.maxBranches + 1);
        GenerateBranches(position, trunkHeight, branchCount);
        
        // 3. Генерируем основную крону на вершине
        Vector3Int crownCenter = position + new Vector3Int(0, trunkHeight, 0);
        GenerateCrown(crownCenter, config.crownRadius, config.crownHeight, config.crownElongation);
        
        return new List<(Vector3Int, int)>(blocksToPlace);
    }
    
    /// <summary>
    /// Генерирует вертикальный ствол (один блок в ширину)
    /// </summary>
    void GenerateTrunk(Vector3Int basePosition, int height)
    {
        // Ствол - один центральный блок на каждом уровне
        for (int y = 0; y < height; y++)
        {
            Vector3Int blockPos = basePosition + new Vector3Int(0, y, 0);
            blocksToPlace.Add((blockPos, config.woodBlockType));
        }
    }
    
    /// <summary>
    /// Генерирует ветки дерева
    /// </summary>
    void GenerateBranches(Vector3Int basePosition, int trunkHeight, int branchCount)
    {
        if (branchCount == 0) return;
        
        // Высота начала веток
        int startHeight = Mathf.RoundToInt(trunkHeight * config.branchStartHeight);
        
        // Распределяем ветки по спирали вокруг ствола
        float angleStep = 360f / branchCount;
        
        for (int i = 0; i < branchCount; i++)
        {
            // Угол в горизонтальной плоскости
            float horizontalAngle = i * angleStep + RandomFloat(-20f, 20f);
            
            // Высота ветки (с небольшой вариацией)
            int branchY = startHeight + random.Next(0, trunkHeight - startHeight);
            
            // Длина ветки
            int branchLength = random.Next(config.minBranchLength, config.maxBranchLength + 1);
            
            // Генерируем ветку
            Vector3Int branchStart = basePosition + new Vector3Int(0, branchY, 0);
            GenerateBranch(branchStart, horizontalAngle, config.branchAngle, branchLength);
        }
    }
    
    /// <summary>
    /// Генерирует одну ветку
    /// </summary>
    void GenerateBranch(Vector3Int start, float horizontalAngle, float verticalAngle, int length)
    {
        // Направление ветки
        float radH = horizontalAngle * Mathf.Deg2Rad;
        float radV = verticalAngle * Mathf.Deg2Rad;
        
        Vector3 direction = new Vector3(
            Mathf.Cos(radH) * Mathf.Cos(radV),
            Mathf.Sin(radV),
            Mathf.Sin(radH) * Mathf.Cos(radV)
        ).normalized;
        
        Vector3 currentPos = new Vector3(start.x, start.y, start.z);
        
        for (int i = 0; i < length; i++)
        {
            // Добавляем небольшое случайное отклонение
            Vector3 randomOffset = new Vector3(
                RandomFloat(-0.2f, 0.2f),
                RandomFloat(-0.1f, 0.3f), // Немного вверх
                RandomFloat(-0.2f, 0.2f)
            );
            
            currentPos += direction + randomOffset;
            Vector3Int blockPos = new Vector3Int(
                Mathf.RoundToInt(currentPos.x),
                Mathf.RoundToInt(currentPos.y),
                Mathf.RoundToInt(currentPos.z)
            );
            
            blocksToPlace.Add((blockPos, config.woodBlockType));
            
            // Суб-ветки (с вероятностью)
            if (i > length / 2 && RandomFloat(0f, 1f) < config.subBranchProbability)
            {
                float subAngleH = horizontalAngle + RandomFloat(-45f, 45f);
                float subAngleV = verticalAngle + RandomFloat(-20f, 20f);
                int subLength = random.Next(1, length / 2 + 1);
                
                GenerateBranch(blockPos, subAngleH, subAngleV, subLength);
            }
        }
        
        // Листва на конце ветки
        if (config.leavesOnBranches)
        {
            Vector3Int branchEnd = new Vector3Int(
                Mathf.RoundToInt(currentPos.x),
                Mathf.RoundToInt(currentPos.y),
                Mathf.RoundToInt(currentPos.z)
            );
            GenerateBranchCrown(branchEnd, config.branchCrownRadius);
        }
    }
    
    /// <summary>
    /// Генерирует крону на конце ветки (небольшой шар)
    /// </summary>
    void GenerateBranchCrown(Vector3Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    float distance = Mathf.Sqrt(x * x + y * y + z * z);
                    
                    if (distance <= radius)
                    {
                        Vector3Int blockPos = center + new Vector3Int(x, y, z);
                        
                        // Шум для органичности
                        if (config.useNoise)
                        {
                            float noise = GetNoise3D(blockPos);
                            float normalizedDist = distance / radius;
                            
                            // На краях шара больше вероятность пропуска
                            if (noise < config.noiseThreshold + normalizedDist * 0.3f)
                                continue;
                        }
                        
                        // Проверка плотности
                        if (RandomFloat(0f, 1f) > config.crownDensity)
                            continue;
                        
                        blocksToPlace.Add((blockPos, config.leavesBlockType));
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Генерирует основную крону дерева (может быть продолговатой)
    /// </summary>
    void GenerateCrown(Vector3Int center, int radius, int height, float elongation)
    {
        // Вычисляем вертикальное сжатие/растяжение
        float verticalScale = 1f + elongation; // 1.0 = сфера, > 1 = вытянута вверх
        
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -height; y <= height; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // Эллипсоидальная форма
                    float dx = x;
                    float dy = y / verticalScale; // Масштабируем Y для продолговатости
                    float dz = z;
                    
                    float distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                    
                    if (distance <= radius)
                    {
                        Vector3Int blockPos = center + new Vector3Int(x, y, z);
                        
                        // Применяем шум для органичности
                        if (config.useNoise)
                        {
                            float noise = GetNoise3D(blockPos);
                            float normalizedDist = distance / radius;
                            
                            // Вероятность пропуска зависит от расстояния от центра
                            float threshold = config.noiseThreshold + normalizedDist * 0.4f;
                            
                            if (noise < threshold)
                                continue;
                        }
                        
                        // Проверка плотности листвы
                        if (RandomFloat(0f, 1f) > config.crownDensity)
                            continue;
                        
                        blocksToPlace.Add((blockPos, config.leavesBlockType));
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Получить 3D шум для позиции
    /// </summary>
    float GetNoise3D(Vector3Int position)
    {
        float x = (position.x + treeOrigin.x * 100) / config.noiseScale;
        float y = (position.y + treeOrigin.y * 100) / config.noiseScale;
        float z = (position.z + treeOrigin.z * 100) / config.noiseScale;
        
        // Комбинируем несколько Perlin noise для 3D эффекта
        float noise1 = Mathf.PerlinNoise(x, y);
        float noise2 = Mathf.PerlinNoise(y, z);
        float noise3 = Mathf.PerlinNoise(z, x);
        
        return (noise1 + noise2 + noise3) / 3f;
    }
    
    /// <summary>
    /// Случайное float число
    /// </summary>
    float RandomFloat(float min, float max)
    {
        return (float)(random.NextDouble() * (max - min) + min);
    }
    
    /// <summary>
    /// Проверяет, можно ли посадить дерево в указанной позиции
    /// </summary>
    public bool CanPlaceTree(Vector3Int position, VoxelWorld world)
    {
        if (world == null || config == null)
            return false;
        
        // Проверяем, что позиция в пределах мира
        if (position.x < 0 || position.z < 0 || position.y < 1 || position.y >= VoxelChunk16.HEIGHT - config.minClearanceAbove)
            return false;
        
        if (position.x >= world.GetWorldWidth() || position.z >= world.GetWorldDepth())
            return false;
        
        // Проверяем наличие почвы под деревом
        if (config.requireSoilBelow)
        {
            int blockBelow = world.GetBlockType(position.x, position.y - 1, position.z);
            
            bool isValidSoil = false;
            foreach (int soilType in config.validSoilTypes)
            {
                if (blockBelow == soilType)
                {
                    isValidSoil = true;
                    break;
                }
            }
            
            if (!isValidSoil)
                return false;
        }
        
        // Проверяем свободное пространство над деревом
        for (int y = position.y; y < position.y + config.minClearanceAbove; y++)
        {
            if (y >= VoxelChunk16.HEIGHT)
                break;
            
            if (world.HasBlockAt(position.x, y, position.z))
                return false; // Есть препятствие
        }
        
        return true;
    }
    
    /// <summary>
    /// Получить размер дерева для предварительных проверок
    /// </summary>
    public Vector3Int GetTreeSize()
    {
        int maxWidth = Mathf.Max(config.trunkRadius, config.crownRadius) * 2 + 2;
        int maxHeight = config.maxTrunkHeight + config.crownHeight + 2;
        
        return new Vector3Int(maxWidth, maxHeight, maxWidth);
    }
}

