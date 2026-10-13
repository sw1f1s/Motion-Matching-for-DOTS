using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleJumpRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new RequestJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleCharacterControl), typeof(MmdSampleJump))]
		[WithAny(typeof(MmdSampleState<MmdSampleLocomotion>), typeof(MmdSampleState<MmdSampleLanding>), typeof(MmdSampleState<MmdSampleFall>),
			typeof(MmdSampleState<MmdSampleSlide>), typeof(MmdSampleState<MmdSampleSlideExit>),
			typeof(MmdSampleState<MmdSampleCrouchEnter>), typeof(MmdSampleState<MmdSampleCrouch>), typeof(MmdSampleState<MmdSampleCrouchExit>))]
		private partial struct RequestJob : IJobEntity {
			private void Execute(in MmdSampleGroundState ground, in MmdSampleJumpIntent intent, ref MmdSampleStateTransition transition) {
				if (intent.Remaining > 0f && (ground.IsGrounded || intent.CoyoteRemaining > 0f)) {
					transition.Request<MmdSampleJump>(MmdSampleStatePriority.Jump);
				}
			}
		}
	}
}
