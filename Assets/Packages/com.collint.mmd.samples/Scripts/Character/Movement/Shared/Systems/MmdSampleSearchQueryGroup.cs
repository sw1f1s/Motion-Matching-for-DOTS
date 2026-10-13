using ColInt.MMD.Physics.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(MmdTrajectoryConstraintGroup))]
	public partial class MmdSampleSearchQueryGroup : ComponentSystemGroup { }
}
