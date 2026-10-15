using ColInt.MMD.Playback.Data;
using Unity.Entities;

namespace ColInt.MMD.Playback.Components {
	public struct MmdAnimationLayersDefinitionReference : IComponentData {
		public BlobAssetReference<MmdAnimationLayersDefinition> Value;
	}
}
