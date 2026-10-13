using ColInt.MMD.MotionMatching.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace ColInt.MMD.Tests {
	[BurstCompile(CompileSynchronously = true)]
	public struct BitMaskTestJob : IJob {
		public NativeArray<ulong> Left;
		[ReadOnly] public NativeArray<ulong> Right;

		public void Execute() {
			var left = new BitMask(Left, 1000);
			var right = new BitMask(Right, 1000);
			left.Set(63);
			left.Or(right);
		}
	}
}
