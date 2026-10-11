using ColInt.MMD.Actions.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Physics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(MmdActionGroup))]
	public partial class MmdTrajectoryConstraintGroup : ComponentSystemGroup { }
}
