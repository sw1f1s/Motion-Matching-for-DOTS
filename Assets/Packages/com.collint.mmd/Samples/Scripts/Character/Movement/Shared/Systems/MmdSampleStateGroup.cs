using ColInt.MMD.Playback.Systems;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(MmdSampleMotionFeedbackGroup))]
	public partial class MmdSampleStateGroup : ComponentSystemGroup { }
}
