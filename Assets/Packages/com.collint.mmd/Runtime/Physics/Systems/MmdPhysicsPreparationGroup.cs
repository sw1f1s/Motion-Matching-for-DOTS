using ColInt.MMD.Playback.Systems;
using Unity.Entities;
using Unity.Physics.Systems;

namespace ColInt.MMD.Physics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(PhysicsSystemGroup))]
	public partial class MmdPhysicsPreparationGroup : ComponentSystemGroup { }

}
