using UnityEngine;

public class VoxelBotAI : MonoBehaviour
{
    [Header("Configuration")]
    public VoxelBotConfig config;
    
    [Header("Components")]
    public VoxelBotController voxelController;
    public Animator animator;
    
    // AI Systems
    private VoxelBotStateMachine stateMachine;
    private VoxelPathfindingService pathfindingService;
    
    // Target
    private Transform target;
    
    void Start()
    {
        InitializeAI();
    }
    
    void Update()
    {
        UpdateAI();
    }
    
    private void InitializeAI()
    {
        if (config == null)
        {
            Debug.LogError($"[VoxelBotAI] VoxelBotConfig not assigned for {gameObject.name}! Please assign config in inspector.");
            return;
        }
        
        if (voxelController == null)
        {
            voxelController = GetComponent<VoxelBotController>();
            if (voxelController == null)
            {
                Debug.LogError($"[VoxelBotAI] VoxelBotController not found on {gameObject.name}!");
                return;
            }
        }
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Initialize AI systems
        stateMachine = new VoxelBotStateMachine(transform, config);
        pathfindingService = new VoxelPathfindingService(transform, config);
        
        // Enable AI control
        voxelController.EnableAIControl();
        
        Debug.Log($"[VoxelBotAI] Initialized AI for {gameObject.name}");
    }
    
    private void UpdateAI()
    {
        if (stateMachine == null || pathfindingService == null) return;
        
        // Update state machine
        stateMachine.Update();
        
        // Get current target from state machine
        Vector3 currentTarget = stateMachine.GetCurrentTarget();
        
        // Handle different states
        switch (stateMachine.currentState)
        {
            case VoxelBotState.Idle:
                Debug.Log($"[VoxelBotAI] Handling Idle state, patrol point: {stateMachine.GetCurrentPatrolPoint()}");
                HandleIdleState();
                break;
            case VoxelBotState.Patrol:
                Debug.Log($"[VoxelBotAI] Handling Patrol state, target: {currentTarget}");
                HandlePatrolState(currentTarget);
                break;
            case VoxelBotState.Chase:
                HandleChaseState(currentTarget);
                break;
            case VoxelBotState.Attack:
                HandleAttackState(currentTarget);
                break;
            case VoxelBotState.Jumping:
                HandleJumpingState();
                break;
            case VoxelBotState.Falling:
                HandleFallingState();
                break;
            case VoxelBotState.Stuck:
                HandleStuckState();
                break;
        }
    }
    
    private void HandleIdleState()
    {
        // Stop movement
        voxelController.MoveToTarget(Vector3.zero, false);
        
        // Check if we have a patrol point available
        Vector3 currentPatrolTarget = stateMachine.GetCurrentPatrolPoint();
        if (currentPatrolTarget != Vector3.zero)
        {
            Debug.Log($"[VoxelBotAI] Found patrol point in Idle state: {currentPatrolTarget}, switching to Patrol");
            stateMachine.ChangeState(VoxelBotState.Patrol);
        }
    }
    
    private void HandlePatrolState(Vector3 patrolTarget)
    {
        Debug.Log($"[VoxelBotAI] HandlePatrolState called with target: {patrolTarget}");
        
        // Always use the current patrol point from state machine
        Vector3 currentPatrolTarget = stateMachine.GetCurrentPatrolPoint();
        Debug.Log($"[VoxelBotAI] Current patrol point from state machine: {currentPatrolTarget}");
        
        if (currentPatrolTarget == Vector3.zero)
        {
            Debug.Log($"[VoxelBotAI] No patrol point available, staying idle");
            stateMachine.ChangeState(VoxelBotState.Idle);
            return;
        }
        
        // Check if patrol target changed
        if (Vector3.Distance(currentPatrolTarget, patrolTarget) > 0.1f)
        {
            Debug.Log($"[VoxelBotAI] Patrol target changed from {patrolTarget} to {currentPatrolTarget}, rebuilding path");
            patrolTarget = currentPatrolTarget;
            pathfindingService.ClearPath(); // Clear old path
        }
        
        Debug.Log($"[VoxelBotAI] Checking pathfinding service. HasPath: {pathfindingService.HasPath()}");
        
        // Find path to patrol point
        if (!pathfindingService.HasPath())
        {
            if (pathfindingService.FindPath(patrolTarget))
            {
                Debug.Log($"[VoxelBotAI] Found patrol path to {patrolTarget}");
            }
            else
            {
                Debug.Log($"[VoxelBotAI] No patrol path found, staying idle");
                stateMachine.ChangeState(VoxelBotState.Idle);
                return;
            }
        }
        
        // Move along path
        Vector3 moveDirection = pathfindingService.GetNextMoveDirection();
        bool shouldJump = pathfindingService.ShouldJump();
        
        if (moveDirection.sqrMagnitude > 0.1f)
        {
            voxelController.MoveToTarget(moveDirection, shouldJump);
        }
        else
        {
            // Path completed, let state machine handle finding new patrol point
            Debug.Log($"[VoxelBotAI] Path completed, clearing path");
            pathfindingService.ClearPath();
        }
    }
    
    private void HandleChaseState(Vector3 chaseTarget)
    {
        // Find path to target
        if (!pathfindingService.HasPath())
        {
            if (pathfindingService.FindPath(chaseTarget))
            {
                Debug.Log($"[VoxelBotAI] Found chase path to {chaseTarget}");
            }
            else
            {
                Debug.Log($"[VoxelBotAI] No chase path found, going to patrol");
                stateMachine.ChangeState(VoxelBotState.Patrol);
                return;
            }
        }
        
        // Move along path
        Vector3 moveDirection = pathfindingService.GetNextMoveDirection();
        bool shouldJump = pathfindingService.ShouldJump();
        
        if (moveDirection.sqrMagnitude > 0.1f)
        {
            voxelController.MoveToTarget(moveDirection, shouldJump);
        }
        else
        {
            // Reached target
            pathfindingService.ClearPath();
        }
    }
    
    private void HandleAttackState(Vector3 attackTarget)
    {
        // Stop movement for attack
        voxelController.MoveToTarget(Vector3.zero, false);
        
        // Face target
        Vector3 directionToTarget = (attackTarget - transform.position).normalized;
        directionToTarget.y = 0;
        
        if (directionToTarget.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, config.turnSpeed * Time.deltaTime);
        }
        
        // Attack logic would go here
        // For now, just go back to chase after a short time
        if (Vector3.Distance(transform.position, attackTarget) > config.attackRangeVoxels * 1.5f)
        {
            stateMachine.ChangeState(VoxelBotState.Chase);
        }
    }
    
    private void HandleJumpingState()
    {
        // Jumping is handled by VoxelBotController
        // This state will be changed externally when jump completes
    }
    
    private void HandleFallingState()
    {
        // Falling is handled by VoxelBotController
        // This state will be changed externally when landing
    }
    
    private void HandleStuckState()
    {
        // Try to get unstuck
        voxelController.MoveToTarget(Vector3.zero, false);
        
        // Clear current path
        pathfindingService.ClearPath();
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (stateMachine != null)
        {
            stateMachine.SetTarget(newTarget);
        }
    }
    
    public void ResetAI()
    {
        if (stateMachine != null)
        {
            stateMachine.ChangeState(VoxelBotState.Idle);
        }
        
        if (pathfindingService != null)
        {
            pathfindingService.ClearPath();
        }
        
        voxelController.DisableAIControl();
    }
    
    public void InitializeWithTarget(Transform targetTransform)
    {
        SetTarget(targetTransform);
    }
    
    // Public getters
    public VoxelBotState CurrentState => stateMachine?.currentState ?? VoxelBotState.Idle;
    public bool HasTarget => target != null;
    public Transform Target => target;
    
    // Debug visualization
    void OnDrawGizmos()
    {
        // Draw path points as cubes
        if (pathfindingService != null && pathfindingService.HasPath())
        {
            var path = pathfindingService.GetCurrentPath();
            
            // Draw each path point as a cube
            for (int i = 0; i < path.Count; i++)
            {
                if (i == 0)
                {
                    // Start point - green cube
                    Gizmos.color = Color.green;
                }
                else if (i == path.Count - 1)
                {
                    // End point - red cube
                    Gizmos.color = Color.red;
                }
                else
                {
                    // Middle points - blue cubes
                    Gizmos.color = Color.blue;
                }
                
                Gizmos.DrawCube(path[i], Vector3.one * 0.8f);
            }
        }
        
        // Draw ground line under bot's feet
        Vector3 botPos = transform.position;
        Vector3Int botVoxel = VoxelWorld.WorldToVoxel(botPos);
        
        // Draw line showing where bot should be walking
        Gizmos.color = Color.white;
        Vector3 groundPos = new Vector3(botPos.x, botVoxel.y + 0.5f, botPos.z);
        Gizmos.DrawLine(groundPos + Vector3.left * 0.5f, groundPos + Vector3.right * 0.5f);
        Gizmos.DrawLine(groundPos + Vector3.forward * 0.5f, groundPos + Vector3.back * 0.5f);
    }
}