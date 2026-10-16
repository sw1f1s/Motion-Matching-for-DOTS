using ColInt.MMD.Database.Data;
using ColInt.MMD.Editor.Authoring;
using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using ColInt.MMD.Graphics.Systems;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using ColInt.MMD.Presentation.Systems;
using NUnit.Framework;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdGpuVisualBindingTests {
		[Test]
		public void MeshNamesAndAdditionalHatDoNotChangeTheRigBones() {
			var rig = new GameObject("Rig");
			try {
				var root = new GameObject("root").transform;
				root.SetParent(rig.transform);
				var head = new GameObject("head").transform;
				head.SetParent(root);
				var body = new GameObject("Body").AddComponent<SkinnedMeshRenderer>();
				body.transform.SetParent(rig.transform);
				body.bones = new[] { head, root };
				var bones = MmdGpuRigBones.Collect(rig.transform, rig.GetComponentsInChildren<Transform>(true), rig.GetComponentsInChildren<SkinnedMeshRenderer>(true));
				body.name = "Renamed Body";
				body.transform.SetAsFirstSibling();
				var hat = new GameObject("Hat").AddComponent<SkinnedMeshRenderer>();
				hat.transform.SetParent(rig.transform);
				hat.bones = new[] { head };
				hat.gameObject.SetActive(false);
				var changed = MmdGpuRigBones.Collect(rig.transform, rig.GetComponentsInChildren<Transform>(true), rig.GetComponentsInChildren<SkinnedMeshRenderer>(true));
				Assert.That(bones, Is.EqualTo(new[] { root, head }));
				Assert.That(changed, Is.EqualTo(bones));
			} finally {
				Object.DestroyImmediate(rig);
			}
		}

		[Test]
		public void AttachmentRemapsEachSkinAndPreservesPrefabAndAnimation() {
			using var world = new World("MMD visual remapping");
			var manager = world.EntityManager;
			Entity owner = CreateOwner(manager);
			Entity prefab = CreatePrefab(manager);
			Entity visual = manager.Instantiate(prefab);
			var render = new MmdGpuCharacterRender { VisualRoot = visual, Status = MmdGpuRenderStatus.Loading };
			manager.AddComponentData(owner, render);
			world.GetOrCreateSystem<MmdGpuRenderAttachmentSystem>().Update(world.Unmanaged);
			Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(owner).Status, Is.EqualTo(MmdGpuRenderStatus.Ready));
			Assert.That(manager.GetComponentData<Parent>(visual).Value, Is.EqualTo(owner));
			Assert.That(manager.HasBuffer<MmdSkeletonBone>(visual), Is.False);
			Assert.That(manager.HasBuffer<MmdLocalPose>(visual), Is.False);
			Assert.That(manager.GetBuffer<MmdLocalPose>(owner).Length, Is.EqualTo(4));
			Assert.That(manager.GetBuffer<MmdSkeletonBone>(owner)[1].PathHash, Is.EqualTo(99));
			world.GetOrCreateSystem<MmdModelMatrixSystem>().Update(world.Unmanaged);
			world.GetOrCreateSystem<MmdSkinMatrixSystem>().Update(world.Unmanaged);
			manager.CompleteAllTrackedJobs();
			for (int child = 1; child <= 2; child++) {
				Entity original = manager.GetBuffer<LinkedEntityGroup>(prefab)[child].Value;
				Entity instance = manager.GetBuffer<LinkedEntityGroup>(visual)[child].Value;
				var originalBindings = manager.GetBuffer<MmdSkinBinding>(original);
				var mappedBindings = manager.GetBuffer<MmdSkinBinding>(instance);
				Assert.That(originalBindings[0].BoneIndex, Is.EqualTo(child == 1 ? 1 : 2));
				Assert.That(mappedBindings[0].BoneIndex, Is.EqualTo(child == 1 ? 3 : 2));
				Assert.That(mappedBindings[0].BindPose, Is.EqualTo(originalBindings[0].BindPose));
				Assert.That(manager.GetComponentData<MmdSkinSource>(original).PoseEntity, Is.EqualTo(prefab));
				Assert.That(manager.GetComponentData<MmdSkinSource>(instance).PoseEntity, Is.EqualTo(owner));
				var matrix = manager.GetBuffer<SkinMatrix>(instance)[0].Value;
				float3 expected = child == 1 ? new float3(2f, 0f, 5f) : new float3(2f, 3f, 0f);
				Assert.That(math.distance(matrix.c3, expected), Is.LessThan(0.00001f));
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void FailedAttachmentDoesNotAlterOwnerOrLeaveVisuals(bool invalidSkin) {
			using var world = new World("MMD invalid visual remapping");
			var manager = world.EntityManager;
			Entity owner = CreateOwner(manager);
			Entity prefab = CreatePrefab(manager);
			Entity visual = manager.Instantiate(prefab);
			if (invalidSkin) {
				Entity skin = manager.GetBuffer<LinkedEntityGroup>(visual)[1].Value;
				var bindings = manager.GetBuffer<MmdSkinBinding>(skin);
				bindings[0] = new MmdSkinBinding { BoneIndex = 100, BindPose = float4x4.identity };
			} else {
				var skeleton = manager.GetBuffer<MmdSkeletonBone>(owner);
				skeleton[3] = new MmdSkeletonBone { PathHash = 404, ParentIndex = 0 };
			}
			manager.AddComponentData(owner, new MmdGpuCharacterRender { VisualRoot = visual, Status = MmdGpuRenderStatus.Loading });
			world.GetOrCreateSystem<MmdGpuRenderAttachmentSystem>().Update(world.Unmanaged);
			Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(owner).Status, Is.EqualTo(invalidSkin ? MmdGpuRenderStatus.InvalidRig : MmdGpuRenderStatus.IncompatibleSkeleton));
			Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(owner).VisualRoot, Is.EqualTo(Entity.Null));
			Assert.That(manager.Exists(visual), Is.False);
			Assert.That(manager.Exists(prefab), Is.True);
			Assert.That(manager.HasBuffer<LinkedEntityGroup>(owner), Is.False);
			Assert.That(manager.GetBuffer<MmdLocalPose>(owner).Length, Is.EqualTo(4));
		}

		private static Entity CreateOwner(EntityManager manager) {
			Entity owner = manager.CreateEntity(typeof(LocalTransform), typeof(MmdGpuCharacterRenderInitialized));
			manager.SetComponentData(owner, LocalTransform.Identity);
			var skeleton = manager.AddBuffer<MmdSkeletonBone>(owner);
			skeleton.Add(new MmdSkeletonBone { PathHash = 1, ParentIndex = -1 });
			skeleton.Add(new MmdSkeletonBone { PathHash = 99, ParentIndex = -1 });
			skeleton.Add(new MmdSkeletonBone { PathHash = 2, ParentIndex = 0 });
			skeleton.Add(new MmdSkeletonBone { PathHash = 3, ParentIndex = 0 });
			var poses = manager.AddBuffer<MmdLocalPose>(owner);
			foreach (float3 position in new[] { new float3(2f, 0f, 0f), float3.zero, new float3(0f, 3f, 0f), new float3(0f, 0f, 5f) }) {
				poses.Add(new MmdLocalPose { Value = new JointPose { Position = position, Rotation = quaternion.identity, Scale = new float3(1f) } });
			}
			manager.AddBuffer<MmdModelMatrix>(owner);
			return owner;
		}

		private static Entity CreatePrefab(EntityManager manager) {
			Entity prefab = manager.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(MmdPlayback));
			manager.SetComponentData(prefab, LocalTransform.Identity);
			var skeleton = manager.AddBuffer<MmdSkeletonBone>(prefab);
			skeleton.Add(new MmdSkeletonBone { PathHash = 1, ParentIndex = -1 });
			skeleton.Add(new MmdSkeletonBone { PathHash = 3, ParentIndex = 0 });
			skeleton.Add(new MmdSkeletonBone { PathHash = 2, ParentIndex = 0 });
			manager.AddBuffer<MmdLocalPose>(prefab).ResizeUninitialized(3);
			manager.AddBuffer<MmdModelMatrix>(prefab);
			manager.AddBuffer<LinkedEntityGroup>(prefab).Add(new LinkedEntityGroup { Value = prefab });
			for (int index = 1; index <= 2; index++) {
				Entity skin = manager.CreateEntity(typeof(Prefab));
				manager.AddComponentData(skin, new MmdSkinSource { PoseEntity = prefab, ModelToMesh = float4x4.identity });
				manager.AddBuffer<MmdSkinBinding>(skin).Add(new MmdSkinBinding { BoneIndex = index, BindPose = float4x4.identity });
				manager.AddBuffer<SkinMatrix>(skin);
				manager.GetBuffer<LinkedEntityGroup>(prefab).Add(new LinkedEntityGroup { Value = skin });
			}
			return prefab;
		}
	}
}
