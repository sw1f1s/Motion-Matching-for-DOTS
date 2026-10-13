using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Benchmark.Components {
	public struct MmdSampleBenchmarkAgent : IComponentData {
		public Random Random;
		public float3 Target;
		public uint CompletedTargets;
	}
}
