using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ColInt.MMD.Graphics.Helpers {
	public static class MmdGpuVisualBinding {
		public static MmdGpuRenderStatus Validate(Entity owner, Entity prefab, BufferLookup<MmdSkeletonBone> skeletons,
			BufferLookup<MmdLocalPose> poses, BufferLookup<MmdModelMatrix> matrices, BufferLookup<LinkedEntityGroup> linked,
			ComponentLookup<MmdSkinSource> sources, BufferLookup<MmdSkinBinding> bindings) {
			if (!skeletons.HasBuffer(prefab) || !poses.HasBuffer(prefab) || !linked.HasBuffer(prefab)) {
				return MmdGpuRenderStatus.InvalidRig;
			}
			var skeleton = skeletons[prefab];
			if (skeleton.Length == 0 || poses[prefab].Length != skeleton.Length || !ValidateSkins(prefab, skeleton.Length, sources, bindings, linked)) {
				return MmdGpuRenderStatus.InvalidRig;
			}
			if (!skeletons.HasBuffer(owner) || !poses.HasBuffer(owner) || !matrices.HasBuffer(owner)) {
				return MmdGpuRenderStatus.WaitingForPose;
			}
			var existing = skeletons[owner];
			int count = poses[owner].Length;
			if (count == 0) {
				return MmdGpuRenderStatus.WaitingForPose;
			}
			if (count != existing.Length) {
				return MmdGpuRenderStatus.IncompatibleSkeleton;
			}
			using var mapping = new NativeArray<int>(skeleton.Length, Allocator.Temp);
			return MmdSkeletonMapping.TryCreate(existing.AsNativeArray(), skeleton.AsNativeArray(), mapping)
				? MmdGpuRenderStatus.Ready : MmdGpuRenderStatus.IncompatibleSkeleton;
		}

		public static MmdGpuRenderStatus Attach(EntityCommandBuffer commands, Entity owner, LocalTransform transform, Entity visualRoot,
			ComponentLookup<LocalToWorld> transforms, ComponentLookup<MmdSkinSource> sources, BufferLookup<LinkedEntityGroup> linked,
			BufferLookup<MmdSkeletonBone> skeletons, BufferLookup<MmdSkinBinding> bindings) {
			if (!skeletons.HasBuffer(owner) || !skeletons.HasBuffer(visualRoot)) {
				return MmdGpuRenderStatus.IncompatibleSkeleton;
			}
			var skeleton = skeletons[visualRoot];
			using var mapping = new NativeArray<int>(skeleton.Length, Allocator.Temp);
			if (!MmdSkeletonMapping.TryCreate(skeletons[owner].AsNativeArray(), skeleton.AsNativeArray(), mapping)) {
				return MmdGpuRenderStatus.IncompatibleSkeleton;
			}
			if (!ValidateSkins(visualRoot, skeleton.Length, sources, bindings, linked)) {
				return MmdGpuRenderStatus.InvalidRig;
			}
			if (!transforms.HasComponent(owner)) {
				commands.AddComponent(owner, new LocalToWorld { Value = transform.ToMatrix() });
			}
			if (!linked.HasBuffer(owner)) {
				commands.AddBuffer<LinkedEntityGroup>(owner).Add(new LinkedEntityGroup { Value = owner });
			}
			commands.AddComponent(visualRoot, new Parent { Value = owner });
			commands.SetComponent(visualRoot, LocalTransform.Identity);
			foreach (var child in linked[visualRoot]) {
				if (sources.TryGetComponent(child.Value, out var source)) {
					var sourceBindings = bindings[child.Value];
					var mappedBindings = commands.SetBuffer<MmdSkinBinding>(child.Value);
					foreach (var binding in sourceBindings) {
						var mapped = binding;
						mapped.BoneIndex = mapping[binding.BoneIndex];
						mappedBindings.Add(mapped);
					}
					source.PoseEntity = owner;
					commands.SetComponent(child.Value, source);
				}
				commands.AppendToBuffer(owner, child);
			}
			commands.RemoveComponent<MmdLocalPose>(visualRoot);
			commands.RemoveComponent<MmdPlayback>(visualRoot);
			commands.RemoveComponent<MmdSkeletonBone>(visualRoot);
			commands.RemoveComponent<MmdModelMatrix>(visualRoot);
			return MmdGpuRenderStatus.Ready;
		}

		private static bool ValidateSkins(Entity rig, int boneCount, ComponentLookup<MmdSkinSource> sources,
			BufferLookup<MmdSkinBinding> bindings, BufferLookup<LinkedEntityGroup> linked) {
			foreach (var child in linked[rig]) {
				if (!sources.TryGetComponent(child.Value, out var source)) {
					continue;
				}
				if (source.PoseEntity != rig || !bindings.HasBuffer(child.Value)) {
					return false;
				}
				foreach (var binding in bindings[child.Value]) {
					if (binding.BoneIndex < 0 || binding.BoneIndex >= boneCount) {
						return false;
					}
				}
			}
			return true;
		}
	}
}
