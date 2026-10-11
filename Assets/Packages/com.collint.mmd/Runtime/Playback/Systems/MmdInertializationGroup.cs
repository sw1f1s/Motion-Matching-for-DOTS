using ColInt.MMD.RootMotion.Systems;
using Unity.Entities;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdPlaybackGroup))]
	[UpdateBefore(typeof(MmdRootApplyGroup))]
	public partial class MmdInertializationGroup : ComponentSystemGroup { }
}
