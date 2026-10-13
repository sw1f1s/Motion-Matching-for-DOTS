using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using Unity.Burst;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleSearchQueryGroup))]
	public partial struct MmdSamplePoseQuerySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdDatabase>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new PoseQueryJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct PoseQueryJob : IJobEntity {
			public float DeltaTime;

			private void Execute(in MmdSampleCharacterControl control, in MmdDatabase data, in MmdPlayback playback, DynamicBuffer<MmdSearchQueryElement> query,
				in DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdPoseFeatureHistory> history) {
				LocomotionSchema.WritePose(ref data.Value.Value, playback, query, poses, history, DeltaTime);
			}
		}
	}
}
