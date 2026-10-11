using Unity.Entities;

namespace ColInt.MMD.Presentation.Components {
	[InternalBufferCapacity(0)]
	public struct MmdSkeletonBone : IBufferElementData {
		public int ParentIndex;
		public int PathHash;
	}
}
