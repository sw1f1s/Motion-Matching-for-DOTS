using System;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Authoring;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace ColInt.MMD.Tests {
	public class MmdAnimationLayerTests {
		[Test]
		public void RealSampleRunKeepsUnmaskedBonesWhileRightArmUsesHammerMotionMatching() {
			var configuration = AssetDatabase.LoadAssetAtPath<MmdAnimationLayersConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleAnimationLayers.asset"));
			using (var sample = new MmdSampleWorld(0.1f)) {
				Entity actor = sample.Characters.Create(MotionDatabaseCodec.Load(configuration.Database.GetData<byte>()), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var database = manager.GetComponentData<MmdDatabase>(actor).Value;
				int run = MmdAnimationLayerBuilder.ResolveClip(ref database.Value, "Run", 0);
				manager.SetComponentData(actor, new MmdPlayback { ClipIndex = run, Speed = 1f, SearchCountdown = 100f });
				using var definition = MmdAnimationLayerBuilder.Build(database, configuration.Layers);
				MmdAnimationLayers.Add(manager, actor, definition);
				sample.Tick();
				var layer = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
				Assert.That(layer.Status, Is.EqualTo(MmdAnimationLayerStatus.Ready));
				Assert.That(layer.ClipIndex, Is.EqualTo(MmdAnimationLayerBuilder.ResolveClip(ref database.Value, "Hammer Run", 0)));
				Assert.That(layer.MatchedFrame, Is.GreaterThanOrEqualTo(0));
				var poses = manager.GetBuffer<MmdComposedPose>(actor);
				float[] mask = MmdAnimationLayerBuilder.ResolveMask(ref database.Value, configuration.Layers[0].Mask);
				int modified = 0;
				for (int bone = 0; bone < poses.Length; bone++) {
					var locomotion = MmdAnimationMath.PoseAt(ref database.Value, run, 0.1f, bone);
					var weapon = MmdAnimationMath.PoseAt(ref database.Value, layer.ClipIndex, layer.Time, bone);
					Assert.That(math.distance(poses[bone].Value.Position, locomotion.Position), Is.LessThan(0.00001f));
					Assert.That(math.distance(poses[bone].Value.Scale, locomotion.Scale), Is.LessThan(0.00001f));
					Assert.That(MmdRotationPacking.ErrorDegrees(poses[bone].Value.Rotation, math.slerp(locomotion.Rotation, weapon.Rotation, mask[bone] * layer.Weight)), Is.LessThan(0.01f));
					if (mask[bone] > 0f) {
						modified++;
					}
				}
				Assert.That(modified, Is.GreaterThan(5).And.LessThan(poses.Length / 2));
			}
		}

		[Test]
		public void SelectedBaseGroupsFadeOutForActionsAndFadeBackInWithoutChangingRequestedWeight() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.1f)) {
					var settings = fixture.Settings("Walk", 1f, 0.2f, 1f, 0f);
					settings.ActiveGroups = new[] { "Idle", "Walk" };
					Entity actor = Create(fixture, sample, new[] { settings }, false);
					var manager = sample.World.EntityManager;
					sample.Tick();
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(1f));
					manager.SetComponentData(actor, new MmdPlayback { ClipIndex = 2, Speed = 1f, SearchCountdown = 100f });
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(0.5f).Within(0.00001f));
					sample.Tick();
					var layer = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					Assert.That(layer.Weight, Is.Zero);
					Assert.That(manager.GetBuffer<MmdAnimationLayerControl>(actor)[0].TargetWeight, Is.EqualTo(1f));
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.EqualTo(6f));
					manager.SetComponentData(actor, new MmdPlayback { ClipIndex = 1, Speed = 1f, SearchCountdown = 100f });
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(0.5f).Within(0.00001f));
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(1f));
				}
			}
		}

		[Test]
		public void SelectedGroupsWithNoSelectionDisableLayerAndUnknownGroupsLeaveEntityUntouched() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.1f)) {
					var settings = fixture.Settings("Walk", 1f, 0f, 1f, 0f);
					settings.ActiveGroups = Array.Empty<string>();
					Entity actor = Create(fixture, sample, new[] { settings }, false);
					var manager = sample.World.EntityManager;
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.Zero);
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.Zero);
					settings.ActiveGroups = new[] { "Missing" };
					Entity invalid = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
					Assert.Throws<ArgumentException>(() => fixture.Add(manager, invalid, new[] { settings }));
					Assert.That(manager.HasBuffer<MmdAnimationLayer>(invalid), Is.False);
					Assert.That(manager.HasComponent<MmdAnimationLayersDefinitionReference>(invalid), Is.False);
				}
			}
		}

		[Test]
		public void GroupMasksSupportSparseIdsBeyond64AndKeepLayerWordsIndependent() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				var group = fixture.Source.Groups[3];
				group.Id = 130;
				fixture.Source.Groups[3] = group;
				var clip = fixture.Source.Clips[0];
				clip.Groups = new[] { 130 };
				fixture.Source.Clips[0] = clip;
				var first = fixture.Settings("Walk", 1f, 0f, 0f, 0f);
				first.ActiveGroups = new[] { "Other" };
				var second = fixture.Settings("Action", 1f, 0f, 0f, 0f);
				second.ActiveGroups = new[] { "Walk" };
				using (var sample = new MmdSampleWorld(0.1f)) {
					Entity actor = Create(fixture, sample, new[] { first, second }, false);
					var manager = sample.World.EntityManager;
					var layers = manager.GetBuffer<MmdAnimationLayer>(actor);
					var definitions = manager.GetComponentData<MmdAnimationLayersDefinitionReference>(actor).Value;
					Assert.That(definitions.Value.Layers[0].ActiveGroups.Length, Is.EqualTo(3));
					Assert.That(definitions.Value.Layers[1].ActiveGroups.Length, Is.EqualTo(1));
					Assert.That(definitions.Value.Layers[0].ActiveGroups[0], Is.Zero);
					Assert.That(definitions.Value.Layers[0].ActiveGroups[1], Is.Zero);
					Assert.That(definitions.Value.Layers[0].ActiveGroups[2], Is.EqualTo(2UL));
					Assert.That(definitions.Value.Layers[1].ActiveGroups[0], Is.EqualTo(2UL));
					sample.Tick();
					layers = manager.GetBuffer<MmdAnimationLayer>(actor);
					Assert.That(layers[0].Weight, Is.EqualTo(1f));
					Assert.That(layers[1].Weight, Is.Zero);
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.EqualTo(3f));
				}
			}
		}

		[Test]
		public void MaskedOverrideBlendsLocalChannelsAndPreservesBaseRootMotion() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				fixture.Mask.Bones[1] = new MmdAnimationBoneWeight { Path = "root/arm", Weight = 0.5f };
				using (var sample = new MmdSampleWorld(0.1f)) {
					Entity actor = Create(fixture, sample, new[] { fixture.Settings("Action", 1f, 0f, 0f, 0.6f) }, true);
					var manager = sample.World.EntityManager;
					manager.SetComponentData(actor, new MmdPlayback { ClipIndex = 1, Speed = 1f, SearchCountdown = 100f });
					sample.Tick();
					var database = manager.GetComponentData<MmdDatabase>(actor).Value;
					var poses = manager.GetBuffer<MmdComposedPose>(actor);
					var before = MmdAnimationMath.PoseAt(ref database.Value, 1, 0.1f, 1);
					var overlay = MmdAnimationMath.PoseAt(ref database.Value, 2, 0.6f, 1);
					Assert.That(math.distance(poses[1].Value.Position, math.lerp(before.Position, overlay.Position, 0.5f)), Is.LessThan(0.00001f));
					Assert.That(MmdRotationPacking.ErrorDegrees(poses[1].Value.Rotation, math.slerp(before.Rotation, overlay.Rotation, 0.5f)), Is.LessThan(0.01f));
					Assert.That(math.distance(poses[1].Value.Scale, math.lerp(before.Scale, overlay.Scale, 0.5f)), Is.LessThan(0.00001f));
					Assert.That(poses[0].Value.Position, Is.EqualTo(MmdAnimationMath.PoseAt(ref database.Value, 1, 0.1f, 0).Position));
					Assert.That(manager.GetComponentData<MmdRootMotion>(actor).Translation.z, Is.EqualTo(0.1f).Within(0.00001f));
					Assert.That(manager.GetComponentData<LocalTransform>(actor).Position.z, Is.EqualTo(0.1f).Within(0.00001f));
				}
			}
		}

		[Test]
		public void LaterLayersHavePriorityAndRotationOnlyPreservesPositionAndScale() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.1f)) {
					var upper = fixture.Settings("Action", 0.5f, 0f, 0f, 0.4f);
					upper.Channels = MotionPoseChannels.Rotation;
					Entity actor = Create(fixture, sample, new[] { fixture.Settings("Walk", 1f, 0f, 0f, 0.2f), upper }, false);
					sample.Tick();
					var pose = sample.World.EntityManager.GetBuffer<MmdComposedPose>(actor)[1].Value;
					Assert.That(pose.Position, Is.EqualTo(new float3(3f, 1.2f, 0f)));
					Assert.That(pose.Scale, Is.EqualTo(new float3(1.2f)));
					Assert.That(MmdRotationPacking.ErrorDegrees(pose.Rotation, quaternion.RotateZ(0.6f)), Is.LessThan(0.01f));
				}
			}
		}

		[Test]
		public void InterruptedFadeRemainsContinuousAndPauseDisabledFreezeLayers() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.05f)) {
					Entity actor = Create(fixture, sample, new[] { fixture.Settings("Walk", 1f, 0.2f, 1f, 0f) }, false);
					var manager = sample.World.EntityManager;
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(0.25f).Within(0.00001f));
					sample.Tick();
					MmdAnimationLayers.SetWeight(manager.GetBuffer<MmdAnimationLayerRequest>(actor), 0, 0f, 0.2f);
					sample.Tick();
					var layer = manager.GetBuffer<MmdAnimationLayer>(actor)[0];
					var pose = manager.GetBuffer<MmdComposedPose>(actor)[1].Value;
					Assert.That(layer.Weight, Is.EqualTo(0.25f).Within(0.00001f));
					sample.SetPaused(true);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Time, Is.EqualTo(layer.Time));
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position, Is.EqualTo(pose.Position));
					sample.SetPaused(false);
					manager.AddComponent<Disabled>(actor);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.EqualTo(layer.Weight));
					manager.RemoveComponent<Disabled>(actor);
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Weight, Is.Zero);
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.Zero);
					manager.DestroyEntity(actor);
					Assert.DoesNotThrow(sample.Tick);
				}
			}
		}

		[TestCase("Walk", 0f, 0.8f, 0.8f)]
		[TestCase("Walk", 2f, 0.8f, 0.2f)]
		[TestCase("Action", 2f, 0.8f, 1f)]
		public void LayersHoldLoopOrClampIndependentlyOfBaseSpeed(string group, float speed, float time, float expected) {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.2f)) {
					Entity actor = Create(fixture, sample, new[] { fixture.Settings(group, 1f, 0f, speed, time) }, false);
					var manager = sample.World.EntityManager;
					manager.SetComponentData(actor, new MmdPlayback { Speed = 0f, SearchCountdown = 100f });
					sample.Tick();
					Assert.That(manager.GetComponentData<MmdPlayback>(actor).Time, Is.Zero);
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Time, Is.EqualTo(expected).Within(0.00001f));
				}
			}
		}

		[Test]
		public void OverlayDoesNotEnterLocomotionInertializationHistory() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.05f)) {
					Entity layered = Create(fixture, sample, new[] { fixture.Settings("Action", 1f, 0f, 0f, 0.6f) }, false);
					Entity plain = Create(fixture, sample, Array.Empty<MmdAnimationLayerSettings>(), false);
					var manager = sample.World.EntityManager;
					foreach (Entity actor in new[] { layered, plain }) {
						manager.AddComponentData(actor, new MmdInertialization { PoseDuration = 0.2f });
						manager.AddComponent<MmdInertializationState>(actor);
						manager.AddBuffer<MmdInertialPose>(actor);
					}
					sample.Tick();
					foreach (Entity actor in new[] { layered, plain }) {
						manager.SetComponentData(actor, new MmdPlayback { ClipIndex = 1, Speed = 1f, SearchCountdown = 100f, Revision = 1 });
					}
					for (int tick = 0; tick < 8; tick++) {
						sample.Tick();
						var a = manager.GetBuffer<MmdInertialPose>(layered)[1];
						var b = manager.GetBuffer<MmdInertialPose>(plain)[1];
						Assert.That(a.Previous.Position, Is.EqualTo(b.Previous.Position));
						Assert.That(a.AngularVelocity, Is.EqualTo(b.AngularVelocity));
						Assert.That(manager.GetBuffer<MmdComposedPose>(layered)[1].Value.Position.x, Is.EqualTo(6f));
					}
				}
			}
		}

		[Test]
		public void InvalidRuntimeLayerIsReportedWithoutCorruptingBaseAndCanRecover() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				using (var sample = new MmdSampleWorld(0.1f)) {
					Entity actor = Create(fixture, sample, new[] { fixture.Settings("Walk", 1f, 0f, 0f, 0f) }, false);
					var manager = sample.World.EntityManager;
					var layers = manager.GetBuffer<MmdAnimationLayer>(actor);
					var layer = layers[0];
					layer.ClipIndex = 100;
					layers[0] = layer;
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.InvalidSettings));
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.Zero);
					layer.ClipIndex = 1;
					layers = manager.GetBuffer<MmdAnimationLayer>(actor);
					layers[0] = layer;
					sample.Tick();
					Assert.That(manager.GetBuffer<MmdAnimationLayer>(actor)[0].Status, Is.EqualTo(MmdAnimationLayerStatus.Ready));
					Assert.That(manager.GetBuffer<MmdComposedPose>(actor)[1].Value.Position.x, Is.EqualTo(3f));
				}
			}
		}

		[Test]
		public void MaskPathsResolveInAnyOrderAndInvalidSetupLeavesEntityUntouched() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				Array.Reverse(fixture.Mask.Bones);
				using (var database = MotionDatabaseBuilder.Build(fixture.Source)) {
					Assert.That(MmdAnimationLayerBuilder.ResolveMask(ref database.Value, fixture.Mask), Is.EqualTo(new[] { 0f, 1f }));
				}
				using (var sample = new MmdSampleWorld(0.1f)) {
					var manager = sample.World.EntityManager;
					Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), false, LocalTransform.Identity);
					fixture.Mask.Bones[0] = new MmdAnimationBoneWeight { Path = "missing", Weight = 1f };
					Assert.Throws<ArgumentException>(() => fixture.Add(manager, actor, new[] { fixture.Settings("Walk", 1f, 0f, 0f, 0f) }));
					Assert.That(manager.HasBuffer<MmdAnimationLayer>(actor), Is.False);
					Assert.That(manager.HasComponent<MmdAnimationLayersDefinitionReference>(actor), Is.False);
				}
			}
		}

		[Test]
		public void LayerOnlyClipsRoundTripButCannotBeSelectedBySearchOrActionRequests() {
			using (var fixture = new MmdAnimationLayerFixture()) {
				var clip = fixture.Source.Clips[1];
				clip.ActionId = 0;
				clip.Mode = MotionPlaybackMode.LayerOnly;
				fixture.Source.Clips[1] = clip;
				var group = fixture.Source.Groups[1];
				group.ActionId = 0;
				group.Mode = MotionPlaybackMode.LayerOnly;
				fixture.Source.Groups[1] = group;
				using (var sample = new MmdSampleWorld(0.1f)) {
					Entity actor = sample.Characters.Create(MotionDatabaseCodec.Load(MotionDatabaseCodec.Write(fixture.Source)), false, LocalTransform.Identity);
					var manager = sample.World.EntityManager;
					var database = manager.GetComponentData<MmdDatabase>(actor).Value;
					Assert.That(database.Value.Clips[1].Mode, Is.EqualTo(MotionPlaybackMode.LayerOnly));
					Assert.Throws<ArgumentException>(() => MotionDatabaseGroups.GetActionId(ref database.Value, new FixedString64Bytes("Walk")));
					MmdMotionGroups.Set(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required, group.Id);
					int frame = MotionSearch.Find(ref database.Value, manager.GetBuffer<MmdSearchQueryElement>(actor), default, manager.GetBuffer<MmdMotionGroup>(actor), 11);
					Assert.That(frame, Is.EqualTo(-1));
					manager.SetComponentData(actor, new MmdActionRequest { ActionId = -1, PreferredClip = -1, Revision = 1 });
					sample.Tick();
					Assert.That(manager.GetComponentData<MmdActionState>(actor).Rejected, Is.True);
				}
			}
		}
		private static Entity Create(MmdAnimationLayerFixture fixture, MmdSampleWorld sample, MmdAnimationLayerSettings[] layers, bool rootMotion) {
			Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(fixture.Source), rootMotion, LocalTransform.Identity);
			sample.World.EntityManager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f, MatchedFrame = -1 });
			fixture.Add(sample.World.EntityManager, actor, layers);
			return actor;
		}
	}
}
