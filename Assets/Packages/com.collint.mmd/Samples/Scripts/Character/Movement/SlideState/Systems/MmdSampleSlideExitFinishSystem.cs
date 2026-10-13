using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateFinishGroup))]
	public partial struct MmdSampleSlideExitFinishSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateFinished<MmdSampleSlideExit>))]
		private partial struct UpdateJob : IJobEntity {

			private void Execute(
				in MmdSampleCrouchProgress progress,
				in MmdSampleStateTransition transition,
				ref MmdSampleCharacterControl control,
				ref MmdInertialization smoothing,
				ref MmdSearchSettings settings,
				ref MmdSampleMovement movement) {
				movement = default;
				if (transition.Target.Component != MmdSampleStateDescriptor.For<MmdSampleCrouch>().Component) {
					control = progress.StandingControl;
					smoothing = progress.StandingInertialization;
					settings.TrajectoryDirectionWeight = progress.StandingDirectionWeight;
				}
			}
		}
	}
}
