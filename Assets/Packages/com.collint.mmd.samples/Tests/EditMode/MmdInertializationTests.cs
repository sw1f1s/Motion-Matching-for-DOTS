using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdInertializationTests {
		[Test]
		public void CompensatingParentAndChildHalfTurnsDoNotSpinTheDisplayedChildDuringTransition() {
			var source = MotionDatabaseFixture.Create();
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				bool halfTurn = source.Frames[frame].ClipIndex == 1;
				quaternion rotation = halfTurn ? new quaternion(0f, 1f, 0f, 0f) : quaternion.identity;
				var parent = source.Poses[frame * 2];
				parent.Rotation = rotation;
				source.Poses[frame * 2] = parent;
				var child = source.Poses[frame * 2 + 1];
				child.Rotation = rotation;
				source.Poses[frame * 2 + 1] = child;
			}
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				manager.AddComponentData(actor, new MmdInertialization { PoseDuration = 0.2f });
				manager.AddComponent<MmdInertializationState>(actor);
				manager.AddBuffer<MmdInertialPose>(actor);
				manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
				for (int tick = 0; tick < 6; tick++) {
					sample.Tick();
				}
				Switch(manager, actor, 1);
				for (int tick = 0; tick < 24; tick++) {
					sample.Tick();
					var poses = manager.GetBuffer<MmdLocalPose>(actor);
					quaternion displayed = math.mul(poses[0].Value.Rotation, poses[1].Value.Rotation);
					Assert.That(MmdRotationPacking.ErrorDegrees(displayed, quaternion.identity), Is.LessThan(0.01f));
				}
			}
		}

		[TestCase(-1f, false)]
		[TestCase(1f, false)]
		[TestCase(-1f, true)]
		[TestCase(1f, true)]
		public void HipTurnsPreserveTheKneeHingeThroughInterruptedTransitions(float direction, bool interrupt) {
			var source = MotionDatabaseFixture.Create();
			source.Parents = new[] { -1, 0, 1, 2 };
			source.BoneNameHashes = new[] { 1, 2, 3, 4 };
			source.Poses = new JointPose[source.Frames.Length * 4];
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				bool turning = source.Frames[frame].ClipIndex == 1;
				for (int bone = 0; bone < 4; bone++) {
					source.Poses[frame * 4 + bone] = new JointPose {
						Position = new float3(0f, bone == 0 ? 0f : 0.4f, 0f), Rotation = quaternion.identity, Scale = new float3(1f)
					};
				}
				var thigh = source.Poses[frame * 4 + 2];
				thigh.Rotation = quaternion.RotateY(turning ? direction * 2f : 0f);
				source.Poses[frame * 4 + 2] = thigh;
				var knee = source.Poses[frame * 4 + 3];
				knee.Rotation = quaternion.RotateZ(turning ? 1.6f : 0.4f);
				source.Poses[frame * 4 + 3] = knee;
			}
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				manager.AddComponentData(actor, new MmdInertialization { PoseDuration = 0.2f });
				manager.AddComponent<MmdInertializationState>(actor);
				manager.AddBuffer<MmdInertialPose>(actor);
				manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
				for (int tick = 0; tick < 6; tick++) {
					sample.Tick();
				}
				Switch(manager, actor, 1);
				for (int tick = 0; tick < 30; tick++) {
					if (interrupt && tick == 4) {
						Switch(manager, actor, 0);
					}
					sample.Tick();
					var knee = manager.GetBuffer<MmdLocalPose>(actor)[3].Value.Rotation;
					float3 bend = MmdAnimationMath.RotationVector(knee);
					Assert.That(math.length(bend.xy), Is.LessThan(0.0001f), "A parent turn must not add sideways rotation to a hinge joint.");
				}
				var expected = quaternion.RotateZ(interrupt ? 0.4f : 1.6f);
				Assert.That(MmdRotationPacking.ErrorDegrees(manager.GetBuffer<MmdLocalPose>(actor)[3].Value.Rotation, expected), Is.LessThan(0.01f));
			}
		}

		[TestCase(0.2f, 2)]
		[TestCase(0f, 2)]
		[TestCase(0.2f, 0)]
		[TestCase(0.2f, 1)]
		public void SettledStopPreservesFootprintOnlyForConfiguredGroupsAndReleasesAlignmentOnMovement(float duration, int stationaryGroup) {
			var source = MotionDatabaseFixture.Create();
			var stop = source.Clips[0];
			stop.Loop = false;
			source.Clips[0] = stop;
			for (int frame = 0; frame < 22; frame++) {
				var value = source.Frames[frame];
				value.Root = RigidTransform.identity;
				source.Frames[frame] = value;
				float offset = frame < 11 ? 0.07f : 0f;
				var pose = source.Poses[frame * 2];
				pose.Position.x = offset;
				source.Poses[frame * 2] = pose;
				source.Features[frame * source.FeatureCount + 15] = offset - 0.2f;
				source.Features[frame * source.FeatureCount + 21] = offset + 0.2f;
			}
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), true, LocalTransform.Identity);
				manager.AddComponentData(actor, new MmdInertialization { PoseDuration = duration, RootDuration = 0.1f });
				MmdMotionGroups.Set(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.StationaryPose, stationaryGroup);
				manager.AddComponent<MmdInertializationState>(actor);
				manager.AddBuffer<MmdInertialPose>(actor);
				manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, SearchCountdown = 100f });
				for (int tick = 0; tick < 6; tick++) {
					sample.Tick();
				}
				Switch(manager, actor, 1);
				for (int tick = 0; tick < 30; tick++) {
					sample.Tick();
				}
				float expected = duration > 0f && System.Array.IndexOf(source.Clips[1].Groups, stationaryGroup) >= 0 ? 0.07f : 0f;
				Assert.That(manager.GetBuffer<MmdLocalPose>(actor)[0].Value.Position.x, Is.EqualTo(expected).Within(0.00001f));
				Assert.That(manager.GetComponentData<MmdInertializationState>(actor).StationaryPoseOffset.x, Is.EqualTo(expected).Within(0.00001f));
				Assert.That(manager.GetComponentData<LocalTransform>(actor).Position, Is.EqualTo(float3.zero));
				Assert.That(manager.GetComponentData<MmdRootMotion>(actor).Translation, Is.EqualTo(float3.zero));
				Switch(manager, actor, 0);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdInertializationState>(actor).StationaryPoseOffset, Is.EqualTo(float3.zero));
				for (int tick = 0; tick < 30; tick++) {
					sample.Tick();
				}
				Assert.That(manager.GetBuffer<MmdLocalPose>(actor)[0].Value.Position.x, Is.EqualTo(0.07f).Within(0.00001f));
			}
		}

		[TestCase(0.4f)]
		[TestCase(0f)]
		public void IdleDurationEasesToAuthoredPoseWithoutChangingHeadingAndResetsOnMovement(float stationaryDuration) {
			var source = MotionDatabaseFixture.Create();
			var stop = source.Clips[0];
			stop.Loop = false;
			source.Clips[0] = stop;
			for (int frame = 0; frame < 22; frame++) {
				var value = source.Frames[frame];
				value.Root = RigidTransform.identity;
				source.Frames[frame] = value;
				var pose = source.Poses[frame * 2];
				pose.Rotation = quaternion.RotateY(frame < 11 ? 0.4f : -0.2f);
				source.Poses[frame * 2] = pose;
			}
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var transform = LocalTransform.FromPositionRotation(new float3(2f, 0f, 3f), quaternion.RotateY(0.7f));
				Entity actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, transform);
				manager.AddComponentData(actor, new MmdInertialization {
					PoseDuration = 0.2f, StationaryPoseDuration = stationaryDuration, RootDuration = 0.1f
				});
				MmdMotionGroups.Set(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.StationaryPose, source.Clips[1].Groups[0]);
				manager.AddComponent<MmdInertializationState>(actor);
				manager.AddBuffer<MmdInertialPose>(actor);
				manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, MatchedFrame = -1, SearchCountdown = 100f });
				for (int tick = 0; tick < 6; tick++) {
					sample.Tick();
				}
				var before = manager.GetBuffer<MmdLocalPose>(actor)[0].Value.Rotation;
				Switch(manager, actor, 1);
				sample.Tick();
				float duration = math.max(0.2f, stationaryDuration);
				Assert.That(manager.GetComponentData<MmdInertializationState>(actor).PoseDuration, Is.EqualTo(duration));
				Assert.That(MmdRotationPacking.ErrorDegrees(before, manager.GetBuffer<MmdLocalPose>(actor)[0].Value.Rotation), Is.LessThan(0.2f));
				for (int tick = 0; tick < 12; tick++) {
					sample.Tick();
				}
				var database = manager.GetComponentData<MmdDatabase>(actor).Value;
				var playback = manager.GetComponentData<MmdPlayback>(actor);
				var target = MmdAnimationMath.PoseAt(ref database.Value, 1, playback.Time, 0);
				float error = MmdRotationPacking.ErrorDegrees(target.Rotation, manager.GetBuffer<MmdLocalPose>(actor)[0].Value.Rotation);
				if (stationaryDuration > 0.2f) {
					Assert.That(error, Is.GreaterThan(1f), "Idle continues easing after the ordinary transition duration.");
				} else {
					Assert.That(error, Is.LessThan(0.001f));
				}
				for (int tick = 0; tick < 15; tick++) {
					sample.Tick();
				}
				Assert.That(MmdRotationPacking.ErrorDegrees(target.Rotation, manager.GetBuffer<MmdLocalPose>(actor)[0].Value.Rotation), Is.LessThan(0.001f), "The final idle rotation is authored, without a retained yaw correction.");
				Assert.That(manager.GetComponentData<LocalTransform>(actor).Position, Is.EqualTo(transform.Position));
				Assert.That(manager.GetComponentData<LocalTransform>(actor).Rotation, Is.EqualTo(transform.Rotation));
				Assert.That(manager.GetComponentData<MmdRootMotion>(actor).Translation, Is.EqualTo(float3.zero));
				Assert.That(manager.GetComponentData<MmdRootMotion>(actor).Rotation, Is.EqualTo(quaternion.identity));
				Switch(manager, actor, 0);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdInertializationState>(actor).PoseDuration, Is.EqualTo(0.2f));
				Switch(manager, actor, 1);
				sample.Tick();
				Switch(manager, actor, 0);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdInertializationState>(actor).PoseDuration, Is.EqualTo(0.2f), "Movement interrupts idle using the ordinary duration.");
			}
		}

		[Test]
		public void SwitchingAndInterruptingPreservesPoseContinuityThenReachesTarget() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity character = CreateCharacter(sample);
				for (int tick = 0; tick < 6; tick++) {
					sample.Tick();
				}
				var previous = manager.GetBuffer<MmdLocalPose>(character)[1].Value;
				Switch(manager, character, 1);
				sample.Tick();
				var current = manager.GetBuffer<MmdLocalPose>(character)[1].Value;
				Assert.That(math.distance(previous.Position, current.Position), Is.LessThan(0.05f));
				Assert.That(math.length(MmdAnimationMath.RotationVector(math.mul(current.Rotation, math.inverse(previous.Rotation)))), Is.LessThan(0.05f));
				Assert.That(math.distance(previous.Scale, current.Scale), Is.LessThan(0.01f));
				for (int tick = 0; tick < 3; tick++) {
					sample.Tick();
				}
				previous = manager.GetBuffer<MmdLocalPose>(character)[1].Value;
				Switch(manager, character, 0);
				sample.Tick();
				current = manager.GetBuffer<MmdLocalPose>(character)[1].Value;
				Assert.That(math.distance(previous.Position, current.Position), Is.LessThan(0.6f), "An interrupted blend starts from its displayed pose.");
				for (int tick = 0; tick < 15; tick++) {
					sample.Tick();
				}
				var playback = manager.GetComponentData<MmdPlayback>(character);
				var database = manager.GetComponentData<MmdDatabase>(character).Value;
				var target = MmdAnimationMath.PoseAt(ref database.Value, playback.ClipIndex, playback.Time, 1);
				current = manager.GetBuffer<MmdLocalPose>(character)[1].Value;
				Assert.That(math.distance(current.Position, target.Position), Is.LessThan(0.00001f));
				Assert.That(math.abs(math.dot(current.Rotation.value, target.Rotation.value)), Is.EqualTo(1f).Within(0.00001f));
			}
		}

		[Test]
		public void RootVelocityBlendsWhileActionsRemainExactAndPauseDisabledFreezeHistory() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity character = CreateCharacter(sample);
				sample.Tick();
				Switch(manager, character, 1);
				sample.Tick();
				var motion = manager.GetComponentData<MmdRootMotion>(character);
				Assert.That(motion.Translation.z, Is.GreaterThan(0f).And.LessThan(sample.Timestep * 0.1f));
				var history = manager.GetComponentData<MmdInertializationState>(character);
				var pose = manager.GetBuffer<MmdLocalPose>(character)[1].Value;
				sample.SetPaused(true);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdInertializationState>(character).Elapsed, Is.EqualTo(history.Elapsed));
				Assert.That(manager.GetBuffer<MmdLocalPose>(character)[1].Value.Position, Is.EqualTo(pose.Position));
				sample.SetPaused(false);
				manager.AddComponent<Disabled>(character);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdInertializationState>(character).Elapsed, Is.EqualTo(history.Elapsed));
				manager.RemoveComponent<Disabled>(character);
				manager.SetComponentData(character, new MmdActionRequest { ActionId = 7, Revision = 1, PreferredClip = -1 });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdRootMotion>(character).Translation.z, Is.EqualTo(sample.Timestep * 2f).Within(0.00001f));
				Assert.That(manager.GetComponentData<MmdActionState>(character).ActionId, Is.EqualTo(7));
			}
		}

		[Test]
		public void ZeroDurationsPreserveSampledPoseAndRootExactly() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				Entity character = CreateCharacter(sample);
				manager.SetComponentData(character, new MmdInertialization());
				sample.Tick();
				Switch(manager, character, 1);
				sample.Tick();
				Assert.That(manager.GetBuffer<MmdLocalPose>(character)[1].Value.Position.x, Is.EqualTo(3f));
				Assert.That(manager.GetComponentData<MmdRootMotion>(character).Translation.z, Is.EqualTo(sample.Timestep).Within(0.00001f));
			}
		}

		[TestCase(-20f)]
		[TestCase(20f)]
		public void RotationCorrectionSettlesWithoutOvershootingOrAddingAnotherAxis(float speed) {
			float3 offset = new float3(0f, 0f, 0.4f);
			float3 velocity = new float3(12f, -15f, speed);
			float previous = offset.z;
			for (int tick = 0; tick <= 60; tick++) {
				float3 correction = MmdAnimationMath.DecayRotation(offset, velocity, tick / 60f, 0.2f);
				Assert.That(math.length(correction.xy), Is.LessThan(0.000001f), "Decay must not invent a twist axis absent from the rotation correction.");
				Assert.That(correction.z, Is.InRange(-0.000001f, previous + 0.000001f), "Angular velocity must not reverse or grow the correction.");
				previous = correction.z;
			}
			Assert.That(previous, Is.Zero);
		}

		[Test]
		public void DecayPreservesInitialVelocityAndFinishesWithoutResidualOrQuaternionSignJump() {
			const float epsilon = 0.00001f;
			float3 offset = new float3(1f, -2f, 3f);
			float3 velocity = new float3(-0.4f, 0.7f, 0.1f);
			Assert.That(MmdAnimationMath.Decay(offset, velocity, 0f, 0.2f), Is.EqualTo(offset));
			float3 derivative = (MmdAnimationMath.Decay(offset, velocity, epsilon, 0.2f) - offset) / epsilon;
			Assert.That(math.distance(derivative, velocity), Is.LessThan(0.03f));
			Assert.That(math.length(MmdAnimationMath.Decay(offset, velocity, 0.2f - epsilon, 0.2f)), Is.LessThan(0.00001f));
			Assert.That(MmdAnimationMath.Decay(offset, velocity, 0.2f, 0.2f), Is.EqualTo(float3.zero));
			quaternion rotation = quaternion.RotateY(0.7f);
			Assert.That(math.distance(MmdAnimationMath.RotationVector(rotation), MmdAnimationMath.RotationVector(new quaternion(-rotation.value))), Is.LessThan(0.00001f));
		}

		private static Entity CreateCharacter(MmdSampleWorld sample) {
			var source = MotionDatabaseFixture.Create();
			for (int frame = 11; frame < 22; frame++) {
				var pose = source.Poses[frame * 2 + 1];
				pose.Position.x = 3f;
				pose.Rotation = quaternion.RotateY(math.PI * 0.5f);
				pose.Scale = new float3(1.2f);
				source.Poses[frame * 2 + 1] = pose;
			}
			Entity character = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
			var manager = sample.World.EntityManager;
			manager.AddComponentData(character, new MmdInertialization { PoseDuration = 0.2f, RootDuration = 0.1f });
			manager.AddComponent<MmdInertializationState>(character);
			manager.AddBuffer<MmdInertialPose>(character);
			manager.SetComponentData(character, new MmdPlayback { Speed = 1f, MatchedFrame = -1, SearchCountdown = 100f });
			return character;
		}

		private static void Switch(EntityManager manager, Entity character, int clip) {
			var playback = manager.GetComponentData<MmdPlayback>(character);
			playback.ClipIndex = clip;
			playback.Time = 0f;
			playback.Revision++;
			manager.SetComponentData(character, playback);
		}
	}
}
