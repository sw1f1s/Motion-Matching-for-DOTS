using System;
using System.Reflection;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Authoring;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.Presentation.Components;
using ColInt.MMD.Presentation.Systems;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColInt.MMD.Tests {
	public class MmdAnimationLayerLifecycleTests {
		[TestCase(false)]
		[TestCase(true)]
		public void CharacterConfigurationBuildsIndependentLayersAndPresentationUsesFinalPoseAfterWorldRestart(bool groupSource) {
			using (var fixture = new MmdAnimationLayerFixture()) {
				var asset = new TextAsset(MotionDatabaseCodec.Write(fixture.Source));
				var configuration = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
				var layers = ScriptableObject.CreateInstance<MmdAnimationLayersConfiguration>();
				try {
					var settings = fixture.Settings("Walk", 1f, 0f, groupSource ? 1f : 0f, 0.4f);
					settings.Source = groupSource ? MmdAnimationLayerSource.Group : MmdAnimationLayerSource.Clip;
					layers.Configure(asset, new[] { settings });
					configuration.Assets.Database = asset;
					typeof(MmdSampleCharacterConfiguration).GetField("_animationLayers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configuration, layers);
					for (int restart = 0; restart < 2; restart++) {
						using (var sample = new MmdSampleWorld(0.1f)) {
							Entity first = sample.Characters.Create(configuration, LocalTransform.Identity, false, false);
							Entity second = sample.Characters.Create(configuration, LocalTransform.Identity, false, false);
							var manager = sample.World.EntityManager;
							Assert.That(manager.GetComponentData<MmdAnimationLayersDefinitionReference>(first).Value,
								Is.EqualTo(manager.GetComponentData<MmdAnimationLayersDefinitionReference>(second).Value));
							Assert.That(manager.GetBuffer<MmdAnimationLayerPose>(first).Length, Is.EqualTo(1));
							MmdAnimationLayers.SetWeight(manager.GetBuffer<MmdAnimationLayerRequest>(second), 0, 0f, 0f);
							sample.Tick();
							Assert.That(manager.GetBuffer<MmdComposedPose>(first)[1].Value.Position.x, Is.EqualTo(3f));
							Assert.That(manager.GetBuffer<MmdComposedPose>(second)[1].Value.Position.x, Is.Zero);
							var system = sample.World.GetExistingSystem<MmdModelMatrixSystem>();
							system.Update(sample.World.Unmanaged);
							manager.CompleteAllTrackedJobs();
							Assert.That(manager.GetBuffer<MmdModelMatrix>(first)[1].Value.c3.x, Is.EqualTo(3f));
							manager.DestroyEntity(first);
							manager.AddComponent<Disabled>(second);
							Assert.DoesNotThrow(sample.Tick);
						}
					}
				} finally {
					Object.DestroyImmediate(layers);
					Object.DestroyImmediate(configuration);
					Object.DestroyImmediate(asset);
				}
			}
		}

		[Test]
		public void MismatchedLayerDatabaseFailsWithoutLeavingACharacter() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				var first = new TextAsset(MotionDatabaseCodec.Write(fixture.Source));
				var second = new TextAsset(MotionDatabaseCodec.Write(fixture.Source));
				var configuration = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
				var layers = ScriptableObject.CreateInstance<MmdAnimationLayersConfiguration>();
				try {
					configuration.Assets.Database = first;
					layers.Configure(second, Array.Empty<MmdAnimationLayerSettings>());
					typeof(MmdSampleCharacterConfiguration).GetField("_animationLayers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configuration, layers);
					using (var sample = new MmdSampleWorld(0.1f)) {
						Assert.Throws<ArgumentException>(() => sample.Characters.Create(configuration, LocalTransform.Identity, false, false));
						using (var query = sample.World.EntityManager.CreateEntityQuery(typeof(MmdPlayback))) {
							Assert.That(query.CalculateEntityCount(), Is.Zero);
						}
					}
				} finally {
					Object.DestroyImmediate(layers);
					Object.DestroyImmediate(configuration);
					Object.DestroyImmediate(first);
					Object.DestroyImmediate(second);
				}
			}
		}
	}
}
