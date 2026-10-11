using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Entities;
using Unity.Transforms;

namespace ColInt.MMD.Graphics.Helpers {
	public static class MmdGpuVisualBinding {
		public static MmdGpuRenderStatus Validate(Entity owner, Entity prefab, BufferLookup<MmdSkeletonBone> skeletons,
			BufferLookup<MmdLocalPose> poses, BufferLookup<MmdModelMatrix> matrices, BufferLookup<LinkedEntityGroup> linked) {
			if (!skeletons.HasBuffer(prefab) || !poses.HasBuffer(prefab) || !linked.HasBuffer(prefab)) {
				return MmdGpuRenderStatus.InvalidRig;
			}
			var skeleton = skeletons[prefab];
			if (!skeletons.HasBuffer(owner) || !poses.HasBuffer(owner) || !matrices.HasBuffer(owner)) {
				return MmdGpuRenderStatus.WaitingForPose;
			}
			var existing = skeletons[owner];
			if (existing.Length != skeleton.Length) {
				return MmdGpuRenderStatus.IncompatibleSkeleton;
			}
			for (int i = 0; i < skeleton.Length; i++) {
				if (existing[i].ParentIndex != skeleton[i].ParentIndex || existing[i].PathHash != skeleton[i].PathHash) {
					return MmdGpuRenderStatus.IncompatibleSkeleton;
				}
			}
			int count = poses[owner].Length;
			if (count == 0) {
				return MmdGpuRenderStatus.WaitingForPose;
			}
			if (count != skeleton.Length) {
				return MmdGpuRenderStatus.IncompatibleSkeleton;
			}
			return MmdGpuRenderStatus.Ready;
		}

		public static void Attach(EntityCommandBuffer commands, Entity owner, LocalTransform transform, Entity visualRoot,
			ComponentLookup<LocalToWorld> transforms, ComponentLookup<MmdSkinSource> sources, BufferLookup<LinkedEntityGroup> linked) {
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
					source.PoseEntity = owner;
					commands.SetComponent(child.Value, source);
				}
				commands.AppendToBuffer(owner, child);
			}
			commands.RemoveComponent<MmdLocalPose>(visualRoot);
			commands.RemoveComponent<MmdPlayback>(visualRoot);
			commands.RemoveComponent<MmdSkeletonBone>(visualRoot);
			commands.RemoveComponent<MmdModelMatrix>(visualRoot);
		}
	}
}
