using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
	public partial class MmdSamplePlayerInputGroup : ComponentSystemGroup { }
}
