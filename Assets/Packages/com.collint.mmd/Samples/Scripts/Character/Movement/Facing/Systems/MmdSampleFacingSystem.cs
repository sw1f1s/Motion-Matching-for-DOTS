using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Facing.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleFacingGroup))]
	public partial struct MmdSampleFacingSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new FacingJob {
				PhysicsRoots = SystemAPI.GetComponentLookup<MmdPhysicsRoot>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(MmdSampleState<MmdSampleSlide>), typeof(MmdSampleState<MmdSampleSlideExit>))]
		private partial struct FacingJob : IJobEntity {
			[ReadOnly] public ComponentLookup<MmdPhysicsRoot> PhysicsRoots;

			private void Execute(in MmdSampleInput input, Entity entity, in MmdSampleCharacterControl control, in LocalTransform transform, in MmdActionState action, in MmdDatabase data, ref MmdPlayback playback, in DynamicBuffer<MmdMotionGroup> groups,
				ref MmdSampleTrajectoryState trajectory, ref MmdRootMotion motion) {
				if (!motion.Valid || motion.DeltaTime <= 0f) {
					return;
				}
				if (!PhysicsRoots.HasComponent(entity)) {
					trajectory.Velocity = math.rotate(transform.Rotation, motion.Translation) / motion.DeltaTime;
				}
				float2 facing = MmdSampleFacing.Resolve(input.Movement);
				if (action.ActionId != 0) {
					return;
				}
				if (math.lengthsq(facing) < 0.000001f) {
					// Stop steering immediately, including residual angular motion from the outgoing clip.
					motion.Rotation = quaternion.identity;
					return;
				}
				float error = trajectory.TurnError;
				float limit = math.max(0f, control.TurnSpeed) * motion.DeltaTime;
				float authoredYaw = MmdAnimationMath.RotationVector(motion.Rotation).y;
				ref var database = ref data.Value.Value;
				var clip = database.Clips[playback.ClipIndex];
				if (!clip.Loop && control.TurnGroup != 0 && MmdMotionGroups.Is(groups, MmdMotionGroupUsage.Required, control.TurnGroup)) {
					// A blocked turn keeps its phase until alignment, release or a new direction request.
					playback.SearchCountdown = float.PositiveInfinity;
				}
				if (MmdSampleFacing.IsHalfTurn(ref database, clip)) {
					quaternion current = MmdAnimationMath.RootAt(ref database, playback.ClipIndex, playback.Time).rot;
					quaternion previous = MmdAnimationMath.RootAt(ref database, playback.ClipIndex, playback.Time - motion.DeltaTime * playback.Speed).rot;
					quaternion future = MmdAnimationMath.RootAt(ref database, playback.ClipIndex, playback.Time + 0.6f).rot;
					float rawYaw = MmdAnimationMath.RotationVector(math.mul(math.inverse(previous), current)).y;
					float upcomingYaw = MmdAnimationMath.RotationVector(math.mul(math.inverse(current), future)).y;
					if (math.abs(rawYaw) > 0.001f || math.abs(upcomingYaw) > 0.03f) {
						// Keep the planted phase continuous; search resumes when intent changes or alignment finishes.
						if (MmdMotionGroups.Contains(groups, control.ReversalGroup, MmdMotionGroupUsage.Required) &&
							(rawYaw * error > 0f || upcomingYaw * error > 0f)) {
							playback.SearchCountdown = float.PositiveInfinity;
						}
						float pivotYaw = error >= 0f ? math.clamp(authoredYaw, 0f, error) : math.clamp(authoredYaw, error, 0f);
						motion.Rotation = quaternion.RotateY(pivotYaw);
						return;
					}
					// Once the authored turn has ended, resolve its small remaining heading error normally.
				}
				float correction = math.clamp(error - authoredYaw, -limit, limit);
				float yaw = math.clamp(authoredYaw + correction, -limit, limit);
				yaw = error >= 0f ? math.clamp(yaw, 0f, error) : math.clamp(yaw, error, 0f);
				motion.Rotation = quaternion.RotateY(yaw);
			}
		}
	}
}
