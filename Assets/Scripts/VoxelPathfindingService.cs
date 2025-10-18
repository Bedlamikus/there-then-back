using System.Collections.Generic;
using UnityEngine;

public class VoxelPathfindingService
{
    private Transform botTransform;
    private VoxelBotConfig config;
    
    private List<Vector3> currentPath;
    private int currentPathIndex;
    private bool hasPath;
    
    public VoxelPathfindingService(Transform botTransform, VoxelBotConfig config)
    {
        this.botTransform = botTransform;
        this.config = config;
        currentPath = new List<Vector3>();
        hasPath = false;
    }
    
    public bool FindPath(Vector3 targetPosition)
    {
        Vector3 startPos = botTransform.position;
        Vector3Int startVoxel = VoxelWorld.WorldToVoxel(startPos);
        Vector3Int targetVoxel = VoxelWorld.WorldToVoxel(targetPosition);
        
        // Check if target is too far
        int distance = Mathf.Max(
            Mathf.Abs(targetVoxel.x - startVoxel.x),
            Mathf.Abs(targetVoxel.z - startVoxel.z)
        );
        
        if (distance > config.maxPathfindingDistanceVoxels)
        {
            Debug.Log($"[VoxelPathfinding] Target too far: {distance} voxels (max: {config.maxPathfindingDistanceVoxels})");
            return false;
        }
        
        // Use A* pathfinding
        currentPath = FindPathAStar(startVoxel, targetVoxel);
        
        if (currentPath != null && currentPath.Count > 0)
        {
            hasPath = true;
            currentPathIndex = 0;
            Debug.Log($"[VoxelPathfinding] Found path with {currentPath.Count} points");
            return true;
        }
        
        Debug.Log($"[VoxelPathfinding] No path found to target");
        return false;
    }
    
    private List<Vector3> FindPathAStar(Vector3Int start, Vector3Int target)
    {
        List<Vector3> path = new List<Vector3>();
        
        // Simple pathfinding: try to move in straight line first
        Vector3Int current = start;
        path.Add(VoxelWorld.VoxelToWorld(current));
        
        int maxSteps = config.maxPathfindingDistanceVoxels;
        int steps = 0;
        
        while (current != target && steps < maxSteps)
        {
            Vector3Int next = GetNextVoxelTowardsTarget(current, target);
            
            if (next == current)
            {
                // Can't move towards target, try to find alternative
                next = FindAlternativeVoxel(current, target);
                if (next == current)
                {
                    // Stuck, break
                    break;
                }
            }
            
            current = next;
            path.Add(VoxelWorld.VoxelToWorld(current));
            steps++;
        }
        
        return path;
    }
    
    private Vector3Int GetNextVoxelTowardsTarget(Vector3Int current, Vector3Int target)
    {
        // First try horizontal movement towards target
        Vector3Int horizontalDirection = new Vector3Int(
            Mathf.Clamp(target.x - current.x, -1, 1),
            0, // No Y movement in horizontal step
            Mathf.Clamp(target.z - current.z, -1, 1)
        );
        
        Vector3Int horizontalNext = current + horizontalDirection;
        
        // Check if we can move horizontally
        if (CanMoveToVoxel(horizontalNext))
        {
            return horizontalNext;
        }
        
        // If horizontal movement blocked, try to go up or down
        // Try going up first (step up)
        Vector3Int upNext = new Vector3Int(current.x, current.y + 1, current.z);
        if (CanMoveToVoxel(upNext))
        {
            return upNext;
        }
        
        // Try going down (step down)
        Vector3Int downNext = new Vector3Int(current.x, current.y - 1, current.z);
        if (CanMoveToVoxel(downNext))
        {
            return downNext;
        }
        
        // Try diagonal horizontal movement (no Y change)
        if (horizontalDirection.x != 0 && horizontalDirection.z != 0)
        {
            // Try X only
            Vector3Int xOnlyNext = new Vector3Int(current.x + horizontalDirection.x, current.y, current.z);
            if (CanMoveToVoxel(xOnlyNext))
            {
                return xOnlyNext;
            }
            
            // Try Z only
            Vector3Int zOnlyNext = new Vector3Int(current.x, current.y, current.z + horizontalDirection.z);
            if (CanMoveToVoxel(zOnlyNext))
            {
                return zOnlyNext;
            }
        }
        
        return current; // Can't move
    }
    
    private Vector3Int FindAlternativeVoxel(Vector3Int current, Vector3Int target)
    {
        // Try 8 directions around current position
        Vector3Int[] directions = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
            new Vector3Int(1, 0, 1), new Vector3Int(-1, 0, 1),
            new Vector3Int(1, 0, -1), new Vector3Int(-1, 0, -1)
        };
        
        // Find direction that gets us closer to target
        Vector3Int bestDirection = Vector3Int.zero;
        float bestDistance = float.MaxValue;
        
        foreach (Vector3Int dir in directions)
        {
            Vector3Int testPos = current + dir;
            if (CanMoveToVoxel(testPos))
            {
                float distance = Vector3Int.Distance(testPos, target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = dir;
                }
            }
        }
        
        return current + bestDirection;
    }
    
    private bool CanMoveToVoxel(Vector3Int voxel)
    {
        // Check if voxel is empty (can stand on it)
        if (VoxelWorld.IsVoxelSolid(voxel)) return false;
        
        // Check if there's ground below (support for stairs)
        bool hasGround = false;
        for (int dy = 1; dy <= 2; dy++) // Check 2 blocks down for stairs
        {
            Vector3Int groundVoxel = new Vector3Int(voxel.x, voxel.y - dy, voxel.z);
            if (VoxelWorld.IsVoxelSolid(groundVoxel))
            {
                hasGround = true;
                break;
            }
        }
        
        if (!hasGround) return false;
        
        // Check if there's enough space above for bot height
        for (int i = 1; i <= config.botHeightVoxels; i++)
        {
            Vector3Int checkVoxel = new Vector3Int(voxel.x, voxel.y + i, voxel.z);
            if (VoxelWorld.IsVoxelSolid(checkVoxel)) return false;
        }
        
        // Additional check: ensure we're not too high above ground
        // Count how many empty blocks below us
        int emptyBlocksBelow = 0;
        for (int dy = 1; dy <= 10; dy++) // Check up to 10 blocks down
        {
            Vector3Int checkVoxel = new Vector3Int(voxel.x, voxel.y - dy, voxel.z);
            if (!VoxelWorld.IsVoxelSolid(checkVoxel))
            {
                emptyBlocksBelow++;
            }
            else
            {
                break; // Found ground
            }
        }
        
        // Don't allow points that are more than 3 blocks above ground
        if (emptyBlocksBelow > 3) return false;
        
        return true;
    }
    
    public Vector3 GetNextMoveDirection()
    {
        if (!hasPath || currentPathIndex >= currentPath.Count)
        {
            return Vector3.zero;
        }
        
        Vector3 currentPos = botTransform.position;
        Vector3 nextPoint = currentPath[currentPathIndex];
        
        // Check if we've reached the current point
        if (Vector3.Distance(currentPos, nextPoint) < 1.5f) // Increased distance
        {
            currentPathIndex++;
            
            // Check if we've reached the end
            if (currentPathIndex >= currentPath.Count)
            {
                hasPath = false;
                Debug.Log($"[VoxelPathfinding] Reached end of path, path completed");
                return Vector3.zero;
            }
            
            nextPoint = currentPath[currentPathIndex];
        }
        
        Vector3 direction = (nextPoint - currentPos).normalized;
        return direction;
    }
    
    public bool ShouldJump()
    {
        if (!hasPath || currentPathIndex >= currentPath.Count) return false;
        
        Vector3 currentPos = botTransform.position;
        Vector3 nextPoint = currentPath[currentPathIndex];
        
        // Check if next point is higher (need to jump)
        float heightDifference = nextPoint.y - currentPos.y;
        return heightDifference > 0.5f;
    }
    
    public Vector3 GetJumpTarget()
    {
        if (!hasPath || currentPathIndex >= currentPath.Count) return Vector3.zero;
        
        return currentPath[currentPathIndex];
    }
    
    public void ClearPath()
    {
        currentPath.Clear();
        hasPath = false;
        currentPathIndex = 0;
    }
    
    public bool HasPath()
    {
        return hasPath && currentPathIndex < currentPath.Count;
    }
    
    public List<Vector3> GetCurrentPath()
    {
        return currentPath;
    }
}
