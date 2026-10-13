using System.Reflection;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Samples.Bootstrap;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Spawning;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdSampleCharacterSpawnerTests {
		[Test]
		public void SpawnersShareWorldAndDatabaseRespectSpawnPointsAndFollowWorldRestart() {
			var root = new GameObject("MMD World Fixture");
			var first = new GameObject("MMD First Spawner");
			var second = new GameObject("MMD Second Spawner");
			root.SetActive(false);
			first.SetActive(false);
			second.SetActive(false);
			var source = MotionDatabaseFixture.Create();
			source.Groups = new[] { new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Walk"), Id = 2 } };
			source.Clips[0].Groups = System.Array.Empty<int>();
			source.Clips[2].Groups = System.Array.Empty<int>();
			var database = new TextAsset(MotionDatabaseCodec.Write(source));
			var configuration = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
			try {
				configuration.Assets.Database = database;
				configuration.Locomotion.WalkSpeed = 1f;
				// This lifecycle fixture has only idle/loop clips, so select its moving category explicitly.
				configuration.Locomotion.WalkGroup = "Walk";
				var bootstrap = root.AddComponent<MmdSampleBootstrap>();
				first.transform.SetPositionAndRotation(new Vector3(3f, 0f, 4f), Quaternion.Euler(0f, 90f, 0f));
				second.transform.position = new Vector3(-2f, 0f, 6f);
				var a = first.AddComponent<MmdSampleCharacterSpawner>();
				var b = second.AddComponent<MmdSampleCharacterSpawner>();
				Configure(a, configuration, true);
				Configure(b, configuration, false);
				SetField(bootstrap, "_characters", new[] { a, b });
				int worldCount = MmdWorldTestQueries.CountLiveWorlds();
				first.SetActive(true);
				Assert.That(a.CharacterEntity, Is.EqualTo(Entity.Null));
				second.SetActive(true);
				Assert.That(b.CharacterEntity, Is.EqualTo(Entity.Null));
				root.SetActive(true);
				var world = bootstrap.SampleWorld.World;
				var manager = world.EntityManager;
				Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worldCount + 1));
				Assert.That(a.CharacterEntity, Is.Not.EqualTo(b.CharacterEntity));
				Assert.That(manager.GetComponentData<LocalTransform>(a.CharacterEntity).Position, Is.EqualTo(new float3(3f, 0f, 4f)));
				Assert.That(math.distance(manager.GetComponentData<LocalTransform>(a.CharacterEntity).Forward(), new float3(1f, 0f, 0f)), Is.LessThan(0.00001f));
				Assert.That(manager.GetComponentData<LocalTransform>(b.CharacterEntity).Position, Is.EqualTo(new float3(-2f, 0f, 6f)));
				Assert.That(manager.GetComponentData<MmdDatabase>(a.CharacterEntity).Value, Is.EqualTo(manager.GetComponentData<MmdDatabase>(b.CharacterEntity).Value));
				Assert.That(manager.GetComponentData<MmdSampleCharacterControl>(a.CharacterEntity).WalkSpeed, Is.EqualTo(1f));
				Assert.That(manager.HasComponent<MmdSampleCharacterControl>(b.CharacterEntity), Is.False);
				// Input is world X/Z; this character's local forward points along world +X.
				bootstrap.SampleWorld.SetInput(new MmdSampleInput { Movement = new float2(1f, 0f) });
				bootstrap.SampleWorld.Tick();
				Assert.That(manager.GetComponentData<LocalTransform>(a.CharacterEntity).Position.x, Is.GreaterThan(3f));
				Assert.That(manager.GetComponentData<LocalTransform>(b.CharacterEntity).Position.xz, Is.EqualTo(new float2(-2f, 6f)));
				Assert.That(manager.GetComponentData<LocalTransform>(b.CharacterEntity).Position.y, Is.LessThan(0f));
				Entity oldCharacter = a.CharacterEntity;
				first.SetActive(false);
				world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
				Assert.That(manager.Exists(oldCharacter), Is.False);
				Assert.That(manager.Exists(b.CharacterEntity), Is.True);
				first.SetActive(true);
				Assert.That(a.CharacterEntity, Is.EqualTo(Entity.Null));
				root.SetActive(false);
				Assert.That(a.CharacterEntity, Is.EqualTo(Entity.Null));
				Assert.That(b.CharacterEntity, Is.EqualTo(Entity.Null));
				Assert.That(world.IsCreated, Is.False);
				root.SetActive(true);
				manager = bootstrap.SampleWorld.World.EntityManager;
				Assert.That(manager.Exists(a.CharacterEntity), Is.True);
				Assert.That(manager.Exists(b.CharacterEntity), Is.True);
				Assert.That(MmdWorldTestQueries.CountLiveWorlds(), Is.EqualTo(worldCount + 1));
			} finally {
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(configuration);
				Object.DestroyImmediate(database);
			}
		}

		private static void Configure(MmdSampleCharacterSpawner spawner, MmdSampleCharacterConfiguration configuration, bool controlled) {
			SetField(spawner, "_configuration", configuration);
			SetField(spawner, "_playerControlled", controlled);
		}

		private static void SetField(Object target, string name, object value) {
			target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
		}
	}
}
