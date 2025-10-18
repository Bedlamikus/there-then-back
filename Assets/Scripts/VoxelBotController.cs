using System.Collections.Generic;
using UnityEngine;

public class VoxelBotController : MonoBehaviour
{
    [Header("Configuration")]
    public VoxelBotConfig config;
    
    [Header("Components")]
    public Animator animator;
    
    // Movement state
    private Vector3 velocity;
    private bool isGrounded;
    private bool isJumping;
    private bool isFalling;
    
    // Jump state
    private Vector3 jumpStartPosition;
    private Vector3 jumpTargetPosition;
    private float jumpStartTime;
    private List<Vector3> jumpTrajectory;
    private int currentTrajectoryIndex;
    
    // AI control
    private Vector3 aiMoveDirection;
    private bool aiShouldJump;
    private bool isAIControlled;
    
    // Physics
    private Vector3 lastPosition;
    private bool wasGrounded = true; // Track previous ground status for debug
    
    void Start()
    {
        InitializeBot();
    }
    
    void Update()
    {
        if (config == null) return;
        
        CheckGrounded();
        ApplyGravity();
        
        if (isJumping)
        {
            HandleJump();
        }
        
        if (isAIControlled)
        {
            HandleAIMovement();
        }
        
        UpdateAnimations();
    }
    
    private void InitializeBot()
    {
        if (config == null)
        {
            Debug.LogError($"[VoxelBotController] VoxelBotConfig not assigned for {gameObject.name}! Please assign config in inspector.");
            return;
        }
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Initialize physics
        velocity = Vector3.zero;
        isGrounded = false;
        isJumping = false;
        isFalling = false;
        
        Debug.Log($"[VoxelBotController] Initialized bot: {gameObject.name}");
    }
    
    private void CheckGrounded()
    {
        // Check ground every frame for better physics
        
        Vector3 botPosition = transform.position;
        Vector3Int botVoxel = VoxelWorld.WorldToVoxel(botPosition);
        
        // Проверяем есть ли земля под ногами с учетом размеров бота
        bool hasGround = false;
        int radiusBlocks = Mathf.CeilToInt(config.botDiameterVoxels / 2f);
        
        // Проверяем все блоки под ногами бота
        for (int dx = -radiusBlocks; dx <= radiusBlocks; dx++)
        {
            for (int dz = -radiusBlocks; dz <= radiusBlocks; dz++)
            {
                Vector3Int groundVoxel = new Vector3Int(botVoxel.x + dx, botVoxel.y - 1, botVoxel.z + dz);
                if (VoxelWorld.IsVoxelSolid(groundVoxel))
                {
                    hasGround = true;
                    break;
                }
            }
            if (hasGround) break;
        }
        
        // Бот на земле если есть земля под ногами
        // Но не перезаписываем isGrounded если он был установлен в ApplyGravity()
        if (!isGrounded || hasGround)
        {
            isGrounded = hasGround;
        }
        
        // Debug log when ground status changes
        if (isGrounded != wasGrounded)
        {
            Debug.Log($"[VoxelBotController] Ground status changed: {wasGrounded} -> {isGrounded}, hasGround={hasGround}");
            
            // If we just lost ground, ensure we start falling
            if (!isGrounded && wasGrounded)
            {
                velocity.y = 0; // Start falling from rest
                Debug.Log($"[VoxelBotController] Started falling, velocity.y={velocity.y}");
            }
            
            wasGrounded = isGrounded;
        }
        
        // If on ground, reset vertical velocity
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = 0;
            isFalling = false;
        }
    }
    
    private void ApplyGravity()
    {
        if (isJumping) return; // No gravity during jump
        
        if (!isGrounded)
        {
            // Apply gravity (gravity is negative, so velocity.y becomes more negative)
            float oldVelocityY = velocity.y;
            velocity.y += config.gravity * Time.deltaTime;
            
            // Limit fall speed
            velocity.y = Mathf.Max(velocity.y, -config.maxFallSpeed);
            
            // Apply vertical movement (velocity.y is negative when falling)
            Vector3 newPosition = transform.position + Vector3.up * velocity.y * Time.deltaTime;
            
            // Check for ground collision when moving down
            if (velocity.y < 0) // Moving down
            {
                Vector3Int newVoxel = VoxelWorld.WorldToVoxel(newPosition);
                Vector3Int groundVoxel = new Vector3Int(newVoxel.x, newVoxel.y - 1, newVoxel.z);
                
                // Check if we would hit ground
                if (VoxelWorld.IsVoxelSolid(groundVoxel))
                {
                    // Stop at ground level
                    Vector3 groundWorldPos = VoxelWorld.VoxelToWorld(groundVoxel);
                    newPosition.y = groundWorldPos.y + 1f; // Position above ground
                    velocity.y = 0; // Stop falling
                    isGrounded = true;
                    Debug.Log($"[VoxelBotController] Hit ground at {newPosition}, stopped falling");
                }
            }
            
            transform.position = newPosition;
            
            isFalling = true;
            
            // Debug log
            if (Time.frameCount % 60 == 0) // Every second
            {
                Debug.Log($"[VoxelBotController] Falling: velocity.y={velocity.y:F2} (was {oldVelocityY:F2}), position={transform.position}");
            }
        }
        else
        {
            // On ground, reset vertical velocity
            velocity.y = 0;
            isFalling = false;
        }
    }
    
    private void HandleAIMovement()
    {
        if (aiMoveDirection.sqrMagnitude > 0.1f)
        {
            MoveInDirection(aiMoveDirection);
        }
        
        if (aiShouldJump)
        {
            StartJump(aiMoveDirection);
            aiShouldJump = false;
        }
    }
    
    private void MoveInDirection(Vector3 direction)
    {
        if (isJumping) return; // Can't move during jump
        
        direction.y = 0; // Only horizontal movement
        direction = direction.normalized;
        
        // Check for obstacles in movement direction
        if (HasObstacleInDirection(direction))
        {
            return; // Can't move
        }
        
        // Apply horizontal movement
        Vector3 horizontalMovement = direction * config.moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + horizontalMovement;
        
        // Don't force height adjustment - let gravity handle vertical movement
        // Only check if we're moving to a valid position
        Vector3Int voxelPos = VoxelWorld.WorldToVoxel(newPosition);
        
        // Check if there's enough space at the target position
        bool hasSpace = true;
        for (int i = 0; i <= config.botHeightVoxels; i++)
        {
            Vector3Int checkVoxel = new Vector3Int(voxelPos.x, voxelPos.y + i, voxelPos.z);
            if (VoxelWorld.IsVoxelSolid(checkVoxel))
            {
                hasSpace = false;
                break;
            }
        }
        
        // Only move if there's space
        if (hasSpace)
        {
            transform.position = newPosition;
        }
        
        // Rotate towards movement direction
        if (direction.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, config.turnSpeed * Time.deltaTime);
        }
    }
    
    private bool HasObstacleInDirection(Vector3 direction)
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentPos + direction.normalized * 1.5f;
        
        // Check multiple points along the movement path
        int steps = 5; // Check 5 points along the path
        for (int step = 1; step <= steps; step++)
        {
            float t = (float)step / steps;
            Vector3 checkPos = Vector3.Lerp(currentPos, targetPos, t);
            Vector3Int checkVoxel = VoxelWorld.WorldToVoxel(checkPos);
            
            // Check if there's a solid block in the way at this point
            for (int i = 0; i <= config.botHeightVoxels; i++)
            {
                Vector3Int obstacleVoxel = new Vector3Int(checkVoxel.x, checkVoxel.y + i, checkVoxel.z);
                if (VoxelWorld.IsVoxelSolid(obstacleVoxel))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private float GetSurfaceHeight(Vector3Int voxelPos)
    {
        // Find the highest solid block at this X,Z position
        for (int y = VoxelWorld.Instance.GetWorldHeight() - 1; y >= 0; y--)
        {
            Vector3Int checkVoxel = new Vector3Int(voxelPos.x, y, voxelPos.z);
            if (VoxelWorld.IsVoxelSolid(checkVoxel))
            {
                return y + 1; // Return height above the solid block
            }
        }
        
        return 0; // No ground found
    }
    
    public void StartJump(Vector3 direction)
    {
        if (isJumping || !isGrounded) return;
        
        direction.y = 0;
        direction = direction.normalized;
        
        // Calculate jump target
        Vector3Int currentVoxel = VoxelWorld.WorldToVoxel(transform.position);
        Vector3Int jumpTargetVoxel = new Vector3Int(
            currentVoxel.x + Mathf.RoundToInt(direction.x * config.jumpDistanceVoxels),
            currentVoxel.y,
            currentVoxel.z + Mathf.RoundToInt(direction.z * config.jumpDistanceVoxels)
        );
        
        Vector3 jumpTarget = VoxelWorld.VoxelToWorld(jumpTargetVoxel);
        
        // Check if jump target is within world bounds
        if (!IsPositionWithinWorldBounds(jumpTarget))
        {
            Debug.LogWarning($"[VoxelBotController] Jump target outside world bounds: {jumpTarget}");
            return; // Can't jump outside world
        }
        
        // Check if jump target is valid
        if (!IsValidJumpTarget(jumpTargetVoxel))
        {
            return; // Can't jump there
        }
        
        // Calculate jump trajectory
        jumpTrajectory = CalculateJumpTrajectory(transform.position, jumpTarget);
        
        if (jumpTrajectory == null || jumpTrajectory.Count == 0)
        {
            return; // Invalid trajectory
        }
        
        // Start jump
        jumpStartPosition = transform.position;
        jumpTargetPosition = jumpTarget;
        jumpStartTime = Time.time;
        currentTrajectoryIndex = 0;
        isJumping = true;
        
        Debug.Log($"[VoxelBotController] Started jump from {jumpStartPosition} to {jumpTargetPosition}");
    }
    
    private bool IsValidJumpTarget(Vector3Int targetVoxel)
    {
        // Check if target voxel is within world bounds
        if (targetVoxel.x < 0 || targetVoxel.x >= VoxelWorld.Instance.chunksX * 16 ||
            targetVoxel.z < 0 || targetVoxel.z >= VoxelWorld.Instance.chunksZ * 16 ||
            targetVoxel.y < 0 || targetVoxel.y >= VoxelWorld.Instance.GetWorldHeight())
        {
            return false;
        }
        
        // Check if target voxel is empty
        if (VoxelWorld.IsVoxelSolid(targetVoxel)) return false;
        
        // Check if there's ground below target (support for stairs)
        bool hasGround = false;
        for (int dy = 1; dy <= 2; dy++) // Check 2 blocks down for stairs
        {
            Vector3Int groundVoxel = new Vector3Int(targetVoxel.x, targetVoxel.y - dy, targetVoxel.z);
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
            Vector3Int checkVoxel = new Vector3Int(targetVoxel.x, targetVoxel.y + i, targetVoxel.z);
            if (VoxelWorld.IsVoxelSolid(checkVoxel)) return false;
        }
        
        return true;
    }
    
    private List<Vector3> CalculateJumpTrajectory(Vector3 start, Vector3 target)
    {
        List<Vector3> trajectory = new List<Vector3>();
        
        float totalDistance = Vector3.Distance(start, target);
        float jumpDuration = totalDistance / config.moveSpeed;
        
        for (int i = 0; i <= config.jumpTrajectoryPoints; i++)
        {
            float t = (float)i / config.jumpTrajectoryPoints;
            
            // Interpolate horizontal position
            Vector3 horizontalPos = Vector3.Lerp(start, target, t);
            
            // Calculate vertical position with jump arc
            float jumpArc = Mathf.Sin(t * Mathf.PI) * config.jumpHeight;
            // Use target Y as base height for landing
            float baseHeight = Mathf.Lerp(start.y, target.y, t);
            Vector3 trajectoryPoint = new Vector3(horizontalPos.x, baseHeight + jumpArc, horizontalPos.z);
            
            // Check if trajectory point is within world bounds
            if (IsPositionWithinWorldBounds(trajectoryPoint))
            {
                trajectory.Add(trajectoryPoint);
            }
            else
            {
                // If any point is outside bounds, return empty trajectory
                Debug.LogWarning($"[VoxelBotController] Trajectory point outside world bounds: {trajectoryPoint}");
                return new List<Vector3>();
            }
        }
        
        return trajectory;
    }
    
    private void HandleJump()
    {
        if (!isJumping) return;
        
        float jumpTime = Time.time - jumpStartTime;
        float jumpDuration = Vector3.Distance(jumpStartPosition, jumpTargetPosition) / config.moveSpeed;
        
        if (jumpTime >= jumpDuration)
        {
            // Jump completed
            Vector3 finalPosition = jumpTargetPosition;
            
            // Проверяем границы мира перед установкой позиции
            if (IsPositionWithinWorldBounds(finalPosition))
            {
                transform.position = finalPosition;
                isJumping = false;
                Debug.Log($"[VoxelBotController] Jump completed to {finalPosition}");
            }
            else
            {
                // Позиция вне границ мира - прерываем прыжок
                Debug.LogWarning($"[VoxelBotController] Jump target outside world bounds: {finalPosition}");
                isJumping = false;
            }
            return;
        }
        
        // Follow trajectory
        float t = jumpTime / jumpDuration;
        
        // Interpolate horizontal position
        Vector3 horizontalStart = new Vector3(jumpStartPosition.x, 0, jumpStartPosition.z);
        Vector3 horizontalTarget = new Vector3(jumpTargetPosition.x, 0, jumpTargetPosition.z);
        Vector3 horizontalPos = Vector3.Lerp(horizontalStart, horizontalTarget, t);
        
        // Calculate vertical position with jump arc
        float jumpArc = Mathf.Sin(t * Mathf.PI) * config.jumpHeight;
        Vector3 currentPos = new Vector3(horizontalPos.x, jumpStartPosition.y + jumpArc, horizontalPos.z);
        
        // Проверяем границы мира
        if (!IsPositionWithinWorldBounds(currentPos))
        {
            Debug.LogWarning($"[VoxelBotController] Jump position outside world bounds: {currentPos}");
            isJumping = false;
            return;
        }
        
        // Check for obstacles during jump
        Vector3Int currentVoxel = VoxelWorld.WorldToVoxel(currentPos);
        if (VoxelWorld.IsVoxelSolid(currentVoxel))
        {
            // Hit obstacle, stop horizontal movement but continue vertical
            currentPos.x = transform.position.x;
            currentPos.z = transform.position.z;
        }
        
        transform.position = currentPos;
    }
    
    private bool IsPositionWithinWorldBounds(Vector3 position)
    {
        if (VoxelWorld.Instance == null) return true;
        
        // Проверяем границы мира
        int worldHeight = VoxelWorld.Instance.GetWorldHeight();
        int worldWidth = VoxelWorld.Instance.chunksX * 16; // 16 блоков в чанке
        int worldDepth = VoxelWorld.Instance.chunksZ * 16;
        
        // Проверяем Y координату (высота)
        if (position.y < 0 || position.y >= worldHeight)
        {
            return false;
        }
        
        // Проверяем X и Z координаты (границы мира)
        if (position.x < 0 || position.x >= worldWidth || 
            position.z < 0 || position.z >= worldDepth)
        {
            return false;
        }
        
        return true;
    }
    
    private void UpdateAnimations()
    {
        if (animator == null) return;
        
        // Set animation parameters (using same names as EnemyBot.cs)
        float speed = velocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(speed / config.moveSpeed);
        
        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsGrounded", isGrounded);
        
        // Only set jumping parameter if it exists in the animator
        if (HasAnimatorParameter("IsJumping"))
        {
            animator.SetBool("IsJumping", isJumping);
        }
        
        if (HasAnimatorParameter("IsFalling"))
        {
            animator.SetBool("IsFalling", isFalling);
        }
    }
    
    private bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == parameterName)
            {
                return true;
            }
        }
        return false;
    }
    
    // AI Control Interface
    public void MoveToTarget(Vector3 moveDirection, bool shouldJump)
    {
        aiMoveDirection = moveDirection;
        aiShouldJump = shouldJump;
        isAIControlled = true;
    }
    
    public void DisableAIControl()
    {
        isAIControlled = false;
        aiMoveDirection = Vector3.zero;
        aiShouldJump = false;
    }
    
    public void EnableAIControl()
    {
        isAIControlled = true;
    }
    
    // Getters for AI
    public bool IsGrounded => isGrounded;
    public bool IsJumping => isJumping;
    public bool IsFalling => isFalling;
    public Vector3 Velocity => velocity;
    
    // Legacy interface methods for compatibility
    public void SetTarget(Transform target)
    {
        // This method is kept for compatibility but doesn't do anything
        // Target is now managed by VoxelBotAI
    }
    
    public VoxelBotData GetBotData()
    {
        return new VoxelBotData(
            gameObject.name,
            transform.position,
            config
        );
    }
    
    public bool CheckHit(Vector3 hitPoint, float damage)
    {
        // Simple hit check - if hit point is within bot bounds
        Vector3 botPos = transform.position;
        float distance = Vector3.Distance(hitPoint, botPos);
        float botRadius = config.botDiameterVoxels * 0.5f;
        
        return distance <= botRadius;
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        // Only show jump trajectory when jumping
        if (isJumping && jumpTrajectory != null && jumpTrajectory.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < jumpTrajectory.Count - 1; i++)
            {
                Gizmos.DrawLine(jumpTrajectory[i], jumpTrajectory[i + 1]);
            }
        }
    }
}