using Unity.Entities;

namespace ColInt.MMD.Playback.Data {
	// The external owner retains the source database and this definition until all readers are disposed.
	public struct MmdAnimationLayersDefinition {
		public int PoseCount;
		public BlobArray<MmdAnimationLayerDefinition> Layers;
	}
}
