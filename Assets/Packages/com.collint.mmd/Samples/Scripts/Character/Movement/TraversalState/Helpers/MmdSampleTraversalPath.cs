using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public static class MmdSampleTraversalPath {
		public static float3 Position(ref MotionDatabase database, in MmdSampleTraversalTarget target, in MmdSampleTraversalClip clip, float time, bool continueMoving = false) {
			float sampleTime = math.clamp(time, clip.StartTime, clip.Duration);
			float3 origin = MmdAnimationMath.RootAt(ref database, clip.ClipIndex, clip.StartTime).pos;
			float3 root = MmdAnimationMath.RootAt(ref database, clip.ClipIndex, sampleTime).pos;
			float3 near = MmdAnimationMath.RootAt(ref database, clip.ClipIndex, clip.ClearanceTime).pos;
			float3 far = MmdAnimationMath.RootAt(ref database, clip.ClipIndex, clip.TransferTime).pos;
			float3 end = MmdAnimationMath.RootAt(ref database, clip.ClipIndex, clip.Duration).pos;
			float3 forward = math.forward(target.Rotation);
			float nearDistance = math.max(0f, math.dot(target.Front - target.Start, forward));
			float farDistance = math.dot(target.AboveEnd - target.Start, forward);
			float endDistance = math.dot(target.End - target.Start, forward);
			float horizontal;
			if (target.Across) {
				horizontal = Map(root.z, new float4(origin.z, near.z, far.z, end.z),
					new float4(0f, nearDistance, farDistance, endDistance));
			} else {
				// A moving pull-up ends at the ledge with momentum, without extending its destination.
				float exitSpeed = clip.EntrySpeed > 1.5f ? clip.ExitSpeed : 0f;
				float transferSpeed = math.max(0f, endDistance - nearDistance) / math.max(0.04f, clip.Duration - clip.ClearanceTime);
				exitSpeed = math.min(exitSpeed, transferSpeed * 2f);
				horizontal = Map(sampleTime, new float4(clip.StartTime, clip.ClearanceTime, clip.Duration, clip.Duration + 0.04f),
					new float4(0f, nearDistance, endDistance, endDistance + exitSpeed * 0.04f));
				if (continueMoving) {
					// Consume the entire final tick, including its fraction beyond the fitted duration.
					// Clamping that fraction to End makes exit speed depend on the approach's timing.
					horizontal += Tangent(transferSpeed, exitSpeed) * math.max(0f, time - clip.Duration);
				}
			}
			float fraction = math.saturate((root.y - origin.y) / math.max(0.001f, clip.RootHeight - origin.y));
			float baseHeight = target.Start.y;
			if (target.Across && sampleTime > clip.PeakTime) {
				baseHeight = math.lerp(target.Start.y, target.End.y, Ease((sampleTime - clip.PeakTime) / math.max(0.04f, clip.Duration - clip.PeakTime)));
			}
			float height = math.lerp(baseHeight, target.AboveFront.y + 0.04f, fraction);
			if (!target.Across && sampleTime >= clip.PeakTime) {
				// After clearing the top, settle monotonically onto its support. Source root
				// bobbing must not sink the capsule or lift it again on the final frame.
				height = math.lerp(target.AboveFront.y + 0.04f, target.End.y,
					Ease((sampleTime - clip.PeakTime) / math.max(0.001f, clip.Duration - clip.PeakTime)));
			}
			float3 position = target.Start + forward * horizontal;
			position.y = sampleTime >= clip.Duration ? target.End.y : height;
			return position;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static quaternion Rotation(in MmdSampleTraversalTarget target, float time, float alignmentDuration) {
			return math.slerp(target.StartRotation, target.Rotation, Ease(time / alignmentDuration));
		}

		private static float Map(float value, float4 source, float4 destination) {
			// Harmonic tangents preserve monotonic motion through each obstacle boundary.
			float3 span = math.max(source.yzw - source.xyz, 0.001f);
			float3 slope = math.max(destination.yzw - destination.xyz, 0f) / span;
			float nearSlope = Tangent(slope.x, slope.y);
			float farSlope = Tangent(slope.y, slope.z);
			if (value <= source.y) {
				return Segment(value, source.x, source.y, destination.x, destination.y, slope.x, nearSlope);
			}
			if (value <= source.z) {
				return Segment(value, source.y, source.z, destination.y, destination.z, nearSlope, farSlope);
			}
			return Segment(value, source.z, source.w, destination.z, destination.w, farSlope, slope.z);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Tangent(float a, float b) {
			return a > 0f && b > 0f ? 2f * a * b / (a + b) : 0f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Segment(float value, float start, float end, float from, float to, float firstSlope, float lastSlope) {
			float span = math.max(0.001f, end - start);
			float t = math.saturate((value - start) / span);
			float t2 = t * t;
			float t3 = t2 * t;
			return (2f * t3 - 3f * t2 + 1f) * from + (t3 - 2f * t2 + t) * span * firstSlope +
				(-2f * t3 + 3f * t2) * to + (t3 - t2) * span * lastSlope;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Ease(float value) {
			float t = math.saturate(value);
			return t * t * t * (t * (t * 6f - 15f) + 10f);
		}
	}
}
