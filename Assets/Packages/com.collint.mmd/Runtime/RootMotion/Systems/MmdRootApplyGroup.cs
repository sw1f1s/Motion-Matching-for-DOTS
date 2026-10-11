using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.RootMotion.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdPlaybackGroup))]
	public partial class MmdRootApplyGroup : ComponentSystemGroup { }
}
