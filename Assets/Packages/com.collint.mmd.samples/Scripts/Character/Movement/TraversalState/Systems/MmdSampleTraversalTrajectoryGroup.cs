using ColInt.MMD.Physics.Systems;
using ColInt.MMD.Playback.Systems;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdTrajectoryConstraintGroup))]
	[UpdateBefore(typeof(MmdSampleLocomotionSelectionGroup))]
	public partial class MmdSampleTraversalTrajectoryGroup : ComponentSystemGroup { }
}
