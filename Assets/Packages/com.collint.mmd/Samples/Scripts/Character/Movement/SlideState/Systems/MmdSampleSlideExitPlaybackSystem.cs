using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleSlideAnimationGroup))]
	public partial struct MmdSampleSlideExitPlaybackSystem : ISystem {
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
		[WithAll(typeof(MmdSampleState<MmdSampleSlideExit>))]
		private partial struct UpdateJob : IJobEntity {

			private void Execute(ref MmdPlayback playback) {
				playback.SearchCountdown = float.PositiveInfinity;
			}
		}
	}
}
