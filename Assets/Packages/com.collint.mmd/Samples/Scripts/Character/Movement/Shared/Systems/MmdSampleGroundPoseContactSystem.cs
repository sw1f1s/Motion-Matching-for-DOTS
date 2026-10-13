using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePoseContactGroup))]
	public partial struct MmdSampleGroundPoseContactSystem : ISystem {
		private EntityQuery _contacts;

		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			_contacts = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSampleFootContact, MmdSampleGroundState>()
				.WithAny<MmdSampleState<MmdSampleLanding>, MmdSampleState<MmdSampleLocomotion>>().Build(ref state);
			state.RequireForUpdate(_contacts);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new ContactJob {
				DeltaTime = SystemAPI.Time.DeltaTime, Landing = SystemAPI.GetComponentLookup<MmdSampleState<MmdSampleLanding>>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAny(typeof(MmdSampleState<MmdSampleLanding>), typeof(MmdSampleState<MmdSampleLocomotion>))]
		private partial struct ContactJob : IJobEntity {
			public float DeltaTime;
			[ReadOnly] public ComponentLookup<MmdSampleState<MmdSampleLanding>> Landing;

			private void Execute(Entity entity, in MmdDatabase data, in MmdPlayback playback, in LocalTransform transform,
				in MmdSampleFootContact contact, in MmdSampleGroundState ground, in DynamicBuffer<MmdMotionGroup> groups,
				ref MmdInertializationState smoothing, DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdInertialPose> history) {
				if (!ground.IsGrounded || DeltaTime <= 0f) {
					return;
				}
				ref var database = ref data.Value.Value;
				// Landing compression and an Idle blend can straighten the legs before the pelvis rises.
				// Correct the displayed skeleton only; physical root motion and jump timing remain independent.
				if (!Landing.HasComponent(entity) && (smoothing.Elapsed >= smoothing.PoseDuration ||
					!MmdMotionGroups.Any(ref database, database.Clips[playback.ClipIndex], groups, MmdMotionGroupUsage.StationaryPose))) {
					return;
				}
				if (MmdSampleFootPose.Support(ref database, poses, contact, transform, ground.SupportHeight)) {
					MmdSampleFootPose.Record(ref database, poses, history, ref smoothing, DeltaTime);
				}
			}
		}
	}
}
