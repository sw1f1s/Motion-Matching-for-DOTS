using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdAnimationLayerGroupTests {
		[Test]
		public void WholeGroupMatchesMovementAndSmoothsClipChangesWithoutChangingBasePlaybackOrRoot() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				ConfigureGroup(fixture);
				using (var sample = new MmdSampleWorld(0.05f)) {
					Entity actor = Create(fixture, sample);
					var manager = sample.World.EntityManager;
					LocomotionSchema.WriteTrajectory(manager.GetBuffer<MmdSearchQueryElement>(actor), float3.zero, 0f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].ClipIndex, Is.Zero);
					float previous = manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x;
					LocomotionSchema.WriteTrajectory(manager.GetBuffer<MmdSearchQueryElement>(actor), new float3(0f, 0f, 1f), 0f);
					sample.Tick();
					var layer = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					Assert.That(layer.ClipIndex, Is.EqualTo(1));
					float current = manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x;
					Assert.That(current - previous, Is.InRange(0f, 0.5f));
					for (int tick = 0; tick < 5; tick++) {
						sample.Tick();
					}
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.EqualTo(3f).Within(0.00001f));
					Assert.That(manager.GetComponentData<MmdPlayback>(actor).ClipIndex, Is.Zero);
					Assert.That(manager.GetComponentData<MmdRootMotion>(actor).Translation, Is.EqualTo(float3.zero));
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.Ready));
				}
			}
		}

		[Test]
		public void GroupLayerFreezesOnPauseDisabledAndReportsInvalidQueryThenRecovers() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				ConfigureGroup(fixture);
				using (var sample = new MmdSampleWorld(0.05f)) {
					Entity actor = Create(fixture, sample);
					var manager = sample.World.EntityManager;
					LocomotionSchema.WriteTrajectory(manager.GetBuffer<MmdSearchQueryElement>(actor), new float3(0f, 0f, 1f), 0f);
					sample.Tick();
					var layer = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					sample.SetPaused(true);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Time, Is.EqualTo(layer.Time));
					sample.SetPaused(false);
					manager.AddComponent<Disabled>(actor);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].SearchCountdown, Is.EqualTo(layer.SearchCountdown));
					manager.RemoveComponent<Disabled>(actor);
					manager.GetBuffer<MmdSearchQueryElement>(actor).ResizeUninitialized(1);
					// Keep the base search asleep while intentionally corrupting only the group query.
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.InvalidQuery));
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.Zero);
					var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
					query.ResizeUninitialized(LocomotionSchema.Dimension);
					for (int feature = 0; feature < query.Length; feature++) {
						query[feature] = default;
					}
					LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 1f), 0f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.Ready));
				}
			}
		}

		[Test]
		public void GroupIgnoresSpecificClipOrdinalAndInvalidSettingsOrMissingQueryDoNotAddBuffers() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				ConfigureGroup(fixture);
				var settings = Settings(fixture);
				settings.ClipInGroup = 999;
				using (var database = MotionDatabaseBuilder.Build(fixture.Source)) {
					using var definition = MmdAnimationLayerBuilder.Build(database, new[] { settings });
					ref var layers = ref definition.Definition.Value.Layers;
					Assert.That(layers[0].GroupId, Is.EqualTo(8));
					Assert.That(layers[0].ClipIndex, Is.Zero);
					settings.Matching.Interval = float.NaN;
					Assert.Throws<ArgumentException>(() => MmdAnimationLayerBuilder.Build(database, new[] { settings }));
				}
				using (var sample = new MmdSampleWorld(0.05f)) {
					var manager = sample.World.EntityManager;
					Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
					manager.RemoveComponent<MmdSearchQueryElement>(actor);
					Assert.Throws<ArgumentException>(() => fixture.Add(manager, actor, new[] { Settings(fixture) }));
					Assert.That(manager.HasBuffer<MmdAnimationLayer>(actor), Is.False);
					Assert.That(manager.HasBuffer<MmdAnimationLayerPose>(actor), Is.False);
				}
			}
		}

		private static void ConfigureGroup(MmdAnimationLayerFixture fixture) {
			var group = fixture.Source.Groups[3];
			group.ActionId = 0;
				group.Mode = MotionPlaybackMode.LayerOnly;
			fixture.Source.Groups[3] = group;
			for (int index = 0; index < 2; index++) {
				var clip = fixture.Source.Clips[index];
				clip.Groups = new[] { 1 << index, group.Id };
				clip.ActionId = 0;
				clip.Mode = MotionPlaybackMode.LayerOnly;
				fixture.Source.Clips[index] = clip;
			}
		}

		private static MmdAnimationLayerSettings Settings(MmdAnimationLayerFixture fixture) {
			var settings = fixture.Settings("Other", 1f, 0f, 1f, 0f);
			settings.Source = MmdAnimationLayerSource.Group;
			settings.Matching.Interval = 0f;
			settings.Matching.SwitchPenalty = 0f;
			return settings;
		}

		private static Entity Create(MmdAnimationLayerFixture fixture, MmdSampleWorld sample) {
			Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
			var manager = sample.World.EntityManager;
			manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f, MatchedFrame = -1 });
			fixture.Add(manager, actor, new[] { Settings(fixture) });
			return actor;
		}
	}
}
