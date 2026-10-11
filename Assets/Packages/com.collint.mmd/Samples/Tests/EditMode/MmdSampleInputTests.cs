using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Debugging.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdSampleInputTests {
		[TestCase(1f)]
		[TestCase(-1f)]
		public void ExactReversalKeepsTurnSideThroughQuaternionRoundoff(float sign) {
			float error = sign;
			for (int tick = 0; tick < 60; tick++) {
				float perturbation = tick % 2 == 0 ? 0.000001f : -0.000001f;
				error = ColInt.MMD.Samples.Character.Movement.Facing.Helpers.MmdSampleFacing.YawError(quaternion.RotateY(perturbation), new float2(0f, -1f), error);
				Assert.That(math.sign(error), Is.EqualTo(sign));
			}
		}

		[TestCase(1f, 0f)]
		[TestCase(-1f, 0f)]
		[TestCase(0f, -1f)]
		public void MovementTurnsThroughEcsRootWithoutOvershootAndPauseStopsIt(float x, float z) {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				Entity character = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdSampleCharacterControl { TurnSpeed = 2f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(x, z) });
				sample.Tick();
				var initial = manager.GetComponentData<LocalTransform>(character);
				Assert.That(math.abs(math.atan2(initial.Forward().x, initial.Forward().z)), Is.EqualTo(2f / 60f).Within(0.00001f));
				sample.SetPaused(true);
				sample.Tick();
				Assert.That(manager.GetComponentData<LocalTransform>(character).Rotation, Is.EqualTo(initial.Rotation));
				sample.SetPaused(false);
				for (int tick = 0; tick < 600; tick++) {
					sample.Tick();
				}
				float3 forward = manager.GetComponentData<LocalTransform>(character).Forward();
				Assert.That(math.distance(forward.xz, new float2(x, z)), Is.LessThan(0.00001f));
				var query = manager.GetBuffer<MmdSearchQueryElement>(character);
				Assert.That(query[2].Value, Is.Zero.Within(0.00001f));
				Assert.That(query[3].Value, Is.EqualTo(1f).Within(0.00001f));
			}
		}

		[Test]
		public void StoppingPreservesHeadingDespiteCameraRotationAndOutgoingTurnInertia() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				Entity character = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 1f, TurnSpeed = 4.5f, IdleTags = 1, WalkTags = 2, TurnTags = 8 });
				manager.AddComponentData(character, new MmdInertialization { PoseDuration = 0.2f, RootDuration = 0.1f });
				manager.AddComponent<MmdInertializationState>(character);
				manager.AddBuffer<MmdInertialPose>(character);
				sample.SetInput(new MmdSampleInput { Movement = new float2(1f, 0f) });
				for (int tick = 0; tick < 30; tick++) {
					sample.Tick();
				}
				var heading = manager.GetComponentData<LocalTransform>(character).Rotation;
				Assert.That(math.abs(heading.value.y), Is.GreaterThan(0.01f));
				var inertia = manager.GetComponentData<MmdInertializationState>(character);
				inertia.RootAngularVelocity = new float3(0f, 1f, 0f);
				manager.SetComponentData(character, inertia);
				for (int tick = 0; tick < 120; tick++) {
					float yaw = tick * 0.1f;
					sample.SetInput(new MmdSampleInput { Facing = new float2(math.sin(yaw), math.cos(yaw)) });
					sample.Tick();
					Assert.That(math.distance(manager.GetComponentData<LocalTransform>(character).Rotation.value, heading.value), Is.LessThan(0.000001f));
					Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(1u));
					Assert.That(manager.GetComponentData<MmdRootMotion>(character).Rotation, Is.EqualTo(quaternion.identity));
					var query = manager.GetBuffer<MmdSearchQueryElement>(character);
					for (int point = 0; point < 3; point++) {
						Assert.That(query[point * 4 + 2].Value, Is.Zero);
						Assert.That(query[point * 4 + 3].Value, Is.EqualTo(1f));
					}
				}
			}
		}

		[TestCase(0f, 0f, false, 0f, 1u)]
		[TestCase(0f, 0f, true, 0f, 1u)]
		[TestCase(0f, 1f, false, 0f, 2u)]
		[TestCase(1f, 1f, true, 0f, 4u)]
		[TestCase(0f, 0f, false, -1f, 1u)]
		[TestCase(0f, 1f, false, 1f, 2u)]
		public void LocomotionIntentFiltersOnlyControlledActiveCharacters(float x, float y, bool run, float turn, uint tags) {
			using (var world = new MmdSampleWorld(0.1f)) {
				var manager = world.World.EntityManager;
				Entity controlled = world.Characters.Create(default, true, LocalTransform.Identity);
				Entity disabled = world.Characters.Create(default, true, LocalTransform.Identity);
				Entity other = world.Characters.Create(default, true, LocalTransform.Identity);
				var control = new MmdSampleCharacterControl {
					WalkSpeed = 2f, RunSpeed = 4f, TurnSpeed = 1f,
					IdleTags = 1, WalkTags = 2, RunTags = 4, TurnTags = 8
				};
				var settings = new MmdSearchSettings { RequiredTags = 32, ExcludedTags = 16, Interval = 0.25f, SwitchPenalty = 0.4f };
				manager.AddComponent<MmdSampleMovement>(controlled);
				manager.AddComponentData(controlled, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(controlled, control);
				manager.AddComponent<MmdSampleMovement>(disabled);
				manager.AddComponentData(disabled, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(disabled, control);
				manager.AddComponentData(controlled, settings);
				manager.AddComponentData(disabled, settings);
				manager.AddComponentData(other, settings);
				manager.AddComponentData(controlled, new MmdPlayback { SearchCountdown = 1f, Time = 0.7f, Revision = 9 });
				manager.AddComponentData(disabled, new MmdPlayback { SearchCountdown = 1f });
				manager.AddComponent<Disabled>(disabled);
				world.SetInput(new MmdSampleInput { Movement = new float2(x, y), Run = run, Facing = turn == 0f ? float2.zero : new float2(math.sin(turn), math.cos(turn)) });
				world.Tick();
				var actual = manager.GetComponentData<MmdSearchSettings>(controlled);
				Assert.That(actual.RequiredTags, Is.EqualTo(tags));
				Assert.That(actual.ExcludedTags, Is.EqualTo(16u));
				Assert.That(actual.Interval, Is.EqualTo(0.25f));
				Assert.That(actual.SwitchPenalty, Is.EqualTo(0.4f));
				var playback = manager.GetComponentData<MmdPlayback>(controlled);
				Assert.That(playback.SearchCountdown, Is.Zero);
				Assert.That(playback.Time, Is.EqualTo(0.7f));
				Assert.That(playback.Revision, Is.EqualTo(9u));
				Assert.That(manager.GetComponentData<MmdSearchSettings>(disabled).RequiredTags, Is.EqualTo(32u));
				Assert.That(manager.GetComponentData<MmdSearchSettings>(other).RequiredTags, Is.EqualTo(32u));
				world.SetPaused(true);
				world.SetInput(new MmdSampleInput { Movement = new float2(1f, 0f), Run = !run });
				world.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(controlled).RequiredTags, Is.EqualTo(tags));
			}
		}

		[TestCase(5f, 4u, 1f)]
		[TestCase(3f, 4u, 0.6f)]
		[TestCase(1f, 2u, 0.5f)]
		[TestCase(0f, 1u, 1f)]
		public void ReachableSpeedSelectsGaitAndLimitsPlaybackTempo(float reachableSpeed, uint tags, float tempo) {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var character = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 2f, RunSpeed = 5f, IdleTags = 1, WalkTags = 2, RunTags = 4 });
				manager.AddComponent<MmdSearchSettings>(character);
				manager.AddComponentData(character, new MmdPlayback { Speed = 1f });
				manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment {
					MaximumSpeed = reachableSpeed, MovementFraction = reachableSpeed / 5f
				});
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(tags));
				Assert.That(manager.GetComponentData<MmdPlayback>(character).Speed, Is.EqualTo(tempo).Within(0.00001f));
			}
		}

		[Test]
		public void CollisionFractionDoesNotApplyPredictedAccelerationAndBrakingTwice() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var character = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 2f, RunSpeed = 5f, IdleTags = 1, WalkTags = 2, RunTags = 4 });
				manager.AddComponent<MmdSearchSettings>(character);
				manager.AddComponentData(character, new MmdPlayback { Speed = 1f });
				manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment { MaximumSpeed = 0.2f, MovementFraction = 0.8f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(4u));
				Assert.That(manager.GetComponentData<MmdPlayback>(character).Speed, Is.EqualTo(0.8f).Within(0.00001f));
			}
		}

		[Test]
		public void WallTurnKeepsAuthoredTempoUntilHeadingReachedAndReleasedInputCancelsIt() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var character = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl {
					WalkSpeed = 2f, RunSpeed = 5f, TurnSpeed = 4.5f, IdleTags = 1, WalkTags = 2, RunTags = 4, TurnTags = 8
				});
				manager.AddComponentData(character, new MmdSearchSettings { RequiredTags = 1 });
				manager.AddComponentData(character, new MmdPlayback { Speed = 0.3f });
				manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment { MaximumSpeed = 0f, MovementFraction = 0f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(1f, 0f), Run = true });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(8u));
				Assert.That(manager.GetComponentData<MmdPlayback>(character).Speed, Is.EqualTo(1f));
				manager.SetComponentData(character, LocalTransform.FromRotation(quaternion.RotateY(math.PI * 0.5f - 0.1f)));
				manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment { MaximumSpeed = float.PositiveInfinity, MovementFraction = 1f });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(8u), "A clear future path must not interrupt the turn before alignment.");
				manager.SetComponentData(character, LocalTransform.FromRotation(quaternion.RotateY(math.PI * 0.5f - 0.01f)));
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(4u));
				manager.SetComponentData(character, LocalTransform.Identity);
				manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment { MaximumSpeed = 0f, MovementFraction = 0f });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(8u));
				sample.SetInput(default);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(1u));
			}
		}

		[Test]
		public void CollisionGaitBoundaryDoesNotAlternateWalkAndRunOnSmallSpeedChanges() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var character = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 2f, RunSpeed = 5f, IdleTags = 1, WalkTags = 2, RunTags = 4 });
				manager.AddComponentData(character, new MmdSearchSettings { RequiredTags = 2 });
				manager.AddComponentData(character, new MmdPlayback { Speed = 1f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
				float[] fractions = { 0.59f, 0.61f, 0.59f, 0.5f, 0.45f, 0.43f, 0.45f, 0.59f };
				uint[] expected = { 2, 4, 4, 4, 4, 2, 2, 2 };
				for (int step = 0; step < fractions.Length; step++) {
					manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment {
						MaximumSpeed = fractions[step] * 5f, MovementFraction = fractions[step]
					});
					sample.Tick();
					Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(expected[step]));
				}
			}
		}

		[Test]
		public void GroundSpeedSelectionDoesNotRetuneExplicitActionPlayback() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var character = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 2f, IdleTags = 1, WalkTags = 2 });
				manager.AddComponentData(character, new MmdSearchSettings { RequiredTags = 8 });
				manager.AddComponentData(character, new MmdPlayback { Speed = 0.75f });
				manager.SetComponentData(character, new MmdActionState { ActionId = 7 });
				manager.SetComponentData(character, new ColInt.MMD.Physics.Components.MmdTrajectoryEnvironment { MaximumSpeed = 0f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f) });
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdSearchSettings>(character).RequiredTags, Is.EqualTo(8u));
				Assert.That(manager.GetComponentData<MmdPlayback>(character).Speed, Is.EqualTo(0.75f));
			}
		}

		[Test]
		public void StableLocomotionIntentPreservesSearchCountdown() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var manager = world.World.EntityManager;
				Entity character = world.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { IdleTags = 1 });
				manager.AddComponentData(character, new MmdSearchSettings { RequiredTags = 1 });
				manager.AddComponentData(character, new MmdPlayback { SearchCountdown = 1f });
				world.Tick();
				Assert.That(manager.GetComponentData<MmdPlayback>(character).SearchCountdown, Is.EqualTo(1f));
			}
		}

		[Test]
		public void WorldDirectionUsesCharacterSpaceAndMovementMagnitudeDoesNotChangePrediction() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var pose = LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(math.PI * 0.5f));
				Entity character = world.Characters.Create(default, true, pose);
				var manager = world.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 2f, RunSpeed = 4f, TurnSpeed = 1f });
				world.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f) });
				world.Tick();
				var query = manager.GetBuffer<MmdSearchQueryElement>(character);
				Assert.That(query[12].Value, Is.Zero);
				Assert.That(query[2].Value, Is.EqualTo(-math.sin(0.2f)).Within(0.00001f));
				Assert.That(query[3].Value, Is.EqualTo(math.cos(0.2f)).Within(0.00001f));
				Assert.That(query[0].Value, Is.LessThan(0f));
				Assert.That(query[1].Value, Is.GreaterThan(0f));
				manager.SetComponentData(character, new MmdSampleTrajectoryState());
				world.SetInput(new MmdSampleInput { Movement = new float2(1f, 1f), Run = true });
				world.Tick();
				query = manager.GetBuffer<MmdSearchQueryElement>(character);
				float predictedX = query[8].Value;
				float predictedZ = query[9].Value;
				manager.SetComponentData(character, new MmdSampleTrajectoryState());
				world.SetInput(new MmdSampleInput { Movement = math.normalize(new float2(1f, 1f)), Run = true });
				world.Tick();
				query = manager.GetBuffer<MmdSearchQueryElement>(character);
				Assert.That(query[8].Value, Is.EqualTo(predictedX).Within(0.00001f));
				Assert.That(query[9].Value, Is.EqualTo(predictedZ).Within(0.00001f));
				Assert.That(manager.GetComponentData<LocalTransform>(character).Rotation, Is.EqualTo(pose.Rotation));
			}
		}

		[Test]
		public void PreviewInputPredictsTrajectoryWithoutDatabaseOrChangingTransform() {
			using (var world = new MmdSampleWorld(0.1f)) {
				Entity character = world.Characters.Create(default, true, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 2f, RunSpeed = 4f, TurnSpeed = 1f });
				world.SetInput(new MmdSampleInput { Movement = new float2(1f, 0f), Facing = new float2(math.sin(0.5f), math.cos(0.5f)), Run = true });
				world.Tick();
				var query = manager.GetBuffer<MmdSearchQueryElement>(character);
				Assert.That(query[0].Value, Is.GreaterThan(0f));
				Assert.That(query[1].Value, Is.GreaterThan(query[0].Value));
				Assert.That(query[2].Value, Is.EqualTo(math.sin(0.2f)).Within(0.00001f));
				Assert.That(query[12].Value, Is.Zero);
				Assert.That(manager.HasComponent<MmdDatabase>(character), Is.False);
				Assert.That(manager.HasComponent<MmdRootMotion>(character), Is.False);
				Assert.That(manager.GetComponentData<LocalTransform>(character).Position, Is.EqualTo(float3.zero));
			}
		}

		[Test]
		public void WorldInputReachesControlledCharacterBeforeSearchAndPlayback() {
			using (var world = new MmdSampleWorld(0.1f)) {
				Entity controlled = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				Entity other = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(controlled);
				manager.AddComponentData(controlled, new MmdSampleCharacterControl { WalkSpeed = 1f, RunSpeed = 2f, TurnSpeed = 1f, WalkTags = 2, RunTags = 2 });
				world.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f) });
				world.Tick();

				Assert.That(manager.GetComponentData<MmdPlayback>(controlled).ClipIndex, Is.EqualTo(1));
				Assert.That(manager.GetComponentData<LocalTransform>(controlled).Position.z, Is.EqualTo(0.1f).Within(0.00001f));
				Assert.That(manager.GetComponentData<MmdPlayback>(other).ClipIndex, Is.Zero);
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(other)[14].Value, Is.Zero);
				world.SetPaused(true);
				world.SetInput(new MmdSampleInput { Movement = new float2(1f, 0f), Run = true });
				world.Tick();
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(controlled)[14].Value, Is.Zero);
				Assert.That(manager.GetComponentData<MmdPlayback>(controlled).Time, Is.EqualTo(0.1f).Within(0.00001f));
				world.SetPaused(false);
				world.Tick();
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(controlled)[12].Value, Is.Zero.Within(0.00001f));
				Assert.That(manager.GetBuffer<MmdSearchQueryElement>(controlled)[14].Value, Is.EqualTo(1f).Within(0.00001f));
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void StartIntentUsesConfiguredGaitIntersectionThenExcludesStartsOnceAligned(bool run) {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var actor = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(actor);
				manager.AddComponentData(actor, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(actor, new MmdSampleCharacterControl {
					WalkSpeed = 2f, RunSpeed = 5f, TurnSpeed = 4.5f, IdleTags = 1, WalkTags = 2, RunTags = 4, TurnTags = 8, StartTags = 64
				});
				manager.AddComponentData(actor, new MmdSearchSettings { RequiredTags = 1, ExcludedTags = 32, MinimumTransitionTime = 0.2f });
				manager.AddComponentData(actor, new MmdPlayback { Speed = 1f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, -1f), Run = run });
				sample.Tick();
				uint gait = run ? 4u : 2u;
				var settings = manager.GetComponentData<MmdSearchSettings>(actor);
				Assert.That(settings.RequiredTags, Is.EqualTo(gait | 64u));
				Assert.That(settings.ExcludedTags, Is.EqualTo(32u));
				manager.SetComponentData(actor, LocalTransform.FromRotation(quaternion.RotateY(math.PI)));
				manager.SetComponentData(actor, new MmdSampleTrajectoryState { Velocity = new float3(0f, 0f, run ? -5f : -2f) });
				manager.SetComponentData(actor, new MmdPlayback { Speed = 1f, TransitionElapsed = 0.3f });
				sample.Tick();
				settings = manager.GetComponentData<MmdSearchSettings>(actor);
				Assert.That(settings.RequiredTags, Is.EqualTo(gait));
				Assert.That(settings.ExcludedTags, Is.EqualTo(32u | 64u));
				sample.SetInput(default);
				sample.Tick();
				settings = manager.GetComponentData<MmdSearchSettings>(actor);
				Assert.That(settings.RequiredTags, Is.EqualTo(1u));
				Assert.That(settings.ExcludedTags, Is.EqualTo(32u), "Idle releases only the Sample-owned start exclusion.");
			}
		}

		[Test]
		public void BackwardStartPredictsStepBeforeCompletingFacingWithoutChangingEntity() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var manager = sample.World.EntityManager;
				var actor = sample.Characters.Create(default, true, LocalTransform.Identity);
				manager.AddComponent<MmdSampleMovement>(actor);
				manager.AddComponentData(actor, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(actor, new MmdSampleCharacterControl { WalkSpeed = 2f, TurnSpeed = 4.5f });
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, -1f) });
				sample.Tick();
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				Assert.That(query[1].Value, Is.LessThan(-0.1f), "A character at rest can step toward a backward request.");
				Assert.That(query[3].Value, Is.GreaterThan(0f), "Early facing still turns progressively.");
				Assert.That(query[11].Value, Is.LessThan(0f));
				Assert.That(manager.GetComponentData<LocalTransform>(actor).Rotation, Is.EqualTo(quaternion.identity));
			}
		}

		[Test]
		public void ReversalBrakesAndCurvesTowardMovementInsteadOfPredictingBackwardRun() {
			using (var world = new MmdSampleWorld(1f / 60f)) {
				Entity character = world.Characters.Create(default, true, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponentData(character, new MmdSampleCharacterControl { RunSpeed = 5.4f, TurnSpeed = 4.5f });
				manager.SetComponentData(character, new MmdSampleTrajectoryState { Velocity = new float3(0f, 0f, 5.4f) });
				world.SetInput(new MmdSampleInput { Movement = new float2(0f, -1f), Facing = new float2(0f, 1f), Run = true });
				world.Tick();
				var query = manager.GetBuffer<MmdSearchQueryElement>(character);
				Assert.That(query[14].Value, Is.EqualTo(5.4f));
				Assert.That(query[1].Value, Is.GreaterThan(0f), "A moving character first brakes in its current direction.");
				Assert.That(query[11].Value, Is.LessThan(0f), "The future heading must turn toward reverse movement, regardless of camera facing.");
				Assert.That(math.length(manager.GetComponentData<MmdSampleTrajectoryState>(character).Velocity), Is.LessThan(5.4f));
				Assert.That(manager.GetComponentData<LocalTransform>(character).Rotation, Is.EqualTo(quaternion.identity));
			}
		}

		[Test]
		public void QuerySpaceKeepsDebugHeadingAfterRootMotionChangesCharacterTransform() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var spawn = LocalTransform.FromPositionRotation(new float3(2f, 0f, 3f), quaternion.RotateY(math.PI * 0.5f));
				Entity character = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, spawn);
				var manager = world.World.EntityManager;
				manager.AddComponent<MmdSampleMovement>(character);
				manager.AddComponentData(character, new MmdSampleCharacterControl { WalkSpeed = 1f, TurnSpeed = 2f, WalkTags = 2 });
				world.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f) });
				world.Tick();
				var space = manager.GetComponentData<MmdSearchQuerySpace>(character);
				Assert.That(space.Position, Is.EqualTo(spawn.Position));
				Assert.That(space.Rotation, Is.EqualTo(spawn.Rotation));
				Assert.That(manager.GetComponentData<LocalTransform>(character).Rotation, Is.Not.EqualTo(space.Rotation));
				var query = manager.GetBuffer<MmdSearchQueryElement>(character);
				var basis = LocalTransform.FromPositionRotation(space.Position, space.Rotation);
				Assert.That(MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsNativeArray().AsReadOnly(), basis, 0, 0f, out var point), Is.True);
				Assert.That(point.Forward.x, Is.EqualTo(math.cos(0.4f)).Within(0.00001f));
				Assert.That(point.Forward.z, Is.EqualTo(math.sin(0.4f)).Within(0.00001f));
			}
		}
	}
}
