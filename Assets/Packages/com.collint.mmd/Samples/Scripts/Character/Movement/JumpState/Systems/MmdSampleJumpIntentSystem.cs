using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleJumpIntentGroup))]
	public partial struct MmdSampleJumpIntentSystem : ISystem {
		private const float JumpBufferDuration = 0.16f;
		private const float TraversalBufferDuration = 0.35f;
		private const float CoyoteDuration = 0.1f;

		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			float deltaTime = SystemAPI.Time.DeltaTime;
			foreach (var (intent, ground, movement, input) in SystemAPI.Query<RefRW<MmdSampleJumpIntent>, RefRO<MmdSampleGroundState>,
				RefRO<MmdSampleMovement>, RefRO<MmdSampleInput>>().WithAll<MmdSampleCharacterControl>()) {
				intent.ValueRW.Remaining = input.ValueRO.JumpPressed ? JumpBufferDuration : math.max(0f, intent.ValueRO.Remaining - deltaTime);
				intent.ValueRW.TraversalRemaining = input.ValueRO.JumpPressed ? TraversalBufferDuration : math.max(0f, intent.ValueRO.TraversalRemaining - deltaTime);
				intent.ValueRW.CoyoteRemaining = movement.ValueRO.IgnoreGround ? 0f :
					ground.ValueRO.IsGrounded ? CoyoteDuration : math.max(0f, intent.ValueRO.CoyoteRemaining - deltaTime);
			}
		}
	}
}
