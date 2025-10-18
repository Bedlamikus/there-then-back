using UnityEngine;

public enum VoxelBotState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Jumping,
    Falling,
    Stuck
}

public class VoxelBotStateMachine
{
    public VoxelBotState currentState { get; private set; }
    public VoxelBotState previousState { get; private set; }
    
    private Transform botTransform;
    private Transform target;
    private VoxelBotConfig config;
    
    // State timers
    private float stateTimer;
    private float idleTimer;
    private float patrolTimer;
    
    // Patrol
    private Vector3 currentPatrolPoint;
    private bool hasPatrolPoint;
    
    public VoxelBotStateMachine(Transform botTransform, VoxelBotConfig config)
    {
        this.botTransform = botTransform;
        this.config = config;
        currentState = VoxelBotState.Idle;
        previousState = VoxelBotState.Idle;
    }
    
    public void Update()
    {
        stateTimer += Time.deltaTime;
        
        switch (currentState)
        {
            case VoxelBotState.Idle:
                UpdateIdle();
                break;
            case VoxelBotState.Patrol:
                UpdatePatrol();
                break;
            case VoxelBotState.Chase:
                UpdateChase();
                break;
            case VoxelBotState.Attack:
                UpdateAttack();
                break;
            case VoxelBotState.Jumping:
                UpdateJumping();
                break;
            case VoxelBotState.Falling:
                UpdateFalling();
                break;
            case VoxelBotState.Stuck:
                UpdateStuck();
                break;
        }
    }
    
    private void UpdateIdle()
    {
        idleTimer += Time.deltaTime;
        
        // Check for target
        if (target != null && IsTargetInRange(config.detectionRangeVoxels))
        {
            ChangeState(VoxelBotState.Chase);
            return;
        }
        
        // Try to find a patrol point while idle
        if (!hasPatrolPoint)
        {
            FindPatrolPoint();
        }
        
        // After idle time, start patrolling (if we have a patrol point)
        if (idleTimer > 2f && hasPatrolPoint)
        {
            ChangeState(VoxelBotState.Patrol);
        }
    }
    
    private void UpdatePatrol()
    {
        patrolTimer += Time.deltaTime;
        
        // Check for target
        if (target != null && IsTargetInRange(config.detectionRangeVoxels))
        {
            ChangeState(VoxelBotState.Chase);
            return;
        }
        
        // If no patrol point, find one
        if (!hasPatrolPoint)
        {
            FindPatrolPoint();
        }
        
        // If patrol point reached or timeout, find new one
        float distanceToPatrol = Vector3.Distance(botTransform.position, currentPatrolPoint);
        if (hasPatrolPoint && (distanceToPatrol < 0.5f || patrolTimer > 10f))
        {
            Debug.Log($"[VoxelBotStateMachine] Looking for new patrol point. Current: {currentPatrolPoint}, Distance: {distanceToPatrol:F2}, Timer: {patrolTimer:F1}s");
            FindPatrolPoint();
        }
    }
    
    private void UpdateChase()
    {
        // Check if target is still in range
        if (target == null || !IsTargetInRange(config.detectionRangeVoxels * 2))
        {
            ChangeState(VoxelBotState.Patrol);
            return;
        }
        
        // Check if target is in attack range
        if (IsTargetInRange(config.attackRangeVoxels))
        {
            ChangeState(VoxelBotState.Attack);
            return;
        }
    }
    
    private void UpdateAttack()
    {
        // Check if target moved out of attack range
        if (target == null || !IsTargetInRange(Mathf.RoundToInt(config.attackRangeVoxels * 1.5f)))
        {
            ChangeState(VoxelBotState.Chase);
            return;
        }
        
        // Attack logic would go here
    }
    
    private void UpdateJumping()
    {
        // Jumping state is managed by VoxelBotController
        // This state will be changed externally
    }
    
    private void UpdateFalling()
    {
        // Falling state is managed by VoxelBotController
        // This state will be changed externally
    }
    
    private void UpdateStuck()
    {
        // Try to get unstuck
        if (stateTimer > 5f)
        {
            ChangeState(VoxelBotState.Patrol);
        }
    }
    
    private void FindPatrolPoint()
    {
        Vector3 botPos = botTransform.position;
        Vector3Int botVoxel = VoxelWorld.WorldToVoxel(botPos);
        
        for (int i = 0; i < config.patrolSearchAttempts; i++)
        {
            // Random point within patrol radius
            int randomX = Random.Range(-config.patrolRadiusVoxels, config.patrolRadiusVoxels + 1);
            int randomZ = Random.Range(-config.patrolRadiusVoxels, config.patrolRadiusVoxels + 1);
            
            // Find ground level for this X,Z position
            int groundY = FindGroundLevel(botVoxel.x + randomX, botVoxel.z + randomZ);
            if (groundY == -1) continue; // No ground found
            
            Vector3Int patrolVoxel = new Vector3Int(
                botVoxel.x + randomX,
                groundY + 1, // Position above ground
                botVoxel.z + randomZ
            );
            
            // Check if this voxel is walkable
            if (IsWalkableVoxel(patrolVoxel))
            {
                currentPatrolPoint = VoxelWorld.VoxelToWorld(patrolVoxel);
                hasPatrolPoint = true;
                patrolTimer = 0f;
                Debug.Log($"[VoxelBotStateMachine] Found new patrol point: {currentPatrolPoint} (attempt {i + 1}/{config.patrolSearchAttempts})");
                return;
            }
        }
        
        // If no patrol point found, stay idle
        hasPatrolPoint = false;
        Debug.Log($"[VoxelBotStateMachine] No patrol point found after {config.patrolSearchAttempts} attempts, switching to Idle");
        ChangeState(VoxelBotState.Idle);
    }
    
    private int FindGroundLevel(int x, int z)
    {
        // Search from top to bottom for the first solid block
        for (int y = VoxelWorld.Instance.GetWorldHeight() - 1; y >= 0; y--)
        {
            Vector3Int voxel = new Vector3Int(x, y, z);
            if (VoxelWorld.IsVoxelSolid(voxel))
            {
                return y; // Found ground level
            }
        }
        return -1; // No ground found
    }
    
    private bool IsWalkableVoxel(Vector3Int voxel)
    {
        // Check if voxel is empty (can stand on it)
        if (VoxelWorld.IsVoxelSolid(voxel)) return false;
        
        // Check if there's ground below
        Vector3Int groundVoxel = new Vector3Int(voxel.x, voxel.y - 1, voxel.z);
        if (!VoxelWorld.IsVoxelSolid(groundVoxel)) return false;
        
        // Check if there's enough space above for bot height
        for (int i = 1; i <= config.botHeightVoxels; i++)
        {
            Vector3Int checkVoxel = new Vector3Int(voxel.x, voxel.y + i, voxel.z);
            if (VoxelWorld.IsVoxelSolid(checkVoxel)) return false;
        }
        
        return true;
    }
    
    private bool IsTargetInRange(int rangeVoxels)
    {
        if (target == null) return false;
        
        Vector3Int botVoxel = VoxelWorld.WorldToVoxel(botTransform.position);
        Vector3Int targetVoxel = VoxelWorld.WorldToVoxel(target.position);
        
        int distance = Mathf.Max(
            Mathf.Abs(targetVoxel.x - botVoxel.x),
            Mathf.Abs(targetVoxel.z - botVoxel.z)
        );
        
        return distance <= rangeVoxels;
    }
    
    public void ChangeState(VoxelBotState newState)
    {
        if (currentState == newState) return;
        
        previousState = currentState;
        currentState = newState;
        stateTimer = 0f;
        
        // Reset state-specific timers
        if (newState == VoxelBotState.Idle)
        {
            idleTimer = 0f;
        }
        else if (newState == VoxelBotState.Patrol)
        {
            patrolTimer = 0f;
            hasPatrolPoint = false;
        }
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public Vector3 GetCurrentTarget()
    {
        switch (currentState)
        {
            case VoxelBotState.Patrol:
                return hasPatrolPoint ? currentPatrolPoint : botTransform.position;
            case VoxelBotState.Chase:
            case VoxelBotState.Attack:
                return target != null ? target.position : botTransform.position;
            default:
                return botTransform.position;
        }
    }
    
    public Vector3 GetCurrentPatrolPoint()
    {
        return hasPatrolPoint ? currentPatrolPoint : Vector3.zero;
    }
    
    public bool ShouldJump()
    {
        // Jump logic will be handled by pathfinding
        return false;
    }
}
