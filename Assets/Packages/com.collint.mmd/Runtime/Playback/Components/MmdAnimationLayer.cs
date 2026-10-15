using ColInt.MMD.Playback.Data;
using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	[InternalBufferCapacity(0)]
	public struct MmdAnimationLayer : IBufferElementData {
		public int ClipIndex;
		public int MatchedFrame;
		public float SearchCountdown;
		public float TransitionElapsed;
		public float Time;
		public float Weight;
		public MmdAnimationLayerStatus Status;
	}
}
