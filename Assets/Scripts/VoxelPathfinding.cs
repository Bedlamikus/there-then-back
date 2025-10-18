using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Узел для A* поиска пути
/// </summary>
public class VoxelNode
{
    public Vector3Int position;
    public float gCost; // Стоимость от старта
    public float hCost; // Эвристическая стоимость до цели
    public float fCost => gCost + hCost; // Общая стоимость
    public VoxelNode parent;
    public bool isWalkable;
    
    public VoxelNode(Vector3Int pos)
    {
        position = pos;
        gCost = 0;
        hCost = 0;
        parent = null;
        isWalkable = true;
    }
}

/// <summary>
/// Система поиска пути по вокселям
/// </summary>
public class VoxelPathfinding
{
    private VoxelBotConfig config;
    
    public VoxelPathfinding(VoxelBotConfig botConfig)
    {
        config = botConfig;
    }
    
    /// <summary>
    /// Находит путь от старта до цели
    /// </summary>
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int target)
    {
        if (start == target)
        {
            return new List<Vector3Int> { target };
        }
        
        // Проверяем что цель достижима
        if (!IsPositionWalkable(target))
        {
            // Ищем ближайшую достижимую позицию
            target = FindNearestWalkablePosition(target, start);
        }
        
        List<VoxelNode> openSet = new List<VoxelNode>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        
        VoxelNode startNode = new VoxelNode(start);
        startNode.gCost = 0;
        startNode.hCost = GetDistance(start, target);
        openSet.Add(startNode);
        
        while (openSet.Count > 0)
        {
            // Находим узел с наименьшей fCost
            VoxelNode currentNode = openSet.OrderBy(n => n.fCost).First();
            openSet.Remove(currentNode);
            closedSet.Add(currentNode.position);
            
            // Проверяем достижение цели
            if (currentNode.position == target)
            {
                return RetracePath(startNode, currentNode);
            }
            
            // Проверяем соседние узлы
            foreach (Vector3Int neighbor in GetNeighbors(currentNode.position))
            {
                if (closedSet.Contains(neighbor))
                    continue;
                
                if (!IsPositionWalkable(neighbor))
                    continue;
                
                float tentativeGCost = currentNode.gCost + GetDistance(currentNode.position, neighbor);
                
                VoxelNode neighborNode = openSet.FirstOrDefault(n => n.position == neighbor);
                
                if (neighborNode == null)
                {
                    neighborNode = new VoxelNode(neighbor);
                    openSet.Add(neighborNode);
                }
                else if (tentativeGCost >= neighborNode.gCost)
                {
                    continue;
                }
                
                neighborNode.parent = currentNode;
                neighborNode.gCost = tentativeGCost;
                neighborNode.hCost = GetDistance(neighbor, target);
            }
        }
        
        // Путь не найден, возвращаем пустой список
        return new List<Vector3Int>();
    }
    
    /// <summary>
    /// Восстанавливает путь от старта до цели
    /// </summary>
    private List<Vector3Int> RetracePath(VoxelNode startNode, VoxelNode endNode)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        VoxelNode currentNode = endNode;
        
        while (currentNode != startNode)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }
        
        path.Reverse();
        return path;
    }
    
    /// <summary>
    /// Получает соседние воксели
    /// </summary>
    private List<Vector3Int> GetNeighbors(Vector3Int position)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();
        
        // Горизонтальные соседи
        neighbors.Add(position + Vector3Int.right);
        neighbors.Add(position + Vector3Int.left);
        neighbors.Add(position + Vector3Int.forward);
        neighbors.Add(position + Vector3Int.back);
        
        // Диагональные соседи
        neighbors.Add(position + Vector3Int.right + Vector3Int.forward);
        neighbors.Add(position + Vector3Int.right + Vector3Int.back);
        neighbors.Add(position + Vector3Int.left + Vector3Int.forward);
        neighbors.Add(position + Vector3Int.left + Vector3Int.back);
        
        // Вертикальные соседи (для прыжков)
        neighbors.Add(position + Vector3Int.up);
        neighbors.Add(position + Vector3Int.down);
        
        return neighbors;
    }
    
    /// <summary>
    /// Проверяет можно ли ходить по этой позиции
    /// </summary>
    private bool IsPositionWalkable(Vector3Int position)
    {
        // Проверяем что под нами есть опора
        Vector3Int below = position + Vector3Int.down;
        if (!VoxelWorld.IsVoxelSolid(below))
        {
            return false;
        }
        
        // Проверяем что на нашей высоте нет препятствий
        if (VoxelWorld.IsVoxelSolid(position))
        {
            return false;
        }
        
        // Проверяем что над нами есть место для бота
        Vector3Int above = position + Vector3Int.up;
        if (VoxelWorld.IsVoxelSolid(above))
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Находит ближайшую достижимую позицию
    /// </summary>
    private Vector3Int FindNearestWalkablePosition(Vector3Int target, Vector3Int start)
    {
        // Ищем в радиусе вокруг цели
        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    Vector3Int checkPos = target + new Vector3Int(x, 0, z);
                    if (IsPositionWalkable(checkPos))
                    {
                        return checkPos;
                    }
                }
            }
        }
        
        // Если ничего не найдено, возвращаем стартовую позицию
        return start;
    }
    
    /// <summary>
    /// Вычисляет расстояние между двумя позициями
    /// </summary>
    private float GetDistance(Vector3Int a, Vector3Int b)
    {
        return Vector3Int.Distance(a, b);
    }
    
    /// <summary>
    /// Проверяет можно ли прыгнуть на позицию
    /// </summary>
    public bool CanJumpTo(Vector3Int from, Vector3Int to)
    {
        float distance = Vector3Int.Distance(from, to);
        
        // Проверяем что прыжок не слишком далеко
        if (distance > config.jumpDistanceVoxels)
        {
            return false;
        }
        
        // Проверяем что цель достижима
        if (!IsPositionWalkable(to))
        {
            return false;
        }
        
        // Проверяем что нет препятствий на пути
        Vector3 direction = ((Vector3)(to - from)).normalized;
        for (float t = 0; t <= 1; t += 0.1f)
        {
            Vector3Int checkPos = Vector3Int.RoundToInt(Vector3.Lerp(from, to, t));
            if (VoxelWorld.IsVoxelSolid(checkPos))
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Проверяет видимость между двумя позициями
    /// </summary>
    public bool HasLineOfSight(Vector3Int from, Vector3Int to)
    {
        Vector3 direction = ((Vector3)(to - from)).normalized;
        float distance = Vector3Int.Distance(from, to);
        
        for (float t = 0; t <= distance; t += 0.5f)
        {
            Vector3Int checkPos = Vector3Int.RoundToInt(Vector3.Lerp(from, to, t));
            if (VoxelWorld.IsVoxelSolid(checkPos))
            {
                return false;
            }
        }
        
        return true;
    }
}
