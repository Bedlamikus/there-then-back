using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Генератор каньонов для воксельного мира
/// </summary>
public class CanyonGenerator
{
    private CanyonConfig config;
    private System.Random random;
    private VoxelWorld world;
    
    // Точки пути каньона
    private List<Vector3> pathPoints = new List<Vector3>();
    
    public CanyonGenerator(CanyonConfig canyonConfig, VoxelWorld voxelWorld)
    {
        config = canyonConfig;
        world = voxelWorld;
    }
    
    /// <summary>
    /// Генерирует каньон в мире
    /// </summary>
    public void GenerateCanyon(Vector3 startPosition, int seed)
    {
        random = new System.Random(seed);
        pathPoints.Clear();
        
        Debug.Log($"CanyonGenerator: Начинаем генерацию каньона от {startPosition}");
        
        // 1. Генерируем главный путь каньона
        int mainLength = random.Next(config.minLength, config.maxLength + 1);
        float mainDirection = (float)(random.NextDouble() * 360f); // Случайное направление
        GenerateCanyonPath(startPosition, mainDirection, mainLength, pathPoints);
        
        Debug.Log($"CanyonGenerator: Сгенерирован главный путь ({pathPoints.Count} точек)");
        
        // 2. Генерируем ответвления
        int branchCount = random.Next(config.minBranches, config.maxBranches + 1);
        GenerateBranches(pathPoints, branchCount);
        
        Debug.Log($"CanyonGenerator: Сгенерировано {branchCount} ответвлений");
        
        // 3. Вырезаем каньон по всем точкам пути
        CarveCanyonAlongPath(pathPoints);
        
        Debug.Log("CanyonGenerator: Каньон сгенерирован");
    }
    
    /// <summary>
    /// Генерирует извилистый путь каньона
    /// </summary>
    void GenerateCanyonPath(Vector3 startPos, float direction, int length, List<Vector3> points)
    {
        Vector3 currentPos = startPos;
        float currentAngle = direction;
        
        points.Add(currentPos);
        
        for (int i = 0; i < length; i++)
        {
            // Добавляем извилистость через шум
            float noise = Mathf.PerlinNoise(i / config.pathNoiseScale, (float)random.NextDouble());
            float angleChange = (noise - 0.5f) * config.pathCurvature * 30f; // ±15 градусов при curvature=1
            
            currentAngle += angleChange;
            
            // Вычисляем следующую точку
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));
            currentPos += offset;
            
            // Проверка границ мира
            if (currentPos.x < 5 || currentPos.x >= world.GetWorldWidth() - 5 ||
                currentPos.z < 5 || currentPos.z >= world.GetWorldDepth() - 5)
            {
                break; // Вышли за границы
            }
            
            points.Add(currentPos);
        }
    }
    
    /// <summary>
    /// Генерирует ответвления каньона
    /// </summary>
    void GenerateBranches(List<Vector3> mainPath, int branchCount)
    {
        if (branchCount == 0 || mainPath.Count < 10) return;
        
        for (int i = 0; i < branchCount; i++)
        {
            // Выбираем случайную точку на главном пути (не слишком близко к концам)
            int branchStartIndex = random.Next(mainPath.Count / 4, mainPath.Count * 3 / 4);
            Vector3 branchStart = mainPath[branchStartIndex];
            
            // Вычисляем направление главного пути в этой точке
            Vector3 pathDirection = Vector3.zero;
            if (branchStartIndex > 0 && branchStartIndex < mainPath.Count - 1)
            {
                pathDirection = (mainPath[branchStartIndex + 1] - mainPath[branchStartIndex - 1]).normalized;
            }
            
            // Направление ответвления (перпендикулярно или под углом)
            float pathAngle = Mathf.Atan2(pathDirection.z, pathDirection.x) * Mathf.Rad2Deg;
            float branchDirection = pathAngle + (random.NextDouble() < 0.5 ? 1 : -1) * config.branchAngle;
            branchDirection += (float)(random.NextDouble() * 40f - 20f); // ±20° вариация
            
            // Длина ответвления
            float lengthRatio = (float)(random.NextDouble() * (config.maxBranchLengthRatio - config.minBranchLengthRatio) + config.minBranchLengthRatio);
            int branchLength = Mathf.RoundToInt(mainPath.Count * lengthRatio);
            
            // Генерируем путь ответвления
            List<Vector3> branchPoints = new List<Vector3>();
            GenerateCanyonPath(branchStart, branchDirection, branchLength, branchPoints);
            
            // Добавляем точки ответвления к общему списку
            pathPoints.AddRange(branchPoints);
        }
    }
    
    /// <summary>
    /// Вырезает каньон вдоль пути
    /// </summary>
    void CarveCanyonAlongPath(List<Vector3> points)
    {
        HashSet<(int cx, int cz)> affectedChunks = new HashSet<(int, int)>();
        
        foreach (Vector3 point in points)
        {
            int centerX = Mathf.RoundToInt(point.x);
            int centerZ = Mathf.RoundToInt(point.z);
            
            // Находим высоту поверхности в этой точке
            int surfaceY = FindSurfaceY(centerX, centerZ);
            if (surfaceY < config.minGenerationHeight)
                continue; // Слишком низко
            
            // Вычисляем глубину для этой точки
            int depth = random.Next(config.minDepth, config.maxDepth + 1);
            int bottomY = Mathf.Max(surfaceY - depth, config.minBottomHeight);
            
            // Вырезаем каньон в этой точке
            CarveAtPoint(centerX, centerZ, surfaceY, bottomY, affectedChunks);
        }
        
        // Перестраиваем затронутые чанки
        RebuildAffectedChunks(affectedChunks);
        
        Debug.Log($"CanyonGenerator: Затронуто {affectedChunks.Count} чанков");
    }
    
    /// <summary>
    /// Вырезает каньон в конкретной точке
    /// </summary>
    void CarveAtPoint(int centerX, int centerZ, int surfaceY, int bottomY, HashSet<(int cx, int cz)> affectedChunks)
    {
        int maxWidth = Mathf.Max(config.bottomWidth, config.topWidth);
        
        for (int x = -maxWidth; x <= maxWidth; x++)
        {
            for (int z = -maxWidth; z <= maxWidth; z++)
            {
                int worldX = centerX + x;
                int worldZ = centerZ + z;
                
                // Проверка границ
                if (worldX < 0 || worldZ < 0 || worldX >= world.GetWorldWidth() || worldZ >= world.GetWorldDepth())
                    continue;
                
                float horizontalDist = Mathf.Sqrt(x * x + z * z);
                
                // Проходим по вертикали от дна до поверхности
                for (int y = bottomY; y <= surfaceY; y++)
                {
                    // Вычисляем ширину на текущей высоте (расширение к верху)
                    float heightRatio = (float)(y - bottomY) / Mathf.Max(1, surfaceY - bottomY);
                    float currentWidth = Mathf.Lerp(config.bottomWidth, config.topWidth, heightRatio);
                    
                    // Добавляем неровности на склонах
                    if (config.addSlopeRoughness)
                    {
                        float roughness = GetNoiseValue(worldX, y, worldZ, config.slopeRoughnessScale);
                        currentWidth += (roughness - 0.5f) * config.slopeRoughnessIntensity * config.topWidth;
                    }
                    
                    if (horizontalDist <= currentWidth)
                    {
                        int blockType = world.GetBlockType(worldX, y, worldZ);
                        
                        // Не изменяем защищенные блоки (ресурсы)
                        if (IsProtectedBlock(blockType))
                            continue;
                        
                        // Удаляем блок (создаем воздух)
                        RemoveBlock(worldX, y, worldZ, affectedChunks);
                    }
                }
                
                // Терраформирование: земля на склонах
                if (config.placeDirtOnSlopes && horizontalDist > config.bottomWidth && horizontalDist <= config.topWidth)
                {
                    PlaceSlopeDirt(worldX, worldZ, bottomY, surfaceY, affectedChunks);
                }
                
                // Терраформирование: камни на дне
                if (config.placeRocksOnBottom && horizontalDist <= config.bottomWidth)
                {
                    PlaceBottomRocks(worldX, worldZ, bottomY, affectedChunks);
                }
            }
        }
    }
    
    /// <summary>
    /// Размещает землю на склонах
    /// </summary>
    void PlaceSlopeDirt(int worldX, int worldZ, int bottomY, int topY, HashSet<(int cx, int cz)> affectedChunks)
    {
        // Находим первый твердый блок снизу вверх
        for (int y = bottomY; y <= topY; y++)
        {
            int blockType = world.GetBlockType(worldX, y, worldZ);
            
            if (blockType != -1) // Нашли твердый блок
            {
                // Проверяем что над ним воздух
                int blockAbove = world.GetBlockType(worldX, y + 1, worldZ);
                if (blockAbove == -1)
                {
                    // Это поверхность склона - размещаем землю
                    for (int d = 0; d < config.slopeDirtThickness; d++)
                    {
                        int targetY = y - d;
                        if (targetY < bottomY) break;
                        
                        int targetBlock = world.GetBlockType(worldX, targetY, worldZ);
                        if (!IsProtectedBlock(targetBlock) && targetBlock != -1)
                        {
                            SetBlock(worldX, targetY, worldZ, 1, affectedChunks); // 1 = Земля
                        }
                    }
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// Размещает камни на дне каньона
    /// </summary>
    void PlaceBottomRocks(int worldX, int worldZ, int bottomY, HashSet<(int cx, int cz)> affectedChunks)
    {
        // Проверяем плотность камней
        if (random.NextDouble() > config.bottomRockDensity)
            return;
        
        // Находим дно (первый твердый блок)
        for (int y = bottomY; y <= bottomY + 10; y++)
        {
            if (y >= VoxelChunk16.HEIGHT) break;
            
            int blockType = world.GetBlockType(worldX, y, worldZ);
            
            if (blockType != -1) // Нашли твердый блок
            {
                // Проверяем что над ним воздух (это дно каньона)
                int blockAbove = world.GetBlockType(worldX, y + 1, worldZ);
                if (blockAbove == -1)
                {
                    // Размещаем камни
                    for (int h = 0; h < config.bottomRockHeight; h++)
                    {
                        int targetY = y - h;
                        if (targetY < bottomY - 5) break;
                        
                        int targetBlock = world.GetBlockType(worldX, targetY, worldZ);
                        if (!IsProtectedBlock(targetBlock) && targetBlock != -1)
                        {
                            SetBlock(worldX, targetY, worldZ, 2, affectedChunks); // 2 = Камень
                        }
                    }
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// Удаляет блок в мире
    /// </summary>
    void RemoveBlock(int wx, int wy, int wz, HashSet<(int cx, int cz)> affectedChunks)
    {
        if (world.SetBlockForced(wx, wy, wz, -1, false)) // -1 = воздух
        {
            int cxi = wx / VoxelChunk16.WIDTH;
            int czi = wz / VoxelChunk16.DEPTH;
            affectedChunks.Add((cxi, czi));
        }
    }
    
    /// <summary>
    /// Устанавливает блок в мире (перезаписывает существующие)
    /// </summary>
    void SetBlock(int wx, int wy, int wz, int blockType, HashSet<(int cx, int cz)> affectedChunks)
    {
        if (world.SetBlockForced(wx, wy, wz, blockType, false))
        {
            int cxi = wx / VoxelChunk16.WIDTH;
            int czi = wz / VoxelChunk16.DEPTH;
            affectedChunks.Add((cxi, czi));
        }
    }
    
    /// <summary>
    /// Находит высоту поверхности
    /// </summary>
    int FindSurfaceY(int worldX, int worldZ)
    {
        for (int y = VoxelChunk16.HEIGHT - 1; y >= 0; y--)
        {
            if (world.HasBlockAt(worldX, y, worldZ))
                return y;
        }
        return -1;
    }
    
    /// <summary>
    /// Проверяет, защищен ли блок от изменения
    /// </summary>
    bool IsProtectedBlock(int blockType)
    {
        if (blockType == -1) return false; // Воздух не защищен
        
        foreach (int protectedType in config.protectedBlockTypes)
        {
            if (blockType == protectedType)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Получает значение шума для позиции
    /// </summary>
    float GetNoiseValue(int x, int y, int z, float scale)
    {
        float nx = x / scale;
        float ny = y / scale;
        float nz = z / scale;
        
        // Комбинируем два Perlin noise для псевдо-3D
        float n1 = Mathf.PerlinNoise(nx, ny);
        float n2 = Mathf.PerlinNoise(ny, nz);
        
        return (n1 + n2) * 0.5f;
    }
    
    /// <summary>
    /// Перестраивает затронутые чанки
    /// </summary>
    void RebuildAffectedChunks(HashSet<(int cx, int cz)> affectedChunks)
    {
        foreach (var chunkKey in affectedChunks)
        {
            world.RebuildChunk(chunkKey.cx, chunkKey.cz);
        }
    }
    
    /// <summary>
    /// Получает список всех точек пути (для отладки)
    /// </summary>
    public List<Vector3> GetPathPoints()
    {
        return new List<Vector3>(pathPoints);
    }
}

