using ColInt.MMD.MotionMatching.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Actions.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(MmdSearchGroup))]
	public partial class MmdActionGroup : ComponentSystemGroup { }
}
