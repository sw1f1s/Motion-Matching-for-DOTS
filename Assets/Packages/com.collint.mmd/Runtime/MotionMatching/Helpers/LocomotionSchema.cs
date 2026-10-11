using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Helpers {
	public static class LocomotionSchema {
		public const int Dimension = 27;
		public static readonly float3 PredictionTimes = new float3(0.2f, 0.4f, 0.6f);

		public static void WriteTrajectory(DynamicBuffer<MmdSearchQueryElement> query, float3 localVelocity, float yawRate) {
			for (int i = 0; i < 3; i++) {
				float time = PredictionTimes[i];
				quaternion rotation = quaternion.RotateY(yawRate * time);
				// Constant velocity prediction. Host may overwrite it with navigation/collision-aware samples.
				float3 position = localVelocity * time;
				float3 forward = math.rotate(rotation, new float3(0f, 0f, 1f));
				query[i * 4] = new MmdSearchQueryElement { Value = position.x };
				query[i * 4 + 1] = new MmdSearchQueryElement { Value = position.z };
				query[i * 4 + 2] = new MmdSearchQueryElement { Value = forward.x };
				query[i * 4 + 3] = new MmdSearchQueryElement { Value = forward.z };
			}
			query[12] = new MmdSearchQueryElement { Value = localVelocity.x };
			query[13] = new MmdSearchQueryElement { Value = localVelocity.y };
			query[14] = new MmdSearchQueryElement { Value = localVelocity.z };
		}

		public static void WritePose(ref MotionDatabase database, in MmdPlayback playback, DynamicBuffer<MmdSearchQueryElement> query,
			in DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdPoseFeatureHistory> history, float deltaTime) {
			if (database.LeftFootBone < 0 || deltaTime <= 0f) {
				// Use sampled features when foot bindings or a positive sampling interval are unavailable.
				WritePose(ref database, playback, query);
				return;
			}
			bool hasHistory = history.Length == 2;
			if (!hasHistory) {
				history.ResizeUninitialized(2);
			}
			int frame = MmdAnimationMath.FrameAt(ref database, playback.ClipIndex, playback.Time);
			float local = MmdAnimationMath.LocalTime(database.Clips[playback.ClipIndex], playback.Time);
			float amount = math.saturate((local - database.Frames[frame].Time) / (database.Frames[frame + 1].Time - database.Frames[frame].Time));
			float previousTime = playback.Time - deltaTime * math.max(0f, playback.Speed);
			for (int foot = 0; foot < 2; foot++) {
				int bone = foot == 0 ? database.LeftFootBone : database.RightFootBone;
				float3 position = DisplayedPosition(ref database, poses, bone);
				int feature = 15 + foot * 6;
				float3 velocity = float3.zero;
				for (int axis = 0; axis < 3; axis++) {
					float bakedVelocity = math.lerp(database.Features[frame * Dimension + feature + 3 + axis],
						database.Features[(frame + 1) * Dimension + feature + 3 + axis], amount);
					velocity[axis] = bakedVelocity * math.max(0f, playback.Speed);
				}
				if (hasHistory) {
					// Retain the bake's velocity convention while adding the displayed blend's motion.
					// A backward finite difference alone lags the bake's forward difference by half a sample,
					// repeatedly choosing an earlier foot phase even after a transition has settled.
					float3 sampled = SampledPosition(ref database, playback.ClipIndex, playback.Time, bone);
					float3 previousSampled = SampledPosition(ref database, playback.ClipIndex, previousTime, bone);
					velocity += (position - history[foot].Position - (sampled - previousSampled)) / deltaTime;
				}
				for (int axis = 0; axis < 3; axis++) {
					query[feature + axis] = new MmdSearchQueryElement { Value = position[axis] };
					query[feature + 3 + axis] = new MmdSearchQueryElement { Value = velocity[axis] };
				}
				history[foot] = new MmdPoseFeatureHistory { Position = position };
			}
		}

		public static void WritePose(ref MotionDatabase database, in MmdPlayback playback, DynamicBuffer<MmdSearchQueryElement> query) {
			int frame = MmdAnimationMath.FrameAt(ref database, playback.ClipIndex, playback.Time);
			float time = MmdAnimationMath.LocalTime(database.Clips[playback.ClipIndex], playback.Time);
			float amount = math.saturate((time - database.Frames[frame].Time) / (database.Frames[frame + 1].Time - database.Frames[frame].Time));
			for (int i = 15; i < Dimension; i++) {
				float value = math.lerp(database.Features[frame * Dimension + i], database.Features[(frame + 1) * Dimension + i], amount);
				query[i] = new MmdSearchQueryElement { Value = value * (IsVelocityFeature(i) ? math.clamp(playback.Speed, 0f, 1f) : 1f) };
			}
		}

		// Interpolate the baked 0.2/0.4/0.6-second path at the actual playback time.
		// Other schemas keep their original feature values.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ReadFeature(ref MotionDatabase database, int frame, int feature, float speed) {
			int offset = frame * database.FeatureCount;
			float value = database.Features[offset + feature];
			if (database.FeatureCount != Dimension || speed == 1f) {
				return value;
			}
			if (feature >= 12) {
				return IsVelocityFeature(feature) ? value * speed : value;
			}
			int component = feature % 4;
			float sample = (feature / 4 + 1) * speed;
			int upper = math.min(2, (int)math.floor(sample));
			float fraction = sample - upper;
			float2 lowerPosition = upper == 0 ? float2.zero : new float2(database.Features[offset + (upper - 1) * 4], database.Features[offset + (upper - 1) * 4 + 1]);
			float2 upperPosition = new float2(database.Features[offset + upper * 4], database.Features[offset + upper * 4 + 1]);
			if (component < 2) {
				return math.lerp(lowerPosition, upperPosition, fraction)[component];
			}
			float2 lowerFacing = upper == 0 ? new float2(0f, 1f) : new float2(database.Features[offset + (upper - 1) * 4 + 2], database.Features[offset + (upper - 1) * 4 + 3]);
			float2 upperFacing = new float2(database.Features[offset + upper * 4 + 2], database.Features[offset + upper * 4 + 3]);
			return math.normalizesafe(math.lerp(lowerFacing, upperFacing, fraction), new float2(0f, 1f))[component - 2];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsVelocityFeature(int feature) {
			return feature >= 12 && feature <= 14 || feature >= 18 && feature <= 20 || feature >= 24 && feature <= 26;
		}

		private static float3 DisplayedPosition(ref MotionDatabase database, in DynamicBuffer<MmdLocalPose> poses, int bone) {
			float4x4 model = float4x4.identity;
			while (bone >= 0) {
				JointPose pose = poses[bone].Value;
				model = math.mul(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), model);
				bone = database.Parents[bone];
			}
			return model.c3.xyz;
		}

		private static float3 SampledPosition(ref MotionDatabase database, int clip, float time, int bone) {
			float4x4 model = float4x4.identity;
			while (bone >= 0) {
				JointPose pose = MmdAnimationMath.PoseAt(ref database, clip, time, bone);
				model = math.mul(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), model);
				bone = database.Parents[bone];
			}
			return model.c3.xyz;
		}

	}

}
