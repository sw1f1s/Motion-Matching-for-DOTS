using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSearchGroup))]
	public partial struct MmdSearchSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new MotionSearchJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct MotionSearchJob : IJobEntity {
			public float DeltaTime;

			private void Execute(in MmdDatabase data, in MmdSearchSettings settings, in MmdActionState action,
				in DynamicBuffer<MmdSearchQueryElement> query, in DynamicBuffer<MmdMotionGroup> groups, ref MmdPlayback playback, ref MmdFault fault) {
				if (action.ActionId != 0) {
					return;
				}
				playback.TransitionElapsed += DeltaTime;
				playback.SearchCountdown -= DeltaTime;
				if (playback.SearchCountdown > 0f) {
					return;
				}
				ref var database = ref data.Value.Value;
				if (query.Length != database.FeatureCount) {
					fault.Code = MmdFaultCode.InvalidQuery;
					return;
				}
				for (int i = 0; i < query.Length; i++) {
					if (!math.isfinite(query[i].Value)) {
						fault.Code = MmdFaultCode.InvalidQuery;
						return;
					}
				}
				int continuation = MmdAnimationMath.FrameAt(ref database, playback.ClipIndex, playback.Time);
				ref var currentClip = ref database.Clips[playback.ClipIndex];
				if (playback.MatchedFrame >= 0 && playback.TransitionElapsed < settings.MinimumTransitionTime && currentClip.ActionId == 0 &&
					MmdMotionGroups.Matches(ref database, currentClip, groups) &&
					(currentClip.Loop || currentClip.Duration - playback.Time >= settings.MinimumRemainingTime)) {
					fault.Code = MmdFaultCode.None;
					// Let the displayed pose settle before comparing its next foot phase.
					// Input changes can explicitly interrupt; tag changes and clip endpoints bypass this hold.
					return;
				}
				int frame = MotionSearch.Find(ref database, query, settings, groups, continuation, playback.Speed, playback.Time);
				playback.SearchCountdown = settings.Interval;
				if (frame < 0) {
					fault.Code = MmdFaultCode.NoCandidate;
					return;
				}
				fault.Code = MmdFaultCode.None;
				playback.MatchedFrame = frame;
				if (frame == continuation) {
					return;
				}
				playback.ClipIndex = database.Frames[frame].ClipIndex;
				playback.Time = database.Frames[frame].Time;
				playback.Revision++;
				playback.TransitionElapsed = 0f;
			}
		}
	}

}
