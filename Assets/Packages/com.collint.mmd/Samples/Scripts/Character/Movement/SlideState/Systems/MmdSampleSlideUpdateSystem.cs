using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleSlideUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleState<MmdSampleSlide>))]
		private partial struct UpdateJob : IJobEntity {
			public float DeltaTime;
			private void Execute(
				in MmdSampleSlide slide,
				in MmdSampleSlideProgress progress,
				in MmdActionState action,
				ref MmdSampleState<MmdSampleSlide> current,
				ref MmdActionRequest request,
				ref MmdSearchSettings settings,
				ref MmdPlayback playback,
				ref MmdSampleStateTransition transition) {
				current.Elapsed += DeltaTime;
				if (math.length(progress.Velocity) <= slide.StopSpeed) {
					transition.Request<MmdSampleSlideExit>(MmdSampleStatePriority.Completion);
					return;
				}
				if (action.ActionId != 0 && !action.Completed && !action.Rejected) {
					return;
				}
				if (action.ActionId != 0 || action.Rejected) {
					MmdSampleAnimationRequest.Cancel(ref request);
					settings.RequiredTags = slide.MotionTags;
					settings.ExcludedTags = 0;
					settings.PreferLoop = true;
					playback.SearchCountdown = 0f;
				}
			}
		}
	}
}
