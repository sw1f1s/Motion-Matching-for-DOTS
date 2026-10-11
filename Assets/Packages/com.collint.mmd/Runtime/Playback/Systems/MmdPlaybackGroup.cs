using ColInt.MMD.MotionMatching.Systems;
using ColInt.MMD.RootMotion.Systems;
using Unity.Entities;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdSearchGroup))]
	[UpdateBefore(typeof(MmdRootApplyGroup))]
	public partial class MmdPlaybackGroup : ComponentSystemGroup { }
}
