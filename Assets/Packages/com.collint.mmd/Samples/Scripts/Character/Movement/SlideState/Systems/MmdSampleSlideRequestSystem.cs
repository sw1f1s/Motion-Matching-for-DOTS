using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleSlideRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleCharacterControl), typeof(MmdSampleState<MmdSampleLocomotion>))]
		private partial struct UpdateJob : IJobEntity {
			private void Execute(in MmdSampleInput input, in MmdSampleSlide slide, in MmdSampleGroundState ground, in PhysicsVelocity velocity, ref MmdSampleStateTransition transition) {
				if (input.CrouchPressed && input.Sprint && ground.IsGrounded && math.length(velocity.Linear.xz) >= slide.MinimumEntrySpeed) {
					transition.Request<MmdSampleSlide>(MmdSampleStatePriority.Slide);
				}
			}
		}
	}
}
