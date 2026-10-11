using System.Runtime.CompilerServices;
using ColInt.MMD.Samples.Benchmark.Components;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Benchmark.Helpers {
	public static class MmdSampleBenchmarkRandom {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Random CreateStream(int seed, int characterIndex) {
			return new Random(math.max(1u, math.hash(new uint2(unchecked((uint)seed), unchecked((uint)characterIndex)))));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 Position(ref Random random, in MmdSampleBenchmarkArea area) {
			float2 point = random.NextFloat2(-area.HalfSize, area.HalfSize);
			return area.Center + new float3(point.x, 0f, point.y);
		}

		public static float3 Target(ref Random random, in MmdSampleBenchmarkArea area, float3 position) {
			float minimumDistance = area.ArrivalRadius * 3f;
			for (int attempt = 0; attempt < 8; attempt++) {
				float3 target = Position(ref random, area);
				if (math.distancesq(target.xz, position.xz) > minimumDistance * minimumDistance) {
					return target;
				}
			}
			// A bounded retry keeps very small areas from spinning in the target picker.
			float2 direction = math.select(new float2(1f), new float2(-1f), position.xz >= area.Center.xz);
			return area.Center + new float3(direction.x * area.HalfSize.x, 0f, direction.y * area.HalfSize.y);
		}
	}
}
