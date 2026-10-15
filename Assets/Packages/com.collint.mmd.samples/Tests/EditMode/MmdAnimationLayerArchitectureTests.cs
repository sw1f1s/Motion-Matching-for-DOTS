using ColInt.MMD.Database.Components;
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
	public class MmdAnimationLayerArchitectureTests {
		[Test]
		public void FootOverlayCannotFeedBackIntoBasePoseQueryOrRootMotion() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				fixture.Source.LeftFootBone = 0;
				fixture.Source.RightFootBone = 1;
				using (var sample = new MmdSampleWorld(0.05f)) {
					var manager = sample.World.EntityManager;
					var database = MotionDatabaseBuilder.Build(fixture.Source);
					Entity layered = sample.Characters.Create(database, true, LocalTransform.Identity);
					Entity plain = sample.Characters.Create(database, true, LocalTransform.Identity);
					fixture.Add(manager, layered, new[] { fixture.Settings("Action", 1f, 0f, 0f, 0.6f) });
					manager.SetComponentData(layered, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
					manager.SetComponentData(plain, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
					for (int tick = 0; tick < 8; tick++) {
						sample.Tick();
						foreach (Entity actor in new[] { layered, plain }) {
							LocomotionSchema.WritePose(ref database.Value, manager.GetComponentData<MmdPlayback>(actor),
								manager.GetBuffer<MmdSearchQueryElement>(actor), manager.GetBuffer<MmdLocalPose>(actor),
								manager.GetBuffer<MmdPoseFeatureHistory>(actor), 0.05f);
						}
						for (int feature = 0; feature < LocomotionSchema.Dimension; feature++) {
							Assert.That(manager.GetBuffer<MmdSearchQueryElement>(layered)[feature].Value,
								Is.EqualTo(manager.GetBuffer<MmdSearchQueryElement>(plain)[feature].Value));
						}
						Assert.That(manager.GetBuffer<MmdLocalPose>(layered)[1].Value.Position,
							Is.EqualTo(manager.GetBuffer<MmdLocalPose>(plain)[1].Value.Position));
						Assert.That(manager.GetComponentData<MmdRootMotion>(layered).Translation,
							Is.EqualTo(manager.GetComponentData<MmdRootMotion>(plain).Translation));
						Assert.That(manager.GetBuffer<MmdComposedPose>(layered)[1].Value.Position.x, Is.EqualTo(6f));
					}
				}
			}
		}

		[Test]
		public void GroupSearchUsesTheSameDoubleSpeedAsPlayback() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				for (int frame = 0; frame < fixture.Source.Frames.Length; frame++) {
					int clip = fixture.Source.Frames[frame].ClipIndex;
					float speed = clip + 1f;
					for (int horizon = 0; horizon < 3; horizon++) {
						fixture.Source.Features[frame * LocomotionSchema.Dimension + horizon * 4 + 1] = speed * LocomotionSchema.PredictionTimes[horizon];
					}
					fixture.Source.Features[frame * LocomotionSchema.Dimension + 14] = speed;
				}
				for (int index = 0; index < 2; index++) {
					fixture.Source.Clips[index].Groups = new[] { 1 << index, 8 };
				}
				using (var sample = new MmdSampleWorld(0.05f)) {
					var manager = sample.World.EntityManager;
					Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
					manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
					var settings = fixture.Settings("Other", 1f, 0f, 2f, 0f);
					settings.Source = MmdAnimationLayerSource.Group;
					fixture.Add(manager, actor, new[] { settings });
					LocomotionSchema.WriteTrajectory(manager.GetBuffer<MmdSearchQueryElement>(actor), new float3(0f, 0f, 2f), 0f);
					sample.Tick();
					var layer = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					Assert.That(layer.ClipIndex, Is.Zero, "At double speed, the 1 m/s clip matches a 2 m/s query.");
					Assert.That(layer.Time, Is.EqualTo(0.1f).Within(0.00001f));
				}
			}
		}

		[Test]
		public void InactiveGroupSleepsAndWakesImmediatelyButFadeOutKeepsUpdating() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.05f)) {
					var manager = sample.World.EntityManager;
					Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
					manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
					var settings = fixture.Settings("Walk", 0f, 0f, 1f, 0f);
					settings.Source = MmdAnimationLayerSource.Group;
					fixture.Add(manager, actor, new[] { settings });
					sample.Tick();
					var sleeping = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					manager.GetBuffer<MmdSearchQueryElement>(actor).ResizeUninitialized(1);
					for (int tick = 0; tick < 4; tick++) {
						sample.Tick();
					}
					var after = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					Assert.That(after.Status, Is.EqualTo(MmdAnimationLayerStatus.Inactive));
					Assert.That(after.Time, Is.EqualTo(sleeping.Time));
					Assert.That(after.SearchCountdown, Is.EqualTo(sleeping.SearchCountdown));
					Assert.That(after.MatchedFrame, Is.EqualTo(-1));
					var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
					query.Resize(LocomotionSchema.Dimension, Unity.Collections.NativeArrayOptions.ClearMemory);
					LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 1f), 0f);
					MmdAnimationLayers.SetWeight(manager.GetBuffer<MmdAnimationLayerRequest>(actor), 0, 1f, 0f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.Ready));
					float time = manager.GetBuffer<MmdAnimationLayer>(actor)[0].Time;
					MmdAnimationLayers.SetWeight(manager.GetBuffer<MmdAnimationLayerRequest>(actor), 0, 0f, 0.1f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(0.5f).Within(0.00001f));
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Time, Is.GreaterThan(time));
					sample.Tick();
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.Inactive));
				}
			}
		}

		[Test]
		public void CommandsSwitchClipAndGroupWithoutManualTransitionBookkeeping() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.05f)) {
					var manager = sample.World.EntityManager;
					Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
					manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
					fixture.Add(manager, actor, new[] { fixture.Settings("Walk", 1f, 0f, 0f, 0f) });
					sample.Tick();
					MmdAnimationLayers.Play(manager.GetBuffer<MmdAnimationLayerRequest>(actor), 0, 2, 0f, 0f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].ClipIndex, Is.EqualTo(2));
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.InRange(3f, 5.99f));
					MmdAnimationLayers.SetGroup(manager.GetBuffer<MmdAnimationLayerRequest>(actor), 0, 2, 1f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].ClipIndex, Is.EqualTo(1));
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.Ready));
					Assert.That(manager.GetBuffer<MmdAnimationLayerRequest>(actor).Length, Is.Zero);
					sample.SetPaused(true);
					MmdAnimationLayers.SetWeight(manager.GetBuffer<MmdAnimationLayerRequest>(actor), 0, 0f, 0f);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayerRequest>(actor).Length, Is.EqualTo(1));
					sample.SetPaused(false);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.Zero);
					Assert.That(manager.GetBuffer<MmdAnimationLayerRequest>(actor).Length, Is.Zero);
				}
			}
		}
	}
}
