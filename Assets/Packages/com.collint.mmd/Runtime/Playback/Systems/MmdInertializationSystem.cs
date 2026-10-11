using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Playback.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdInertializationGroup))]
	public partial struct MmdInertializationSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new InertializationJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct InertializationJob : IJobEntity {
			public float DeltaTime;

			private void Execute(in MmdDatabase data, in MmdPlayback playback, in MmdInertialization settings,
				in MmdActionState action, in LocalTransform transform, ref MmdRootMotion motion,
				ref MmdInertializationState state, DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdInertialPose> history) {
				if (DeltaTime <= 0f || !motion.Valid) {
					return;
				}
				bool hasHistory = history.Length == poses.Length && history.Length > 0;
				bool switched = hasHistory && state.Revision != playback.Revision;
				if (!hasHistory) {
					history.ResizeUninitialized(poses.Length);
					state.PoseDuration = settings.PoseDuration;
					state.Elapsed = math.max(state.PoseDuration, settings.RootDuration);
				} else if (switched) {
					state.Elapsed = 0f;
				}
				ref var database = ref data.Value.Value;
				float previousTime = playback.Time - DeltaTime * math.max(0f, playback.Speed);
				UpdatePoseAlignment(ref database, playback, settings, action, motion, hasHistory, switched, previousTime, ref state);
				state.Elapsed = math.min(state.Elapsed + DeltaTime, math.max(state.PoseDuration, settings.RootDuration));
				for (int bone = 0; bone < poses.Length; bone++) {
					var target = poses[bone].Value;
					var previousTarget = MmdAnimationMath.PoseAt(ref database, playback.ClipIndex, previousTime, bone);
					if (database.Parents[bone] < 0) {
						target.Position += state.StationaryPoseOffset;
						previousTarget.Position += state.StationaryPoseOffset;
					}
					var entry = hasHistory ? history[bone] : new MmdInertialPose { Previous = previousTarget };
					int parent = database.Parents[bone];
					quaternion targetModel = parent < 0 ? target.Rotation : math.mul(history[parent].TargetModelRotation, target.Rotation);
					quaternion previousTargetModel = parent < 0 ? previousTarget.Rotation : math.mul(history[parent].PreviousTargetModelRotation, previousTarget.Rotation);
					quaternion previousDisplayedModel = hasHistory ? entry.DisplayedModelRotation : previousTargetModel;
					if (switched) {
						entry.PositionOffset = entry.Previous.Position - previousTarget.Position;
						// Blend the resulting model orientation: compensating root/child turns must not add a second spin.
						entry.RotationOffset = MmdAnimationMath.RotationVector(math.mul(previousDisplayedModel, math.inverse(previousTargetModel)));
						entry.ScaleOffset = entry.Previous.Scale - previousTarget.Scale;
						entry.PositionVelocityOffset = entry.PositionVelocity - (target.Position - previousTarget.Position) / DeltaTime;
						entry.AngularVelocityOffset = entry.AngularVelocity - MmdAnimationMath.RotationVector(math.mul(targetModel, math.inverse(previousTargetModel))) / DeltaTime;
						entry.ScaleVelocityOffset = entry.ScaleVelocity - (target.Scale - previousTarget.Scale) / DeltaTime;
					}
					target.Position += MmdAnimationMath.Decay(entry.PositionOffset, entry.PositionVelocityOffset, state.Elapsed, state.PoseDuration);
					quaternion displayedModel = math.normalize(math.mul(MmdAnimationMath.Rotation(MmdAnimationMath.Decay(entry.RotationOffset,
						entry.AngularVelocityOffset, state.Elapsed, state.PoseDuration)), targetModel));
					target.Rotation = parent < 0 ? displayedModel : math.normalize(math.mul(math.inverse(history[parent].DisplayedModelRotation), displayedModel));
					target.Scale += MmdAnimationMath.Decay(entry.ScaleOffset, entry.ScaleVelocityOffset, state.Elapsed, state.PoseDuration);
					entry.PositionVelocity = (target.Position - entry.Previous.Position) / DeltaTime;
					entry.AngularVelocity = MmdAnimationMath.RotationVector(math.mul(displayedModel, math.inverse(previousDisplayedModel))) / DeltaTime;
					entry.TargetModelRotation = targetModel;
					entry.PreviousTargetModelRotation = previousTargetModel;
					entry.DisplayedModelRotation = displayedModel;
					entry.ScaleVelocity = (target.Scale - entry.Previous.Scale) / DeltaTime;
					entry.Previous = target;
					history[bone] = entry;
					poses[bone] = new MmdLocalPose { Value = target };
				}
				float3 velocity = math.rotate(transform.Rotation, motion.Translation) / DeltaTime;
				float3 angular = math.rotate(transform.Rotation, MmdAnimationMath.RotationVector(motion.Rotation)) / DeltaTime;
				if (switched) {
					state.RootVelocityOffset = state.RootVelocity - velocity;
					state.RootAngularOffset = state.RootAngularVelocity - angular;
				}
				// Action/warped root deltas are authored constraints and must stay exact.
				if (hasHistory && action.ActionId == 0) {
					velocity += MmdAnimationMath.Decay(state.RootVelocityOffset, float3.zero, state.Elapsed, settings.RootDuration);
					angular += MmdAnimationMath.Decay(state.RootAngularOffset, float3.zero, state.Elapsed, settings.RootDuration);
					motion.Translation = math.rotate(math.inverse(transform.Rotation), velocity) * DeltaTime;
					motion.Rotation = MmdAnimationMath.Rotation(math.rotate(math.inverse(transform.Rotation), angular) * DeltaTime);
				}
				state.RootVelocity = velocity;
				state.RootAngularVelocity = angular;
				state.Revision = playback.Revision;
				state.PreviousClipIndex = playback.ClipIndex;
				if (database.FeatureCount == LocomotionSchema.Dimension) {
					state.PreviousFootCenter = FootCenter(ref database, playback.ClipIndex, playback.Time) + state.StationaryPoseOffset;
				}
			}

			private void UpdatePoseAlignment(ref MotionDatabase database, in MmdPlayback playback, in MmdInertialization settings,
				in MmdActionState action, in MmdRootMotion motion, bool hasHistory, bool switched, float previousTime,
				ref MmdInertializationState state) {
				if (!hasHistory) {
					state.StationaryPoseOffset = float3.zero;
					return;
				}
				if (!switched) {
					return;
				}
				float3 offset = float3.zero;
				bool stationaryLoop = settings.PoseDuration > 0f && database.FeatureCount == LocomotionSchema.Dimension &&
					action.ActionId == 0 && database.Clips[playback.ClipIndex].Loop &&
					math.lengthsq(state.RootVelocity.xz) <= 0.0025f &&
					math.lengthsq(motion.Translation.xz / DeltaTime) <= 0.0025f;
				state.PoseDuration = stationaryLoop ? math.max(settings.PoseDuration, settings.StationaryPoseDuration) : settings.PoseDuration;
				if (stationaryLoop) {
					if (!database.Clips[state.PreviousClipIndex].Loop) {
						// Preserve the settled footprint instead of sliding the entire skeleton
						// to the idle clip's authored offset. Entity/root motion stay unchanged.
						offset = state.PreviousFootCenter - FootCenter(ref database, playback.ClipIndex, previousTime);
						offset.y = 0f;
					} else {
						offset = state.StationaryPoseOffset;
					}
				}
				// On renewed locomotion, normal inertialization releases this offset from
				// the displayed pose rather than accumulating corrections across stops.
				state.StationaryPoseOffset = offset;
			}

			private static float3 FootCenter(ref MotionDatabase database, int clip, float time) {
				int frame = MmdAnimationMath.FrameAt(ref database, clip, time);
				float local = MmdAnimationMath.LocalTime(database.Clips[clip], time);
				float amount = math.saturate((local - database.Frames[frame].Time) /
					(database.Frames[frame + 1].Time - database.Frames[frame].Time));
				int first = frame * LocomotionSchema.Dimension;
				int next = first + LocomotionSchema.Dimension;
				float3 before = new float3(database.Features[first + 15] + database.Features[first + 21],
					database.Features[first + 16] + database.Features[first + 22], database.Features[first + 17] + database.Features[first + 23]);
				float3 after = new float3(database.Features[next + 15] + database.Features[next + 21],
					database.Features[next + 16] + database.Features[next + 22], database.Features[next + 17] + database.Features[next + 23]);
				return math.lerp(before, after, amount) * 0.5f;
			}
		}
	}
}
