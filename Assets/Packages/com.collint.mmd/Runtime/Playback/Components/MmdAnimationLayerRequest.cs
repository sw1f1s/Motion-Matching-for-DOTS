using ColInt.MMD.Playback.Data;
using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	[InternalBufferCapacity(0)]
	public struct MmdAnimationLayerRequest : IBufferElementData {
		public int LayerIndex;
		public MmdAnimationLayerOperation Operation;
		public int ClipIndex;
		public int GroupId;
		public float Time;
		public float Speed;
		public float Weight;
		public float FadeDuration;
	}
}
