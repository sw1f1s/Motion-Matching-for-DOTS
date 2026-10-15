using ColInt.MMD.RootMotion.Systems;
using Unity.Entities;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdInertializationGroup))]
	[UpdateAfter(typeof(MmdPlaybackGroup))]
	[UpdateAfter(typeof(MmdRootApplyGroup))]
	public partial class MmdAnimationLayerGroup : ComponentSystemGroup { }
}
