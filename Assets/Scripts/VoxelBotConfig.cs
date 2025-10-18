using UnityEngine;

[CreateAssetMenu(fileName = "VoxelBotConfig", menuName = "Bot/Voxel Bot Config")]
public class VoxelBotConfig : ScriptableObject
{
    [Header("Bot Dimensions")]
    [Tooltip("Bot diameter in voxels")]
    public int botDiameterVoxels = 1;
    
    [Tooltip("Bot height in voxels")]
    public int botHeightVoxels = 2;
    
    [Header("Movement")]
    [Tooltip("Movement speed in units per second")]
    public float moveSpeed = 5f;
    
    [Tooltip("Turn speed in degrees per second")]
    public float turnSpeed = 180f;
    
    [Header("Jumping")]
    [Tooltip("Jump height in units")]
    public float jumpHeight = 2f;
    
    [Tooltip("Jump distance in voxels")]
    public int jumpDistanceVoxels = 2;
    
    [Tooltip("Jump trajectory points count")]
    public int jumpTrajectoryPoints = 10;
    
    [Header("Physics")]
    [Tooltip("Gravity force")]
    public float gravity = -9.81f;
    
    [Tooltip("Maximum fall speed")]
    public float maxFallSpeed = 20f;
    
    [Header("Patrol")]
    [Tooltip("Patrol radius in voxels")]
    public int patrolRadiusVoxels = 10;
    
    [Tooltip("Maximum pathfinding distance in voxels")]
    public int maxPathfindingDistanceVoxels = 20;
    
    [Header("AI")]
    [Tooltip("Detection range in voxels")]
    public int detectionRangeVoxels = 15;
    
    [Tooltip("Attack range in voxels")]
    public int attackRangeVoxels = 3;
    
    [Tooltip("Patrol point search attempts")]
    public int patrolSearchAttempts = 10;
}