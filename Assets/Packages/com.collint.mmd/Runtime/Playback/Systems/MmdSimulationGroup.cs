using Unity.Entities;

namespace ColInt.MMD.Playback.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	
	public partial class MmdSimulationGroup : ComponentSystemGroup { }
}
