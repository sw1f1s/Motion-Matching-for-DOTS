using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Graphics.Components {
	[InternalBufferCapacity(0)]
	public struct MmdSkinBinding : IBufferElementData {
		public int BoneIndex;
		public float4x4 BindPose;
	}

}
