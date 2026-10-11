using System.Runtime.CompilerServices;
using ColInt.MMD.Debugging.Data;
using ColInt.MMD.MotionMatching.Components;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Debugging.Helpers {
	public static class MmdTrajectoryDebugGeometry {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryReadQueryPoint(NativeArray<MmdSearchQueryElement>.ReadOnly query, in LocalTransform transform, int index, float height, out MmdTrajectoryDebugPoint point) {
			point = default;
			if (index < 0 || index >= 3 || query.Length < 12) {
				return false;
			}
			int offset = index * 4;
			float3 position = new float3(query[offset].Value, 0f, query[offset + 1].Value);
			float3 forward = new float3(query[offset + 2].Value, 0f, query[offset + 3].Value);
			if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(forward)) || !math.isfinite(height)) {
				return false;
			}
			point = ToWorld(transform, position, forward, height);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MmdTrajectoryDebugPoint ToWorld(in LocalTransform transform, float3 localPosition, float3 localForward, float height) {
			// Trajectory/root-motion distances are metres, independent of visual character scale.
			float3 position = transform.Position + math.rotate(transform.Rotation, localPosition) + new float3(0f, height, 0f);
			float3 forward = math.normalizesafe(math.rotate(transform.Rotation, localForward));
			return new MmdTrajectoryDebugPoint(position, forward);
		}
	}
}
