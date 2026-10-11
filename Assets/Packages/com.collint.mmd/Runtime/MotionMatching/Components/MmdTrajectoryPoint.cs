using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Components {
	// Host-predicted world-space samples. Physics may constrain them before search.
	public struct MmdTrajectoryPoint : IBufferElementData {
		public float Time;
		public float3 Position;
		public float3 Forward;
	}
}
