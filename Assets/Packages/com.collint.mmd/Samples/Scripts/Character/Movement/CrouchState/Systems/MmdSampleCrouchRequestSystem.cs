using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleCrouchRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new RequestJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleCharacterControl), typeof(MmdSampleCrouch), typeof(MmdSampleState<MmdSampleLocomotion>))]
		private partial struct RequestJob : IJobEntity {
			private void Execute(in MmdSampleGroundState ground, ref MmdSampleStateTransition transition, in MmdSampleInput input) {
				if (input.Crouch && !input.Sprint && ground.IsGrounded) {
					transition.Request<MmdSampleCrouchEnter>(MmdSampleStatePriority.Crouch);
				}
			}
		}
	}
}
