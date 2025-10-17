using UnityEngine;

/// <summary>
/// Адаптер для VoxelMovementService чтобы он работал с EnemyAIStateMachine
/// </summary>
public class VoxelMovementServiceAdapter : EnemyMovementService
{
    private VoxelMovementService voxelMovementService;
    
    public VoxelMovementServiceAdapter(CharacterController controller, Transform transform, EnemyMovementConfig config, Animator animator) 
        : base(controller, transform, config, animator)
    {
        // Создаем VoxelMovementService
        voxelMovementService = new VoxelMovementService(transform, config, animator);
    }
    
    public override void Update()
    {
        voxelMovementService.Update();
    }
    
    public override void HandleMovement(Vector3 moveDirection)
    {
        voxelMovementService.HandleMovement(moveDirection);
    }
    
    public override void InitiateJump()
    {
        voxelMovementService.InitiateJump();
    }
    
    public override bool IsPreparingJumpOrCooldown()
    {
        return voxelMovementService.IsPreparingJumpOrCooldown();
    }
    
    // Переопределяем свойства
    public override bool IsGrounded => voxelMovementService.IsGrounded;
    public override Vector3 Velocity => voxelMovementService.Velocity;
    public override bool IsPreparingJump => voxelMovementService.IsPreparingJump;
}
