using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Components {
	// Previous displayed positions in the same model space as baked pose features.
	[InternalBufferCapacity(2)]
	public struct MmdPoseFeatureHistory : IBufferElementData {
		public float3 Position;
	}
}
