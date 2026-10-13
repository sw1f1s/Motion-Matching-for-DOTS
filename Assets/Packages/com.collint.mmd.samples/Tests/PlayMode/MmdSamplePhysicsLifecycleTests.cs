using System.Reflection;
using System.Threading.Tasks;
using ColInt.MMD.Samples.Bootstrap;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Helpers;
using ColInt.MMD.Samples.Physics;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdSamplePhysicsLifecycleTests {
		[Test]
		public async Task AutomaticPlayerLoopDoesNotAdvanceManualPhysicsTick() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity character = sample.Characters.Create(default, false, LocalTransform.FromPosition(0f, 3f, 0f));
				MmdSampleCharacterPhysicsBuilder.Add(manager, character);
				sample.Tick();
				float3 position = manager.GetComponentData<LocalTransform>(character).Position;
				Assert.That(position.y, Is.LessThan(3f));
				await UnityEngine.Awaitable.NextFrameAsync();
				await UnityEngine.Awaitable.NextFrameAsync();
				Assert.That(manager.GetComponentData<LocalTransform>(character).Position, Is.EqualTo(position));
				sample.Tick();
				Assert.That(manager.GetComponentData<LocalTransform>(character).Position.y, Is.LessThan(position.y));
			}
		}

		[Test]
		public void GeometryFollowsWorldRestartAndReleasesStaticEntitiesOnDisable() {
			var root = new GameObject("World fixture");
			var geometryObject = new GameObject("Geometry fixture");
			root.SetActive(false);
			geometryObject.SetActive(false);
			try {
				var bootstrap = root.AddComponent<MmdSampleBootstrap>();
				var geometry = geometryObject.AddComponent<MmdSampleWorldPhysicsGeometry>();
				typeof(MmdSampleBootstrap).GetField("_worldGeometry", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bootstrap, new[] { geometry });
				var collider = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, new float3(10f, 1f, 10f), 0.8f);
				var cached = MmdSampleWorldPhysicsCacheBody.Pack(new MmdSampleWorldPhysicsBody("Ground", RigidTransform.identity, collider));
				geometry.SetCache("PlayMode fixture", new[] { cached });
				geometryObject.SetActive(true);
				root.SetActive(true);
				World first = bootstrap.SampleWorld.World;
				using (var bodies = first.EntityManager.CreateEntityQuery(typeof(PhysicsCollider))) {
					Assert.That(bodies.CalculateEntityCount(), Is.EqualTo(1));
					geometryObject.SetActive(false);
					first.GetExistingSystemManaged<InitializationSystemGroup>().Update();
					Assert.That(bodies.CalculateEntityCount(), Is.Zero);
					geometryObject.SetActive(true);
					Assert.That(bodies.CalculateEntityCount(), Is.Zero);
				}
				root.SetActive(false);
				Assert.That(first.IsCreated, Is.False);
				root.SetActive(true);
				using (var bodies = bootstrap.SampleWorld.World.EntityManager.CreateEntityQuery(typeof(PhysicsCollider))) {
					Assert.That(bodies.CalculateEntityCount(), Is.EqualTo(1));
				}
			} finally {
				Object.DestroyImmediate(geometryObject);
				Object.DestroyImmediate(root);
			}
		}
	}
}
