using ColInt.MMD.Physics.Systems;
using Unity.Entities;
using Unity.Physics.Systems;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MmdPhysicsPreparationGroup))]
	[UpdateBefore(typeof(PhysicsSystemGroup))]
	public partial class MmdSampleMovementGroup : ComponentSystemGroup { }
}
