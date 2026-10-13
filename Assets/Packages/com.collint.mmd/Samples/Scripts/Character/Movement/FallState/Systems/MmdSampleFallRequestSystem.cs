using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.FallState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleFallRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new RequestJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleFall))]
		[WithNone(typeof(MmdSampleState<MmdSampleJump>), typeof(MmdSampleState<MmdSampleFall>), typeof(MmdSampleTraversalActive))]
		private partial struct RequestJob : IJobEntity {
			private void Execute(in MmdSampleGroundState ground, in PhysicsVelocity velocity, in MmdSampleMovementSettings settings, ref MmdSampleStateTransition transition) {
				if (!ground.IsGrounded && velocity.Linear.y <= -settings.FallEntrySpeed) {
					transition.Request<MmdSampleFall>(MmdSampleStatePriority.Fall);
				}
			}
		}
	}
}
