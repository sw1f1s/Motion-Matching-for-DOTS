using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	public struct MmdPlayback : IComponentData {
		public int ClipIndex;
		public float Time;
		public float Speed;
		public uint Revision;
		public float SearchCountdown;
		public float TransitionElapsed;
		public int MatchedFrame;
	}

}
