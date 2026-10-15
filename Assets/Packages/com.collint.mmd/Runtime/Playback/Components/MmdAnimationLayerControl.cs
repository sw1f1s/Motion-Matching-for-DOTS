using ColInt.MMD.Playback.Data;
using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	[InternalBufferCapacity(0)]
	public struct MmdAnimationLayerControl : IBufferElementData {
		public MmdAnimationLayerSource Source;
		public int GroupId;
		public float Speed;
		public float TargetWeight;
		public float FadeDuration;
	}
}
