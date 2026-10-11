using ColInt.MMD.Actions.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.MotionMatching.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdActionGroup))]
	[UpdateBefore(typeof(MmdPlaybackGroup))]
	public partial class MmdSearchGroup : ComponentSystemGroup { }
}
