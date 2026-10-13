using ColInt.MMD.Playback.Systems;
using ColInt.MMD.RootMotion.Systems;
using ColInt.MMD.Samples.Character.Movement.Facing.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdSampleFacingGroup))]
	[UpdateBefore(typeof(MmdRootApplyGroup))]
	public partial class MmdSampleSlideMotionGroup : ComponentSystemGroup { }
}
