using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateGroup))]
	[UpdateAfter(typeof(MmdSampleStateFinishGroup))]
	public partial class MmdSampleStateCommitGroup : ComponentSystemGroup { }
}
