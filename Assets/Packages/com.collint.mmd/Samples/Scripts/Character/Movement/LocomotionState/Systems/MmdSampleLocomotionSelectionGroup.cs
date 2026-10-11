using ColInt.MMD.Actions.Systems;
using ColInt.MMD.Physics.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.LocomotionState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdTrajectoryConstraintGroup))]
	[UpdateBefore(typeof(MmdActionGroup))]
	public partial class MmdSampleLocomotionSelectionGroup : ComponentSystemGroup { }
}
