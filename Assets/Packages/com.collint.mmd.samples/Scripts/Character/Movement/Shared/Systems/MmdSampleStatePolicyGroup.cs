using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateGroup))]
	[UpdateAfter(typeof(MmdSampleStateCleanupGroup))]
	public partial class MmdSampleStatePolicyGroup : ComponentSystemGroup { }
}
