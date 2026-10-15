using ColInt.MMD.Database.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Components {
	// Layer-local transition history, before masks and layer weights are applied.
	[InternalBufferCapacity(0)]
	public struct MmdAnimationLayerPose : IBufferElementData {
		public JointPose Previous;
		public float3 PositionOffset;
		public float3 RotationOffset;
		public float3 ScaleOffset;
	}
}
