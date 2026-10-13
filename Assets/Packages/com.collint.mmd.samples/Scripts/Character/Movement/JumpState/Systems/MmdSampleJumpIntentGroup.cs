using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateGroup))]
	[UpdateAfter(typeof(MmdSampleStateGroundGroup))]
	[UpdateBefore(typeof(MmdSampleStateRequestGroup))]
	public partial class MmdSampleJumpIntentGroup : ComponentSystemGroup { }
}
