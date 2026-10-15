using ColInt.MMD.Database.Data;
using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	[InternalBufferCapacity(0)]
	public struct MmdComposedPose : IBufferElementData {
		public JointPose Value;
	}
}
