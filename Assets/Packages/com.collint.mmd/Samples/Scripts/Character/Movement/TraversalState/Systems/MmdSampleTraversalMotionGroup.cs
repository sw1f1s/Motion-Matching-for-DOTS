using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;
using Unity.Physics.Systems;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MmdSampleMovementGroup))]
	[UpdateBefore(typeof(PhysicsSystemGroup))]
	public partial class MmdSampleTraversalMotionGroup : ComponentSystemGroup { }
}
