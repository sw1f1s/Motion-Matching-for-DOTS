using ColInt.MMD.Playback.Systems;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Benchmark.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSimulationGroup))]
	[UpdateBefore(typeof(MmdSampleStateGroup))]
	[UpdateBefore(typeof(MmdSampleSearchQueryGroup))]
	public partial class MmdSampleBenchmarkIntentGroup : ComponentSystemGroup { }
}
