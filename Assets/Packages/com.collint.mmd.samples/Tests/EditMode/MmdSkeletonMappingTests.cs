using ColInt.MMD.Graphics.Helpers;
using ColInt.MMD.Presentation.Components;
using NUnit.Framework;
using Unity.Collections;

namespace ColInt.MMD.Tests {
	public class MmdSkeletonMappingTests {
		[Test]
		public void MapsReorderedBranchesAndIgnoresUnusedDatabaseNodes() {
			using var source = new NativeArray<MmdSkeletonBone>(new[] { Bone(1, -1), Bone(2, 0), Bone(3, 1), Bone(99, -1), Bone(4, 0) }, Allocator.Temp);
			using var target = new NativeArray<MmdSkeletonBone>(new[] { Bone(1, -1), Bone(4, 0), Bone(2, 0), Bone(3, 2) }, Allocator.Temp);
			using var mapping = new NativeArray<int>(target.Length, Allocator.Temp);
			Assert.That(MmdSkeletonMapping.TryCreate(source, target, mapping), Is.True);
			Assert.That(mapping.ToArray(), Is.EqualTo(new[] { 0, 4, 1, 2 }));
		}

		[TestCase(3, 1)]
		[TestCase(2, -1)]
		[TestCase(2, 1)]
		[TestCase(2, 3)]
		public void RejectsMissingBonesAndInvalidHierarchy(int hash, int parent) {
			using var source = new NativeArray<MmdSkeletonBone>(new[] { Bone(1, -1), Bone(2, 0) }, Allocator.Temp);
			using var target = new NativeArray<MmdSkeletonBone>(new[] { Bone(1, -1), Bone(hash, parent) }, Allocator.Temp);
			using var mapping = new NativeArray<int>(target.Length, Allocator.Temp);
			Assert.That(MmdSkeletonMapping.TryCreate(source, target, mapping), Is.False);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void RejectsAmbiguousBonePaths(bool duplicateSource) {
			using var source = new NativeArray<MmdSkeletonBone>(new[] { Bone(1, -1), Bone(duplicateSource ? 1 : 2, 0) }, Allocator.Temp);
			using var target = new NativeArray<MmdSkeletonBone>(new[] { Bone(1, -1), Bone(duplicateSource ? 2 : 1, 0) }, Allocator.Temp);
			using var mapping = new NativeArray<int>(target.Length, Allocator.Temp);
			Assert.That(MmdSkeletonMapping.TryCreate(source, target, mapping), Is.False);
		}

		private static MmdSkeletonBone Bone(int hash, int parent) {
			return new MmdSkeletonBone { PathHash = hash, ParentIndex = parent };
		}
	}
}
