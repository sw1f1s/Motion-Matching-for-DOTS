using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	// Optional transition smoothing, independent of rendering and root-motion ownership.
	public struct MmdInertialization : IComponentData {
		public float PoseDuration;
		public float StationaryPoseDuration;
		public float RootDuration;
	}
}
