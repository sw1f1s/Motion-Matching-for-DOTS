using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePoseContactGroup))]
	public partial struct MmdSampleTraversalFootContactSystem : ISystem {
		private EntityQuery _contacts;

		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			_contacts = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSampleFootContact, MmdSampleTraversalActive>().WithNone<MmdSampleTraversalBlocked>().Build(ref state);
			state.RequireForUpdate(_contacts);
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new ContactJob {
				DeltaTime = SystemAPI.Time.DeltaTime
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleTraversalActive))]
		[WithNone(typeof(MmdSampleTraversalBlocked))]
		private partial struct ContactJob : IJobEntity {
			public float DeltaTime;

			private void Execute(in MmdSampleInput input, in MmdDatabase data, in MmdPlayback playback, in LocalTransform transform,
				in MmdSampleTraversalTarget target, in MmdSampleTraversalProgress progress, in MmdSampleTraversalSettings settings,
				in MmdSampleFootContact contact, ref MmdInertializationState smoothing,
				DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdInertialPose> history) {
				if (target.Across || DeltaTime <= 0f) {
					return;
				}
				ref var database = ref data.Value.Value;
				bool continuing = target.Supported && math.dot(input.Movement, math.forward(target.Rotation).xz) > 0.5f;
				float3 position = MmdSampleTraversalPath.Position(ref database, target, progress.Clip, playback.Time, continuing);
				quaternion rotation = MmdSampleTraversalPath.Rotation(target, progress.Clip, playback.Time, settings.AlignmentDuration);
				bool left = Solve(ref database, poses, database.LeftFootBone, contact.LeftToe, position, rotation, transform.Scale, target, settings.Skin);
				bool right = Solve(ref database, poses, database.RightFootBone, contact.RightToe, position, rotation, transform.Scale, target, settings.Skin);
				if (left || right) {
					MmdSampleFootPose.Record(ref database, poses, history, ref smoothing, DeltaTime);
				}
			}

			private static bool Solve(ref MotionDatabase database, DynamicBuffer<MmdLocalPose> poses, int foot, int toe,
				float3 position, quaternion rotation, float scale, in MmdSampleTraversalTarget target, float skin) {
				float3 ankle = MmdSampleFootPose.Model(ref database, poses, foot).c3.xyz;
				float3 tip = MmdSampleFootPose.Model(ref database, poses, toe).c3.xyz;
				float3 ankleCorrection = Correction(position + math.rotate(rotation, ankle * scale), target, skin);
				float3 toeCorrection = Correction(position + math.rotate(rotation, tip * scale), target, skin);
				int calf = database.Parents[foot];
				int thigh = database.Parents[calf];
				float3 forward = math.forward(target.Rotation);
				float3 correction = forward * math.min(math.dot(ankleCorrection, forward), math.dot(toeCorrection, forward));
				correction.y = math.max(ankleCorrection.y, toeCorrection.y);
				if (math.lengthsq(correction) < 0.00000001f) {
					return false;
				}
				float3 goal = ankle + math.rotate(math.inverse(rotation), correction) / scale;
				quaternion footRotation = ModelRotation(ref database, poses, foot);
				// Preserve the animated knee bend; only ankles and toes constrain the contact goal.
				PlaceLeg(ref database, poses, foot, calf, thigh, goal, footRotation);
				return true;
			}

			private static void PlaceLeg(ref MotionDatabase database, DynamicBuffer<MmdLocalPose> poses, int foot, int calf, int thigh,
				float3 goal, quaternion footRotation) {
				float3 hip = MmdSampleFootPose.Model(ref database, poses, thigh).c3.xyz;
				float3 knee = MmdSampleFootPose.Model(ref database, poses, calf).c3.xyz;
				float3 ankle = MmdSampleFootPose.Model(ref database, poses, foot).c3.xyz;
				float upper = math.distance(hip, knee);
				float lower = math.distance(knee, ankle);
				float distance = math.clamp(math.distance(hip, goal), math.abs(upper - lower) + 0.00001f, upper + lower - 0.00001f);
				float3 direction = math.normalizesafe(goal - hip);
				goal = hip + direction * distance;
				float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
				float3 bend = knee - hip;
				bend -= direction * math.dot(bend, direction);
				float3 hint = new float3(0f, 0f, 1f);
				hint -= direction * math.dot(hint, direction);
				bend = math.normalizesafe(bend, math.normalizesafe(hint, new float3(1f, 0f, 0f)));
				float3 kneeGoal = hip + direction * along + bend * math.sqrt(math.max(0f, upper * upper - along * along));
				SetRotation(ref database, poses, thigh, math.mul(FromTo(knee - hip, kneeGoal - hip), ModelRotation(ref database, poses, thigh)));
				knee = MmdSampleFootPose.Model(ref database, poses, calf).c3.xyz;
				ankle = MmdSampleFootPose.Model(ref database, poses, foot).c3.xyz;
				SetRotation(ref database, poses, calf, math.mul(FromTo(ankle - knee, goal - knee), ModelRotation(ref database, poses, calf)));
				SetRotation(ref database, poses, foot, footRotation);
			}

			private static float3 Correction(float3 point, in MmdSampleTraversalTarget target, float skin) {
				float3 forward = math.forward(target.Rotation);
				float depth = math.dot(point - target.Ledge, forward) + skin;
				float lift = target.Ledge.y + skin - point.y;
				if (depth <= 0f || lift <= 0f) {
					return float3.zero;
				}
				// Slide the foot up the face, then continuously around the top corner.
				// Normalizing the blend reaches at least one surface instead of leaving
				// a partially corrected point inside both the wall and its top.
				float amount = math.smoothstep(0.16f, 0.02f, lift);
				float normalization = math.max(amount, 1f - amount);
				return -forward * (depth * (1f - amount) / normalization) + math.up() * (lift * amount / normalization);
			}

			private static quaternion ModelRotation(ref MotionDatabase database, in DynamicBuffer<MmdLocalPose> poses, int bone) {
				quaternion result = quaternion.identity;
				for (int current = bone; current >= 0; current = database.Parents[current]) {
					result = math.mul(poses[current].Value.Rotation, result);
				}
				return result;
			}

			private static void SetRotation(ref MotionDatabase database, DynamicBuffer<MmdLocalPose> poses, int bone, quaternion rotation) {
				var pose = poses[bone].Value;
				pose.Rotation = math.normalize(math.mul(math.inverse(ModelRotation(ref database, poses, database.Parents[bone])), rotation));
				poses[bone] = new MmdLocalPose { Value = pose };
			}

			private static quaternion FromTo(float3 from, float3 to) {
				float3 first = math.normalizesafe(from);
				float3 second = math.normalizesafe(to);
				float scalar = 1f + math.dot(first, second);
				if (scalar < 0.00001f) {
					float3 axis = math.cross(first, math.abs(first.y) < 0.9f ? math.up() : new float3(1f, 0f, 0f));
					return quaternion.AxisAngle(math.normalize(axis), math.PI);
				}
				return math.normalize(new quaternion(new float4(math.cross(first, second), scalar)));
			}
		}
	}
}
