using Unity.Entities;

namespace ColInt.MMD.MotionMatching.Components {
	// Buffer index selects a block of 64 group IDs; ID 1 is bit 0.
	[InternalBufferCapacity(1)]
	public struct MmdMotionGroup : IBufferElementData {
		public ulong Required;
		public ulong Excluded;
		public ulong StationaryPose;
	}
}
