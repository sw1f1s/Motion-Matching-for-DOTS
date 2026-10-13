using ColInt.MMD.Playback.Systems;
using ColInt.MMD.RootMotion.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Facing.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdInertializationGroup))]
	[UpdateBefore(typeof(MmdRootApplyGroup))]
	public partial class MmdSampleFacingGroup : ComponentSystemGroup { }
}
