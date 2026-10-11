using System;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Object = UnityEngine.Object;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MmdSampleCharacterFactoryTests {
		[Test]
		public void DifferentConfigurationsKeepIndependentDatabasesInOneWorldAndShareOnlyMatchingAssets() {
			var source = MotionDatabaseFixture.Create();
			var firstAsset = new TextAsset(MotionDatabaseCodec.Write(source));
			var secondAsset = new TextAsset(MotionDatabaseCodec.Write(source));
			var first = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
			var second = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
			SetDatabase(first, firstAsset);
			SetDatabase(second, secondAsset);
			try {
				using (var sample = new MmdSampleWorld(0.1f)) {
					using (var otherWorld = new MmdSampleWorld(0.1f)) {
						var manager = sample.World.EntityManager;
						Entity a = sample.Characters.Create(first, LocalTransform.Identity, false, false);
						Entity b = sample.Characters.Create(second, LocalTransform.FromPosition(2f, 0f, 0f), false, false);
						Entity shared = sample.Characters.Create(first, LocalTransform.Identity, false, false);
						Entity other = otherWorld.Characters.Create(first, LocalTransform.Identity, false, false);
						var database = manager.GetComponentData<MmdDatabase>(a).Value;
						Assert.That(database, Is.EqualTo(manager.GetComponentData<MmdDatabase>(shared).Value));
						Assert.That(database, Is.Not.EqualTo(manager.GetComponentData<MmdDatabase>(b).Value));
						Assert.That(database, Is.Not.EqualTo(otherWorld.World.EntityManager.GetComponentData<MmdDatabase>(other).Value));
						manager.DestroyEntity(a);
						manager.AddComponent<Disabled>(shared);
						sample.Tick();
						Assert.That(manager.GetComponentData<MmdPlayback>(b).Time, Is.EqualTo(0.1f).Within(0.00001f));
						Assert.That(manager.GetComponentData<MmdPlayback>(shared).Time, Is.Zero);
						sample.Dispose();
						sample.Dispose();
						Assert.Throws<ObjectDisposedException>(() => sample.Characters.Create(first, LocalTransform.Identity, false, false));
						otherWorld.Tick();
						Assert.That(otherWorld.World.EntityManager.GetComponentData<MmdPlayback>(other).Time, Is.EqualTo(0.1f).Within(0.00001f));
					}
				}
			} finally {
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
				Object.DestroyImmediate(firstAsset);
				Object.DestroyImmediate(secondAsset);
			}
		}

		[Test]
		public void ExternallyDisposedWorldAllowsRepeatedSampleCleanup() {
			using (var sample = new MmdSampleWorld(0.1f)) {
				sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				sample.World.Dispose();
				Assert.DoesNotThrow(sample.Dispose);
				Assert.DoesNotThrow(sample.Dispose);
				Assert.Throws<ObjectDisposedException>(() => sample.Characters.Create(default, false, LocalTransform.Identity));
			}
		}

		private static void SetDatabase(MmdSampleCharacterConfiguration configuration, TextAsset asset) {
			configuration.Assets.Database = asset;
		}
	}
}
