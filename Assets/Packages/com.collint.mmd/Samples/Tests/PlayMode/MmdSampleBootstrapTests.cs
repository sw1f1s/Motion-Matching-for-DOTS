using System.Reflection;
using System.Text.RegularExpressions;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Samples.Bootstrap;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Spawning;
using ColInt.MMD.Samples.Physics.Helpers;
using ColInt.MMD.Samples.Physics;
using ColInt.MMD.Samples.Simulation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine.TestTools;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdSampleBootstrapTests {
		[Test]
		public void CreatesEmptyWorldAndReleasesWorldOnDisable() {
			var root = new GameObject("MMD Bootstrap Fixture");
			root.SetActive(false);
			try {
				var bootstrap = root.AddComponent<MmdSampleBootstrap>();
				int worldCount = MmdWorldTestQueries.CountLiveWorlds();
				root.SetActive(true);
				Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worldCount + 1));
				Assert.That(bootstrap.IsRunning, Is.True);
				var firstWorld = bootstrap.SampleWorld.World;
				using (var characters = firstWorld.EntityManager.CreateEntityQuery(typeof(MmdSearchQueryElement))) {
					Assert.That(characters.CalculateEntityCount(), Is.Zero);
				}
				Assert.That(root.transform.childCount, Is.Zero);
				Assert.That(root.GetComponentsInChildren<Renderer>().Length, Is.Zero);
				bootstrap.SampleWorld.Tick();
				root.SetActive(false);
				Assert.That(firstWorld.IsCreated, Is.False);
				Assert.That(bootstrap.IsRunning, Is.False);
				Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worldCount));
				root.SetActive(true);
				Assert.That(bootstrap.IsRunning, Is.True);
				Assert.That(bootstrap.SampleWorld.World, Is.Not.SameAs(firstWorld));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void BootstrapStartsGeometryCharactersAndDriverAndResetsBeforeWorldDisposal(bool participantsAreChildren) {
			var root = new GameObject("Bootstrap owner fixture");
			var participants = new GameObject("Sample participants fixture");
			root.SetActive(false);
			participants.SetActive(false);
			var configuration = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
			try {
				if (participantsAreChildren) {
					participants.transform.SetParent(root.transform);
				}
				var bootstrap = root.AddComponent<MmdSampleBootstrap>();
				var geometry = participants.AddComponent<MmdSampleWorldPhysicsGeometry>();
				var collider = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, new float3(10f, 1f, 10f), 0.8f);
				var floor = new MmdSampleWorldPhysicsBody("Floor", new RigidTransform(quaternion.identity, new float3(0f, -0.5f, 0f)), collider);
				geometry.SetCache("Bootstrap fixture", new[] { MmdSampleWorldPhysicsCacheBody.Pack(floor) });
				var character = participants.AddComponent<MmdSampleCharacterSpawner>();
				SetField(character, "_configuration", configuration);
				var driver = participants.AddComponent<MmdSampleSimulationDriver>();
				SetField(bootstrap, "_worldGeometry", new[] { geometry });
				SetField(bootstrap, "_characters", new[] { character });
				SetField(bootstrap, "_simulationDriver", driver);
				participants.SetActive(true);
				Assert.That(character.CharacterEntity, Is.EqualTo(Entity.Null));
				Assert.That(GetField(driver, "_activeWorld"), Is.Null);
				int worlds = MmdWorldTestQueries.CountLiveWorlds();
				for (int restart = 0; restart < 2; restart++) {
					root.SetActive(true);
					var sample = bootstrap.SampleWorld;
					Assert.That(GetField(driver, "_activeWorld"), Is.SameAs(sample));
					Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worlds + 1));
					using (var bodies = sample.World.EntityManager.CreateEntityQuery(typeof(PhysicsCollider))) {
						Assert.That(bodies.CalculateEntityCount(), Is.EqualTo(2));
					}
					sample.Tick();
					Assert.That(sample.World.EntityManager.GetComponentData<LocalTransform>(character.CharacterEntity).Position.y, Is.GreaterThanOrEqualTo(-0.05f));
					root.SetActive(false);
					Assert.That(GetField(driver, "_activeWorld"), Is.Null);
					Assert.That(character.CharacterEntity, Is.EqualTo(Entity.Null));
					Assert.That(sample.World.IsCreated, Is.False);
					Assert.That(bootstrap.IsRunning, Is.False);
					Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worlds));
				}
			} finally {
				Object.DestroyImmediate(participants);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(configuration);
			}
		}

		[Test]
		public void FailedStartupReleasesAlreadyCreatedCharactersAndCanRestart() {
			var root = new GameObject("Failed bootstrap fixture");
			var participants = new GameObject("Startup failure participants");
			root.SetActive(false);
			participants.SetActive(false);
			var configuration = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
			try {
				var bootstrap = root.AddComponent<MmdSampleBootstrap>();
				var first = participants.AddComponent<MmdSampleCharacterSpawner>();
				var second = participants.AddComponent<MmdSampleCharacterSpawner>();
				SetField(first, "_configuration", configuration);
				var driver = participants.AddComponent<MmdSampleSimulationDriver>();
				SetField(bootstrap, "_characters", new[] { first, second });
				SetField(bootstrap, "_simulationDriver", driver);
				participants.SetActive(true);
				int worlds = MmdWorldTestQueries.CountLiveWorlds();
				LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: A character configuration is required"));
				root.SetActive(true);
				Assert.That(bootstrap.IsRunning, Is.False);
				Assert.That(first.CharacterEntity, Is.EqualTo(Entity.Null));
				Assert.That(second.CharacterEntity, Is.EqualTo(Entity.Null));
				Assert.That(GetField(driver, "_activeWorld"), Is.Null);
				Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worlds));
				SetField(second, "_configuration", configuration);
				root.SetActive(false);
				root.SetActive(true);
				Assert.That(bootstrap.SampleWorld.World.EntityManager.Exists(first.CharacterEntity), Is.True);
				Assert.That(bootstrap.SampleWorld.World.EntityManager.Exists(second.CharacterEntity), Is.True);
				Assert.That(GetField(driver, "_activeWorld"), Is.SameAs(bootstrap.SampleWorld));
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(participants);
				Object.DestroyImmediate(configuration);
			}
		}

		private static object GetField(Object target, string name) {
			return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
		}

		private static void SetField(Object target, string name, object value) {
			target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
		}
	}
}
