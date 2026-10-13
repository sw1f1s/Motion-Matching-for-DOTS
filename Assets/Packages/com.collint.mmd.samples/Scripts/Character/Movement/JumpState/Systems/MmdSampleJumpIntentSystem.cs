using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[BurstCompile]
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

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new JumpIntentJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleCharacterControl))]
		private partial struct JumpIntentJob : IJobEntity {
			public float DeltaTime;

			private void Execute(ref MmdSampleJumpIntent intent, in MmdSampleGroundState ground, in MmdSampleMovement movement, in MmdSampleInput input) {
				intent.Remaining = input.JumpPressed ? JumpBufferDuration : math.max(0f, intent.Remaining - DeltaTime);
				intent.TraversalRemaining = input.JumpPressed ? TraversalBufferDuration : math.max(0f, intent.TraversalRemaining - DeltaTime);
				intent.CoyoteRemaining = movement.IgnoreGround ? 0f : ground.IsGrounded ? CoyoteDuration : math.max(0f, intent.CoyoteRemaining - DeltaTime);
			}
		}
	}
}
