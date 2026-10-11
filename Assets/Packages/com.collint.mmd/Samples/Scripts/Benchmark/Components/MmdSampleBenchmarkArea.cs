using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Benchmark.Components {
	public struct MmdSampleBenchmarkArea : IComponentData {
		public float3 Center;
		public float2 HalfSize;
		public float ArrivalRadius;
	}
}
