using ColInt.MMD.Actions.Systems;
using ColInt.MMD.MotionMatching.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdActionGroup))]
	[UpdateBefore(typeof(MmdSearchGroup))]
	public partial class MmdSampleSlideAnimationGroup : ComponentSystemGroup { }
}
