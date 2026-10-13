using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Playback.Helpers;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.Facing.Helpers {
	public static class MmdSampleFacing {
		public static bool IsHalfTurn(ref MotionDatabase database, in MotionClip clip) {
			if (clip.Loop) {
				return false;
			}
			quaternion first = database.Frames[clip.FirstFrame].Root.rot;
			quaternion last = database.Frames[clip.FirstFrame + clip.FrameCount - 1].Root.rot;
			float yaw = MmdAnimationMath.RotationVector(math.mul(math.inverse(first), last)).y;
			return math.abs(yaw) > 2.0943951f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float2 Resolve(float2 movement) {
			return math.normalizesafe(movement);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float YawError(quaternion rotation, float2 facing, float previousError = 0f) {
			if (math.lengthsq(facing) < 0.000001f) {
				return 0f;
			}
			float3 forward = math.rotate(rotation, new float3(0f, 0f, 1f));
			float2 current = math.normalizesafe(forward.xz, new float2(0f, 1f));
			float2 target = math.normalizesafe(facing);
			float error = math.atan2(current.y * target.x - current.x * target.y, math.dot(current, target));
			if (math.abs(error) > math.PI - 0.01f) {
				// At the antipode atan2's sign depends on roundoff. Keep the existing turn side.
				return math.sign(previousError == 0f ? 1f : previousError) * math.abs(error);
			}
			return error;
		}
	}
}
