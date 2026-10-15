using Unity.Entities;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdAnimationLayerGroup), OrderFirst = true)]
	public partial class MmdAnimationLayerControlGroup : ComponentSystemGroup { }
}
