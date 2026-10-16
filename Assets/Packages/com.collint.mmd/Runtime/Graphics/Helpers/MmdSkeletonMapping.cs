using ColInt.MMD.Presentation.Components;
using Unity.Collections;

namespace ColInt.MMD.Graphics.Helpers {
	public static class MmdSkeletonMapping {
		public static bool TryCreate(NativeArray<MmdSkeletonBone> source, NativeArray<MmdSkeletonBone> target, NativeArray<int> mapping) {
			if (source.Length == 0 || target.Length == 0 || mapping.Length != target.Length) {
				return false;
			}
			using var indices = new NativeParallelHashMap<int, int>(source.Length, Allocator.Temp);
			using var visited = new NativeParallelHashSet<int>(target.Length, Allocator.Temp);
			for (int index = 0; index < source.Length; index++) {
				if (!indices.TryAdd(source[index].PathHash, index)) {
					return false;
				}
			}
			for (int index = 0; index < target.Length; index++) {
				if (!visited.Add(target[index].PathHash) || !indices.TryGetValue(target[index].PathHash, out int mapped)) {
					return false;
				}
				mapping[index] = mapped;
			}
			for (int index = 0; index < target.Length; index++) {
				int parent = target[index].ParentIndex;
				if (parent < -1 || parent >= target.Length || parent == index) {
					return false;
				}
				if (source[mapping[index]].ParentIndex != (parent < 0 ? -1 : mapping[parent])) {
					return false;
				}
			}
			return true;
		}
	}
}
