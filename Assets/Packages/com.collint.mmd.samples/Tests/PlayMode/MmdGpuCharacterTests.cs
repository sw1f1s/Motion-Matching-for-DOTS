using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Authoring;
using ColInt.MMD.Graphics.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
#if UNITY_EDITOR
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using ColInt.MMD.Graphics.Systems;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Deformations;
using Unity.Entities.Serialization;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdGpuCharacterTests {
		private const string VariantPath = "Assets/MmdBakeTemporary/TestOutputs/MmdMeshVariant.prefab";
		private static readonly string PrefabPath = MmdSampleTestAssets.GetPath("Prefabs/SampleCharacter.prefab");

		[Test]
		public async Task RenamedMeshAndAdditionalSkinnedPartShareTheOriginalDatabase() {
			var original = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			if (!AssetDatabase.IsValidFolder("Assets/MmdBakeTemporary")) {
				AssetDatabase.CreateFolder("Assets", "MmdBakeTemporary");
			}
			if (!AssetDatabase.IsValidFolder("Assets/MmdBakeTemporary/TestOutputs")) {
				AssetDatabase.CreateFolder("Assets/MmdBakeTemporary", "TestOutputs");
			}
			Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath), Is.Null);
			var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
			try {
				var body = contents.GetComponentInChildren<SkinnedMeshRenderer>();
				body.name = "Renamed Body";
				body.transform.SetAsFirstSibling();
				var accessory = new GameObject("Additional Skinned Part").AddComponent<SkinnedMeshRenderer>();
				accessory.transform.SetParent(contents.transform, false);
				accessory.sharedMesh = body.sharedMesh;
				accessory.sharedMaterials = body.sharedMaterials;
				accessory.bones = body.bones;
				accessory.rootBone = body.rootBone;
				accessory.localBounds = body.localBounds;
				accessory.gameObject.AddComponent<MmdGpuSkinAuthoring>();
				PrefabUtility.SaveAsPrefabAsset(contents, VariantPath);
			} finally {
				PrefabUtility.UnloadPrefabContents(contents);
			}
			try {
				var variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
				using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				using var simulation = new MmdSampleWorld(0.1f);
				var manager = simulation.World.EntityManager;
				Entity character = simulation.Characters.Create(GpuRigDatabaseFixture.Build(original), true, LocalTransform.Identity);
				int boneCount = manager.GetBuffer<MmdLocalPose>(character).Length;
				manager.AddComponentData(character, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(variant) });
				await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
				await WaitForFrames(3, lifetime.Token);
				using (var query = manager.CreateEntityQuery(typeof(MmdSkinSource), typeof(MmdSkinBinding), typeof(SkinMatrix))) {
					Assert.That(query.CalculateEntityCount(), Is.EqualTo(2));
					using var skins = query.ToEntityArray(Unity.Collections.Allocator.Temp);
					foreach (Entity skin in skins) {
						Assert.That(manager.GetComponentData<MmdSkinSource>(skin).PoseEntity, Is.EqualTo(character));
						var bindings = manager.GetBuffer<MmdSkinBinding>(skin);
						var matrices = manager.GetBuffer<SkinMatrix>(skin);
						Assert.That(matrices.Length, Is.EqualTo(bindings.Length));
						foreach (var binding in bindings) {
							Assert.That(binding.BoneIndex, Is.InRange(0, boneCount - 1));
						}
					}
				}
				Assert.That(manager.GetBuffer<MmdLocalPose>(character).Length, Is.EqualTo(boneCount));
				Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.Zero);
				Entity visual = manager.GetComponentData<MmdGpuCharacterRender>(character).VisualRoot;
				SetRenderingDisabled(manager, character, true);
				await WaitForFrames(4, lifetime.Token);
				Assert.That(manager.Exists(visual), Is.False);
				SetRenderingDisabled(manager, character, false);
				await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
			} finally {
				AssetDatabase.DeleteAsset(VariantPath);
			}
		}

		[Test]
		public async Task RenderingWaitsForAnimationDataAndPreservesItOnRelease() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
				using (var simulation = new MmdSampleWorld(0.1f)) {
					var manager = simulation.World.EntityManager;
					Entity character = manager.CreateEntity(typeof(LocalTransform));
					manager.SetComponentData(character, LocalTransform.Identity);
					manager.AddComponentData(character, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
					await WaitForStatus(manager, character, MmdGpuRenderStatus.WaitingForPose, lifetime.Token);
					Assert.That(manager.HasBuffer<MmdLocalPose>(character), Is.False);
					Assert.That(manager.HasBuffer<MmdSkeletonBone>(character), Is.False);
					Assert.That(manager.HasBuffer<MmdModelMatrix>(character), Is.False);
					Assert.That(manager.HasComponent<MmdPlayback>(character), Is.False);
					Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(character).VisualRoot, Is.EqualTo(Entity.Null));
					manager.AddComponent<MmdBindPoseInitialization>(character);
					await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
					Assert.That(manager.HasComponent<MmdBindPoseInitialization>(character), Is.False);
					int poseCount = manager.GetBuffer<MmdLocalPose>(character).Length;
					SetRenderingDisabled(manager, character, true);
					await WaitForStatus(manager, character, MmdGpuRenderStatus.Unloaded, lifetime.Token);
					Assert.That(manager.GetBuffer<MmdLocalPose>(character).Length, Is.EqualTo(poseCount));
					Assert.That(manager.GetBuffer<MmdSkeletonBone>(character).Length, Is.EqualTo(poseCount));
					Assert.That(manager.HasBuffer<MmdModelMatrix>(character), Is.True);
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.Zero);
				}
			}
		}

		[Test]
		public async Task SameEntityOwnsAnimationAndGpuRenderWithoutExtraTick() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			var mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
			using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
				using (var simulation = new MmdSampleWorld(0.1f)) {
					var manager = simulation.World.EntityManager;
					Entity character = simulation.Characters.Create(GpuRigDatabaseFixture.Build(prefab), true, LocalTransform.Identity);
					manager.AddComponentData(character, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
					await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
					await WaitForFrames(4, lifetime.Token);
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.Zero, "Render loading must not advance simulation.");
					using (var skins = manager.CreateEntityQuery(typeof(MmdSkinSource))) {
						Assert.That(manager.GetComponentData<MmdSkinSource>(skins.GetSingletonEntity()).PoseEntity, Is.EqualTo(character));
					}
					Entity visual = manager.GetComponentData<MmdGpuCharacterRender>(character).VisualRoot;
					Assert.That(manager.HasBuffer<MmdLocalPose>(visual), Is.False);
					var before = EntitiesGraphicsGpuReadback.Read(simulation.World, mesh.vertexCount);
					AssertGpuMatchesSkin(manager, mesh, before);
					Transform[] bones = prefab.GetComponentsInChildren<Transform>(true).Where(t => t != prefab.transform).ToArray();
					int arm = Array.IndexOf(bones, prefab.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperArm));
					var pose = manager.GetBuffer<MmdLocalPose>(character);
					var joint = pose[arm];
					joint.Value.Rotation = math.mul(joint.Value.Rotation, quaternion.RotateX(1.1f));
					pose[arm] = joint;
					await WaitForFrames(3, lifetime.Token);
					var after = EntitiesGraphicsGpuReadback.Read(simulation.World, mesh.vertexCount);
					AssertGpuMatchesSkin(manager, mesh, after);
					int changed = 0;
					int unchanged = 0;
					for (int i = 0; i < before.Length; i++) {
						float distance = math.distance(before[i].Position, after[i].Position);
						if (distance > 0.001f) {
							changed++;
						}
						if (distance < 0.00001f) {
							unchanged++;
						}
					}
					Assert.That(changed, Is.GreaterThan(100));
					Assert.That(unchanged, Is.GreaterThan(100));
					manager.AddComponent<Disabled>(character);
					await WaitForFrames(4, lifetime.Token);
					Assert.That(manager.Exists(visual), Is.False);
					Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(character).VisualRoot, Is.EqualTo(Entity.Null));
					Assert.That(manager.GetBuffer<MmdLocalPose>(character)[arm].Value.Rotation.value, Is.EqualTo(joint.Value.Rotation.value));
					manager.RemoveComponent<Disabled>(character);
					await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
					SetRenderingDisabled(manager, character, true);
					await WaitForFrames(4, lifetime.Token);
					Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(character).VisualRoot, Is.EqualTo(Entity.Null));
					Assert.That(manager.HasBuffer<MmdLocalPose>(character), Is.True);
					SetRenderingDisabled(manager, character, false);
					await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
					SetRenderingDisabled(manager, character, true);
					await WaitForFrames(4, lifetime.Token);
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.Zero);
					simulation.Tick();
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.EqualTo(0.1f).Within(0.00001f));
					using (var requests = manager.CreateEntityQuery(typeof(RequestEntityPrefabLoaded))) {
						Assert.That(requests.CalculateEntityCount(), Is.Zero);
					}
				}
			}
		}

		[Test]
		public async Task RenderingGroupsPauseAndResumeInSameWorldWhileAnimationAndRootMotionContinue() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			var mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
			using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
				int worldCount = MmdWorldTestQueries.CountLiveWorlds();
				using (var sample = new MmdSampleWorld(0.1f)) {
					Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worldCount + 1));
					var manager = sample.World.EntityManager;
					Entity character = sample.Characters.Create(GpuRigDatabaseFixture.Build(prefab), true, LocalTransform.Identity);
					manager.AddComponentData(character, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
					await WaitForStatus(manager, character, MmdGpuRenderStatus.Ready, lifetime.Token);
					await WaitForFrames(3, lifetime.Token);
					Entity skin;
					using (var query = manager.CreateEntityQuery(typeof(MmdSkinSource))) {
						skin = query.GetSingletonEntity();
					}
					SkinMatrix[] frozen;
					using (var matrices = manager.GetBuffer<SkinMatrix>(skin).ToNativeArray(Unity.Collections.Allocator.Temp)) {
						frozen = matrices.ToArray();
					}
					var resources = manager.GetComponentData<MmdGpuCharacterRender>(character);
					var presentation = sample.World.GetExistingSystemManaged<PresentationSystemGroup>();
					var requests = sample.World.GetExistingSystemManaged<MmdGpuRenderRequestGroup>();
					var binding = sample.World.GetExistingSystemManaged<MmdGpuRenderBindingGroup>();
					presentation.Enabled = false;
					requests.Enabled = false;
					binding.Enabled = false;
					LocomotionSchema.WriteTrajectory(manager.GetBuffer<MmdSearchQueryElement>(character), new float3(0f, 0f, 1f), 0f);
					sample.Tick();
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.EqualTo(0.1f).Within(0.00001f));
					Assert.That(manager.GetComponentData<LocalTransform>(character).Position.z, Is.EqualTo(0.1f).Within(0.00001f));
					Transform[] bones = prefab.GetComponentsInChildren<Transform>(true).Where(t => t != prefab.transform).ToArray();
					int arm = Array.IndexOf(bones, prefab.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftUpperArm));
					var pose = manager.GetBuffer<MmdLocalPose>(character);
					var joint = pose[arm];
					joint.Value.Rotation = math.mul(joint.Value.Rotation, quaternion.RotateX(1.1f));
					pose[arm] = joint;
					await WaitForFrames(3, lifetime.Token);
					using (var matrices = manager.GetBuffer<SkinMatrix>(skin).ToNativeArray(Unity.Collections.Allocator.Temp)) {
						Assert.That(matrices.ToArray(), Is.EqualTo(frozen));
					}
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.EqualTo(0.1f).Within(0.00001f), "PlayerLoop must not add animation ticks.");
					presentation.Enabled = true;
					requests.Enabled = true;
					binding.Enabled = true;
					await WaitForFrames(3, lifetime.Token);
					using (var matrices = manager.GetBuffer<SkinMatrix>(skin).ToNativeArray(Unity.Collections.Allocator.Temp)) {
						Assert.That(matrices.ToArray(), Is.Not.EqualTo(frozen));
					}
					Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(character).VisualRoot, Is.EqualTo(resources.VisualRoot));
					Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(character), Is.True);
					Assert.That(manager.GetComponentData<MmdSkinSource>(skin).PoseEntity, Is.EqualTo(character));
					Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worldCount + 1));
					AssertGpuMatchesSkin(manager, mesh, EntitiesGraphicsGpuReadback.Read(sample.World, mesh.vertexCount));
				}
			}
		}

		[Test]
		public void DisablingRenderCancelsPendingLoadOnOwner() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			using (var world = new World("MMD Cancelled Load")) {
				var manager = world.EntityManager;
				Entity owner = manager.CreateEntity(typeof(LocalTransform));
				manager.AddComponentData(owner, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
				var request = world.GetOrCreateSystem<MmdGpuRenderRequestSystem>();
				var retirement = world.GetOrCreateSystem<MmdGpuRenderRetirementSystem>();
				SetRenderingDisabled(manager, owner, true);
				request.Update(world.Unmanaged);
				Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owner), Is.False);
				Assert.That(manager.HasComponent<MmdGpuCharacterRenderInitialized>(owner), Is.False);
				SetRenderingDisabled(manager, owner, false);
				request.Update(world.Unmanaged);
				Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owner), Is.True);
				Assert.That(manager.HasComponent<MmdGpuCharacterRenderInitialized>(owner), Is.True);
				request.Update(world.Unmanaged);
				Assert.That(manager.HasComponent<MmdGpuCharacterRenderInitialized>(owner), Is.True);
				SetRenderingDisabled(manager, owner, true);
				retirement.Update(world.Unmanaged);
				Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owner), Is.False);
				Assert.That(manager.HasComponent<MmdGpuCharacterRenderInitialized>(owner), Is.False);
				Assert.That(manager.Exists(owner), Is.True);
			}
		}

		[Test]
		public void PendingRequestsStayOnOwnersAndReleaseOnDisableDestructionAndSystemDisposal() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			using (var world = new World("MMD Request Lifetime")) {
				var manager = world.EntityManager;
				var owners = new[] {
					manager.CreateEntity(typeof(LocalTransform)),
					manager.CreateEntity(typeof(LocalTransform)),
					manager.CreateEntity(typeof(LocalTransform)),
					manager.CreateEntity(typeof(LocalTransform))
				};
				foreach (Entity owner in owners) {
					manager.SetComponentData(owner, LocalTransform.Identity);
					manager.AddComponentData(owner, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
				}
				var request = world.GetOrCreateSystem<MmdGpuRenderRequestSystem>();
				var cleanup = world.GetOrCreateSystem<MmdGpuRenderCleanupSystem>();
				var retirement = world.GetOrCreateSystem<MmdGpuRenderRetirementSystem>();
				int before;
				using (var entities = manager.GetAllEntities(Unity.Collections.Allocator.Temp)) {
					before = entities.Length;
				}
				request.Update(world.Unmanaged);
				using (var entities = manager.GetAllEntities(Unity.Collections.Allocator.Temp)) {
					Assert.That(entities.Length, Is.EqualTo(before), "Loading must not create separate request entities.");
				}
				foreach (Entity owner in owners) {
					Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owner), Is.True);
				}
				manager.AddComponent<Disabled>(owners[0]);
				SetRenderingDisabled(manager, owners[1], true);
				manager.DestroyEntity(owners[2]);
				Assert.That(manager.Exists(owners[2]), Is.True, "Cleanup ownership must survive destruction until release.");
				retirement.Update(world.Unmanaged);
				Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owners[0]), Is.False);
				Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owners[1]), Is.False);
				Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owners[3]), Is.True);
				world.DestroySystem(cleanup);
				Assert.That(manager.Exists(owners[2]), Is.False, "Disposal must release destroyed owners.");
				world.DestroySystem(retirement);
				foreach (Entity owner in new[] { owners[0], owners[1], owners[3] }) {
					Assert.That(manager.Exists(owner), Is.True);
					Assert.That(manager.HasComponent<MmdGpuCharacterRender>(owner), Is.False);
					Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(owner), Is.False);
				}
			}
		}

		[Test]
		public async Task IncompatibleRigReportsStateWithoutChangingAnimation() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
				using (var simulation = new MmdSampleWorld(0.1f)) {
					Entity character = simulation.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
					var manager = simulation.World.EntityManager;
					manager.AddComponentData(character, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
					await WaitForStatus(manager, character, MmdGpuRenderStatus.IncompatibleSkeleton, lifetime.Token);
					Assert.That(manager.GetComponentData<MmdPlayback>(character).Time, Is.Zero);
					Assert.That(manager.GetBuffer<MmdLocalPose>(character).Length, Is.EqualTo(2));
					var cleanup = manager.GetComponentData<MmdGpuCharacterRender>(character);
					Assert.That(cleanup.VisualRoot, Is.EqualTo(Entity.Null));
					Assert.That(manager.HasComponent<RequestEntityPrefabLoaded>(character), Is.False);
				}
			}
		}

		[Test]
		public async Task DestroyingOneOwnerKeepsOtherInstanceAndReleasesAllLinkedEntities() {
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			using (var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
				using (var simulation = new MmdSampleWorld(0.1f)) {
					var manager = simulation.World.EntityManager;
					Entity first = simulation.Characters.Create(default, false, LocalTransform.Identity);
					Entity second = simulation.Characters.Create(default, false, LocalTransform.FromPosition(2f, 0f, 0f));
					manager.SetComponentData(first, LocalTransform.Identity);
					manager.SetComponentData(second, LocalTransform.FromPosition(2f, 0f, 0f));
					manager.AddComponentData(first, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
					manager.AddComponentData(second, new MmdGpuCharacterRender { Prefab = new EntityPrefabReference(prefab) });
					await WaitForStatus(manager, first, MmdGpuRenderStatus.Ready, lifetime.Token);
					await WaitForStatus(manager, second, MmdGpuRenderStatus.Ready, lifetime.Token);
					var children = manager.GetBuffer<LinkedEntityGroup>(first).ToNativeArray(Unity.Collections.Allocator.Temp);
					Entity[] entities = children.Select(c => c.Value).ToArray();
					children.Dispose();
					manager.DestroyEntity(first);
					await WaitForFrames(5, lifetime.Token);
					foreach (Entity entity in entities) {
						Assert.That(manager.Exists(entity), Is.False, entity.ToString());
					}
					Assert.That(manager.GetComponentData<MmdGpuCharacterRender>(second).Status, Is.EqualTo(MmdGpuRenderStatus.Ready));
					Assert.That(manager.Exists(manager.GetComponentData<MmdGpuCharacterRender>(second).VisualRoot), Is.True);
					Entity gameChild = manager.CreateEntity();
					manager.GetBuffer<LinkedEntityGroup>(second).Add(new LinkedEntityGroup { Value = gameChild });
					SetRenderingDisabled(manager, second, true);
					await WaitForFrames(5, lifetime.Token);
					Assert.That(manager.Exists(gameChild), Is.True);
					Assert.That(manager.GetBuffer<LinkedEntityGroup>(second).Length, Is.EqualTo(2));
					Assert.That(manager.HasBuffer<MmdLocalPose>(second), Is.True, "Render cleanup must preserve animation data.");
					Assert.That(manager.HasBuffer<MmdSkeletonBone>(second), Is.True);
					Assert.That(manager.HasComponent<MmdPlayback>(second), Is.True);
					Assert.That(manager.HasBuffer<MmdModelMatrix>(second), Is.True);
				}
			}
		}

		private static void SetRenderingDisabled(EntityManager manager, Entity entity, bool disabled) {
			if (disabled) {
				manager.AddComponent<MmdGpuCharacterRenderDisabled>(entity);
			} else {
				manager.RemoveComponent<MmdGpuCharacterRenderDisabled>(entity);
			}
		}

		private static async Task WaitForFrames(int count, CancellationToken cancellation) {
			for (int frame = 0; frame < count; frame++) {
				await UnityEngine.Awaitable.NextFrameAsync(cancellation);
			}
		}

		private static async Task WaitForStatus(EntityManager manager, Entity character, MmdGpuRenderStatus expected, CancellationToken cancellation) {
			try {
				while (!manager.HasComponent<MmdGpuCharacterRender>(character) ||
					manager.GetComponentData<MmdGpuCharacterRender>(character).Status != expected) {
					await UnityEngine.Awaitable.NextFrameAsync(cancellation);
				}
			} catch (OperationCanceledException exception) {
				throw new TimeoutException("GPU render did not reach " + expected + ".", exception);
			}
		}

		private static void AssertGpuMatchesSkin(EntityManager manager, Mesh mesh, GpuVertexData[] gpu) {
			using (var query = manager.CreateEntityQuery(typeof(MmdSkinSource), typeof(SkinMatrix))) {
				var skin = manager.GetBuffer<SkinMatrix>(query.GetSingletonEntity(), true);
				Vector3[] vertices = mesh.vertices;
				BoneWeight[] weights = mesh.boneWeights;
				float maximumError = 0f;
				for (int i = 0; i < vertices.Length; i++) {
					BoneWeight weight = weights[i];
					var vertex = new float4((float3)vertices[i], 1f);
					float3 expected = math.mul(skin[weight.boneIndex0].Value, vertex) * weight.weight0 +
						math.mul(skin[weight.boneIndex1].Value, vertex) * weight.weight1 +
						math.mul(skin[weight.boneIndex2].Value, vertex) * weight.weight2 +
						math.mul(skin[weight.boneIndex3].Value, vertex) * weight.weight3;
					maximumError = math.max(maximumError, math.distance(expected, gpu[i].Position));
				}
				Assert.That(maximumError, Is.LessThan(0.0005f), "GPU positions must match the ECS skinning palette.");
			}
		}
	}
}
#endif
