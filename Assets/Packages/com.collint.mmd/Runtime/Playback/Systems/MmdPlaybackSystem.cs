using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdPlaybackGroup))]
	public partial struct MmdPlaybackSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			state.Dependency = new PlaybackJob {
				DeltaTime = SystemAPI.Time.DeltaTime,
				Paused = SystemAPI.GetSingleton<MmdWorldState>().IsPaused
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct PlaybackJob : IJobEntity {
			public float DeltaTime;
			public bool Paused;

			private void Execute(in MmdDatabase data, in MmdActionWarp warp, ref MmdPlayback playback, ref MmdRootMotion motion,
				ref MmdActionState action, DynamicBuffer<MmdLocalPose> poses) {
				motion = new MmdRootMotion { Rotation = quaternion.identity };
				if (Paused) {
					return;
				}
				ref var database = ref data.Value.Value;
				ref var clip = ref database.Clips[playback.ClipIndex];
				float previous = playback.Time;
				float next = previous + DeltaTime * math.max(0f, playback.Speed);
				RigidTransform delta = math.mul(math.inverse(MmdAnimationMath.RootAt(ref database, playback.ClipIndex, previous)),
					MmdAnimationMath.RootAt(ref database, playback.ClipIndex, next));
				if (warp.Enabled && action.ActionId != 0 && warp.RequestRevision == action.ActiveRevision) {
					delta = MmdAnimationMath.WarpDelta(MmdAnimationMath.RootAt(ref database, playback.ClipIndex, previous),
						MmdAnimationMath.RootAt(ref database, playback.ClipIndex, next),
						MmdAnimationMath.RootAt(ref database, playback.ClipIndex, clip.Duration), warp.TargetEnd,
						previous / clip.Duration, next / clip.Duration);
				}
				motion = new MmdRootMotion {
					Translation = delta.pos,
					Rotation = delta.rot,
					DeltaTime = DeltaTime,
					PlaybackRevision = playback.Revision,
					Valid = true
				};
				playback.Time = clip.Loop ? MmdAnimationMath.LocalTime(clip, next) : math.min(next, clip.Duration);
				if (action.ActionId != 0 && next >= clip.Duration) {
					action.Completed = true;
				}
				poses.ResizeUninitialized(database.Parents.Length);
				for (int bone = 0; bone < poses.Length; bone++) {
					poses[bone] = new MmdLocalPose { Value = MmdAnimationMath.PoseAt(ref database, playback.ClipIndex, playback.Time, bone) };
				}
			}
		}
	}

}
