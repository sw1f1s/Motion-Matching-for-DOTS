using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.FallState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleFallUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (current, fall, data, playback, ground, transition) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleFall>>, RefRO<MmdSampleFall>,
				RefRO<MmdDatabase>, RefRW<MmdPlayback>, RefRO<MmdSampleGroundState>, RefRW<MmdSampleStateTransition>>()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				ref var database = ref data.ValueRO.Value.Value;
				var clip = database.Clips[playback.ValueRO.ClipIndex];
				if (playback.ValueRO.MatchedFrame >= 0 && clip.ActionId == 0 && MotionDatabaseGroups.Contains(ref database, clip, fall.ValueRO.Group)) {
					// After the entry match, playback owns the phase of the complete fall loop.
					playback.ValueRW.SearchCountdown = float.PositiveInfinity;
				}
				if (ground.ValueRO.IsGrounded) {
					transition.ValueRW.Request<MmdSampleLanding>(MmdSampleStatePriority.Landing);
				}
			}
		}
	}
}
