using System.Threading.Tasks;
using System.Threading;
using System;
using BoxCollider = UnityEngine.BoxCollider;
using ColInt.MMD.Editor.Physics;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Components;
using ColInt.MMD.Samples.Physics.Helpers;
using ColInt.MMD.Samples.Physics;
using NUnit.Framework;
using Object = UnityEngine.Object;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdSampleWorldPhysicsTests {
		[Test]
		public async Task CacheLoadsIndependentStaticEntitiesIntoEachWorldAndRejectsChangedGeometry() {
			var root = new GameObject("Physics fixture");
			try {
				var source = root.AddComponent<MmdSampleWorldPhysicsGeometry>();
				var box = AddBox(root, "Offset box", new Vector3(2f, 3f, 4f), new Vector3(2f, 3f, 4f));
				box.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
				box.center = new Vector3(0.5f, 0f, 0f);
				AddBox(root, "Trigger", Vector3.zero, Vector3.one).isTrigger = true;
				AddBox(root, "Disabled", Vector3.zero, Vector3.one).enabled = false;
				AddBox(root, "Inactive", Vector3.zero, Vector3.one).gameObject.SetActive(false);
				await MmdSampleWorldPhysicsBaker.BakeAsync(source, CancellationToken.None);
				Assert.That(source.Bodies.Count, Is.EqualTo(1));
				Assert.That(MmdSampleWorldPhysicsBaker.IsCurrent(source), Is.True);
				using (var a = new MmdSampleWorld(1f / 60f)) {
					using (var b = new MmdSampleWorld(1f / 60f)) {
						Entity first = MmdSampleWorldPhysicsLoader.Load(a.World, source)[0];
						Entity second = MmdSampleWorldPhysicsLoader.Load(b.World, source)[0];
						var firstCollider = a.World.EntityManager.GetComponentData<PhysicsCollider>(first).Value;
						var secondCollider = b.World.EntityManager.GetComponentData<PhysicsCollider>(second).Value;
						Assert.That(firstCollider, Is.Not.EqualTo(secondCollider));
						Assert.That(a.World.EntityManager.HasComponent<PhysicsMass>(first), Is.False);
						Assert.That(math.distance(a.World.EntityManager.GetComponentData<LocalTransform>(first).Position, box.transform.TransformPoint(box.center)), Is.LessThan(0.0001f));
						a.Tick();
						using (var query = a.World.EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton))) {
							Assert.That(query.GetSingleton<PhysicsWorldSingleton>().NumStaticBodies, Is.EqualTo(2));
						}
					}
				}
				box.size *= 2f;
				Assert.That(MmdSampleWorldPhysicsBaker.IsCurrent(source), Is.False);
				root.transform.position = Vector3.one;
				using (var world = new MmdSampleWorld(1f / 60f)) {
					Assert.Throws<InvalidOperationException>(() => MmdSampleWorldPhysicsLoader.Load(world.World, source));
				}
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public async Task CancellationAndChangedSourcePreservePreviousCache() {
			var root = new GameObject("Bake cancellation fixture");
			try {
				var source = root.AddComponent<MmdSampleWorldPhysicsGeometry>();
				var box = AddBox(root, "Box", Vector3.zero, Vector3.one);
				await MmdSampleWorldPhysicsBaker.BakeAsync(source, CancellationToken.None);
				string original = source.SourceFingerprint;
				box.size *= 2f;
				using (var cancellation = new CancellationTokenSource()) {
					Task pending = MmdSampleWorldPhysicsBaker.BakeAsync(source, cancellation.Token);
					cancellation.Cancel();
					try {
						await pending;
						Assert.Fail("Cancelled bake must not commit.");
					} catch (OperationCanceledException) {
					}
				}
				Assert.That(source.SourceFingerprint, Is.EqualTo(original));
				Task changed = MmdSampleWorldPhysicsBaker.BakeAsync(source, CancellationToken.None);
				box.size *= 2f;
				try {
					await changed;
					Assert.Fail("Changed geometry must not commit.");
				} catch (InvalidOperationException) {
				}
				Assert.That(source.SourceFingerprint, Is.EqualTo(original));
				Assert.That(MmdSampleWorldPhysicsBaker.IsBaking, Is.False);
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void UnsupportedEnabledColliderFailsExplicitly() {
			var root = new GameObject("Unsupported collider fixture", typeof(CharacterController));
			try {
				Assert.Throws<NotSupportedException>(() => MmdSampleWorldPhysicsBuilder.ValidateCollider(root.GetComponent<CharacterController>()));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void PhysicsTickSupportsFloorStopsAtWallAndPauseFreezesBody() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				AddStatic(manager, "Floor", new float3(0f, -0.5f, 3f), new float3(20f, 1f, 20f));
				AddStatic(manager, "Wall", new float3(0f, 1.5f, 3f), new float3(10f, 3f, 0.5f));
				Entity character = sample.Characters.Create(default, false, LocalTransform.Identity);
				MmdSampleCharacterPhysicsBuilder.Add(manager, character);
				for (int tick = 0; tick < 180; tick++) {
					manager.SetComponentData(character, new MmdRootMotion {
						Translation = new float3(0f, 0f, 2f * sample.Timestep),
						Rotation = quaternion.identity,
						DeltaTime = sample.Timestep,
						Valid = true
					});
					sample.Tick();
				}
				float3 position = manager.GetComponentData<LocalTransform>(character).Position;
				Assert.That(position.z, Is.InRange(2.35f, 2.6f));
				Assert.That(position.y, Is.InRange(-0.05f, 0.05f));
				Assert.That(manager.GetComponentData<MmdRootMotion>(character).Valid, Is.False);
				sample.SetPaused(true);
				for (int tick = 0; tick < 10; tick++) {
					sample.Tick();
				}
				Assert.That(manager.GetComponentData<LocalTransform>(character).Position, Is.EqualTo(position));
				sample.SetPaused(false);
				sample.Tick();
				Assert.That(math.abs(manager.GetComponentData<PhysicsVelocity>(character).Linear.z), Is.LessThan(0.001f));
			}
		}

		[Test]
		public void DisabledAndDestroyedColliderOwnersAreReleasedByCleanup() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity body = AddStatic(manager, "Owned box", float3.zero, new float3(1f));
				manager.AddComponent<Disabled>(body);
				manager.DestroyEntity(body);
				Assert.That(manager.Exists(body), Is.True);
				sample.World.GetExistingSystemManaged<InitializationSystemGroup>().Update();
				Assert.That(manager.Exists(body), Is.False);
				body = AddStatic(manager, "Removed shape", float3.zero, new float3(1f));
				manager.AddComponent<Disabled>(body);
				manager.RemoveComponent<PhysicsCollider>(body);
				sample.World.GetExistingSystemManaged<InitializationSystemGroup>().Update();
				Assert.That(manager.HasComponent<MmdSamplePhysicsColliderOwner>(body), Is.False);
			}
		}

		private static BoxCollider AddBox(GameObject root, string name, Vector3 position, Vector3 scale) {
			var child = new GameObject(name, typeof(BoxCollider));
			child.transform.SetParent(root.transform);
			child.transform.position = position;
			child.transform.localScale = scale;
			return child.GetComponent<BoxCollider>();
		}

		private static Entity AddStatic(EntityManager manager, string name, float3 position, float3 size) {
			var collider = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, size, 0.8f);
			return MmdSampleWorldPhysicsLoader.CreateBody(manager, new MmdSampleWorldPhysicsBody(name, new RigidTransform(quaternion.identity, position), collider));
		}
	}
}
