using Unity.Mathematics;

namespace ColInt.MMD.Debugging.Data {
	public readonly struct MmdTrajectoryDebugPoint {
		public readonly float3 Position;
		public readonly float3 Forward;

		public MmdTrajectoryDebugPoint(float3 position, float3 forward) {
			Position = position;
			Forward = forward;
		}
	}
}
