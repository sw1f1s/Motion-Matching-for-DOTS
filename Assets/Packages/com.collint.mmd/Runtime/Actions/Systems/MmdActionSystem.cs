using ColInt.MMD.Database.Data;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Actions.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdActionGroup))]
	public partial struct MmdActionSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new ActionJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct ActionJob : IJobEntity {
			private void Execute(in MmdDatabase data, in MmdActionRequest request,
				ref MmdActionState action, ref MmdPlayback playback) {
				if (request.Revision == action.RequestRevision) {
					return;
				}
				action.RequestRevision = request.Revision;
				action.Rejected = false;
				if (request.ActionId == 0) {
					action.ActionId = 0;
					action.Completed = false;
					playback.SearchCountdown = 0f;
					return;
				}
				if (request.ActionId < 0) {
					action.Rejected = true;
					return;
				}
				ref var database = ref data.Value.Value;
				int selected = -1;
				for (int i = 0; i < database.Clips.Length; i++) {
					if (database.Clips[i].Mode == MotionPlaybackMode.Action && database.Clips[i].ActionId == request.ActionId &&
						(request.PreferredClip < 0 || request.PreferredClip == i)) {
						selected = i;
						break;
					}
				}
				if (selected < 0) {
					action.Rejected = true;
					return;
				}
				action.ActiveRevision = request.Revision;
				action.ActionId = request.ActionId;
				action.Completed = false;
				playback.ClipIndex = selected;
				playback.Time = 0f;
				playback.Revision++;
			}
		}
	}

}
