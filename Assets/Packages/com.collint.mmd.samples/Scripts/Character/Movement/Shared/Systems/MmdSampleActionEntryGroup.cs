using ColInt.MMD.Actions.Systems;
using ColInt.MMD.Playback.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateAfter(typeof(MmdActionGroup))]
	[UpdateBefore(typeof(MmdPlaybackGroup))]
	public partial class MmdSampleActionEntryGroup : ComponentSystemGroup { }
}
