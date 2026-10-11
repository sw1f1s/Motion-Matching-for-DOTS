using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Components {
	// World basis used when the local search trajectory was written, before root application.
	public struct MmdSearchQuerySpace : IComponentData {
		public float3 Position;
		public quaternion Rotation;
	}
}
