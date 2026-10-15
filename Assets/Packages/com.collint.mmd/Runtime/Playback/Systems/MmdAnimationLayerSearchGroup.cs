using Unity.Entities;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdAnimationLayerGroup))]
	[UpdateAfter(typeof(MmdAnimationLayerControlGroup))]
	public partial class MmdAnimationLayerSearchGroup : ComponentSystemGroup { }
}
