using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateGroup))]
	[UpdateAfter(typeof(MmdSampleStateUpdateGroup))]
	public partial class MmdSampleStateBeginGroup : ComponentSystemGroup { }
}
