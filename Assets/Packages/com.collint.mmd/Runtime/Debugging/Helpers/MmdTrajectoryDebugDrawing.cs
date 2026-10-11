using ColInt.MMD.Debugging.Components;
using ColInt.MMD.Debugging.Data;
using Unity.Mathematics;
using UnityEngine;

namespace ColInt.MMD.Debugging.Helpers {
	public static class MmdTrajectoryDebugDrawing {
		public static readonly Color QueryColor = new Color(0.1f, 0.95f, 1f);
		public static readonly Color ClipColor = new Color(1f, 0.72f, 0.1f);

		public static void DrawPoint(in MmdTrajectoryDebugPoint point, float3 previous, in MmdTrajectoryDebug settings, Color color) {
#if UNITY_EDITOR
			Debug.DrawLine(previous, point.Position, color, 0f, false);
			const int segments = 12;
			float radius = math.max(0f, settings.PointRadius);
			for (int segment = 0; segment < segments; segment++) {
				float a = segment * (2f * math.PI / segments);
				float b = (segment + 1) * (2f * math.PI / segments);
				float3 start = point.Position + new float3(math.cos(a), 0f, math.sin(a)) * radius;
				float3 end = point.Position + new float3(math.cos(b), 0f, math.sin(b)) * radius;
				Debug.DrawLine(start, end, color, 0f, false);
			}
			float length = math.max(0f, settings.DirectionLength);
			float3 tip = point.Position + point.Forward * length;
			float3 right = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), point.Forward));
			Debug.DrawLine(point.Position, tip, color, 0f, false);
			Debug.DrawLine(tip, tip - point.Forward * length * 0.3f + right * length * 0.2f, color, 0f, false);
			Debug.DrawLine(tip, tip - point.Forward * length * 0.3f - right * length * 0.2f, color, 0f, false);
#endif
		}
	}
}
