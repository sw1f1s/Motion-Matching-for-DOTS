using ColInt.MMD.Playback.Systems;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Trajectory.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(MmdSampleSearchQueryGroup))]
	public partial class MmdSampleMotionFeedbackGroup : ComponentSystemGroup { }
}
