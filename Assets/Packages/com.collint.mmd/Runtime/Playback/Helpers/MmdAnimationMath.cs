using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Helpers {
	public static class MmdAnimationMath {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float LocalTime(in MotionClip clip, float time) {
			return clip.Loop ? time - math.floor(time / clip.Duration) * clip.Duration : math.clamp(time, 0f, clip.Duration);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FrameAt(ref MotionDatabase database, int clipIndex, float time) {
			ref MotionClip clip = ref database.Clips[clipIndex];
			return clip.FirstFrame + math.min((int)math.floor(LocalTime(clip, time) * clip.SampleRate), clip.FrameCount - 2);
		}

		public static RigidTransform RootAt(ref MotionDatabase database, int clipIndex, float time) {
			ref MotionClip clip = ref database.Clips[clipIndex];
			float local = LocalTime(clip, time);
			RigidTransform sampled = RootWithin(ref database, clipIndex, local);
			if (!clip.Loop) {
				return sampled;
			}
			int loops = (int)math.floor(time / clip.Duration);
			RigidTransform start = database.Frames[clip.FirstFrame].Root;
			RigidTransform end = database.Frames[clip.FirstFrame + clip.FrameCount - 1].Root;
			RigidTransform cycle = math.mul(math.inverse(start), end);
			return math.mul(start, math.mul(Power(cycle, loops), math.mul(math.inverse(start), sampled)));
		}

		public static JointPose PoseAt(ref MotionDatabase database, int clipIndex, float time, int bone) {
			int frame = FrameAt(ref database, clipIndex, time);
			float local = LocalTime(database.Clips[clipIndex], time);
			float amount = math.saturate((local - database.Frames[frame].Time) /
				(database.Frames[frame + 1].Time - database.Frames[frame].Time));
			JointPose a = PoseSample(ref database, frame, bone);
			JointPose b = PoseSample(ref database, frame + 1, bone);
			return new JointPose {
				Position = math.lerp(a.Position, b.Position, amount),
				Rotation = math.slerp(a.Rotation, b.Rotation, amount),
				Scale = math.lerp(a.Scale, b.Scale, amount)
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JointPose PoseSample(ref MotionDatabase database, int frame, int bone) {
			int clipIndex = database.Frames[frame].ClipIndex;
			int sample = frame - database.Clips[clipIndex].FirstFrame;
			ref MotionPoseTrack track = ref database.PoseTracks[clipIndex * database.Parents.Length + bone];
			return new JointPose {
				Position = database.Positions[track.PositionOffset + ((track.AnimatedChannels & MotionPoseChannels.Position) != 0 ? sample : 0)],
				Rotation = MmdRotationPacking.Unpack(database.Rotations[track.RotationOffset + ((track.AnimatedChannels & MotionPoseChannels.Rotation) != 0 ? sample : 0)]),
				Scale = database.Scales[track.ScaleOffset + ((track.AnimatedChannels & MotionPoseChannels.Scale) != 0 ? sample : 0)]
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 Decay(float3 offset, float3 velocity, float elapsed, float duration) {
			if (duration <= 0f || elapsed >= duration) {
				return float3.zero;
			}
			float t = math.saturate(elapsed / duration);
			float t2 = t * t;
			float t3 = t2 * t;
			float t4 = t3 * t;
			float t5 = t4 * t;
			// Zero initial acceleration; position, velocity and acceleration reach zero at duration.
			return offset * (1f - 10f * t3 + 15f * t4 - 6f * t5)
				+ velocity * duration * (t - 6f * t3 + 8f * t4 - 3f * t5);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 RotationVector(quaternion rotation) {
			rotation = math.normalize(rotation);
			if (rotation.value.w < 0f) {
				rotation.value = -rotation.value;
			}
			float length = math.length(rotation.value.xyz);
			return length < 0.000001f ? rotation.value.xyz * 2f
				: rotation.value.xyz * (2f * math.atan2(length, rotation.value.w) / length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static quaternion Rotation(float3 vector) {
			float angle = math.length(vector);
			if (angle < 0.000001f) {
				return math.normalize(new quaternion(new float4(vector * 0.5f, 1f)));
			}
			return quaternion.AxisAngle(vector / angle, angle);
		}

		// Simulation owns the validated target and collision policy. Returns local delta.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RigidTransform WarpDelta(RigidTransform originalPrevious, RigidTransform originalNext,
			RigidTransform originalEnd, RigidTransform targetEnd, float previousProgress, float nextProgress) {
			RigidTransform correction = math.mul(targetEnd, math.inverse(originalEnd));
			RigidTransform before = math.mul(Blend(correction, previousProgress), originalPrevious);
			RigidTransform after = math.mul(Blend(correction, nextProgress), originalNext);
			return math.mul(math.inverse(before), after);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static RigidTransform Blend(RigidTransform transform, float progress) {
			float weight = math.saturate(progress);
			weight = weight * weight * (3f - 2f * weight);
			return new RigidTransform(math.slerp(quaternion.identity, transform.rot, weight), transform.pos * weight);
		}
		private static RigidTransform RootWithin(ref MotionDatabase database, int clipIndex, float local) {
			int frame = FrameAt(ref database, clipIndex, local);
			ref MotionClip clip = ref database.Clips[clipIndex];
			// A loop endpoint must sample the stored endpoint rather than wrap to its start.
			if (local >= clip.Duration) {
				return database.Frames[clip.FirstFrame + clip.FrameCount - 1].Root;
			}
			float amount = math.saturate((local - database.Frames[frame].Time) /
				(database.Frames[frame + 1].Time - database.Frames[frame].Time));
			RigidTransform a = database.Frames[frame].Root;
			RigidTransform b = database.Frames[frame + 1].Root;
			return new RigidTransform(math.slerp(a.rot, b.rot, amount), math.lerp(a.pos, b.pos, amount));
		}

		private static RigidTransform Power(RigidTransform transform, int exponent) {
			if (exponent < 0) {
				transform = math.inverse(transform);
				exponent = -exponent;
			}
			RigidTransform result = RigidTransform.identity;
			while (exponent > 0) {
				if ((exponent & 1) != 0) {
					result = math.mul(result, transform);
				}
				transform = math.mul(transform, transform);
				exponent >>= 1;
			}
			return result;
		}

	}
}
