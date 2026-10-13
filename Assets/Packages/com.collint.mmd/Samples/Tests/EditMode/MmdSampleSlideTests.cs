using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using ColInt.MMD.Samples.Character.Movement.SlideState.Helpers;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Components;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ColInt.MMD.Tests {
	public class MmdSampleSlideTests {
		private const float Timestep = 1f / 60f;
		private MmdSampleCharacterConfiguration _configuration;
		private MmdSampleWorld _sample;
		private Entity _character;

		[SetUp]
		public void SetUp() {
			_configuration = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>("Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset"));
			_configuration.Assets.RenderPrefab = default;
			_sample = new MmdSampleWorld(Timestep);
		}

		[TearDown]
		public void TearDown() {
			_sample.Dispose();
			Object.DestroyImmediate(_configuration);
		}

		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		public void EachGaitUsesItsOwnAnimationGroup(bool run, bool sprint) {
			CreateFlatCharacter();
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = run, Sprint = sprint });
			Advance(180);
			var manager = _sample.World.EntityManager;
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var playback = manager.GetComponentData<MmdPlayback>(_character);
			int group = sprint ? control.SprintGroup : run ? control.RunGroup : control.WalkGroup;
			Assert.That(group, Is.Not.Zero);
			Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[playback.ClipIndex], group), Is.True);
			AssertHealthy();
		}

		[Test]
		public void SprintCrouchBrakesWithoutInputAndPlaysExitIntoCrouch() {
			CreateFlatCharacter();
			StartSlide();
			_sample.SetInput(new MmdSampleInput { Crouch = true, Facing = new float2(-1f, 0f) });
			var manager = _sample.World.EntityManager;
			float previous = math.length(manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity);
			bool sawLoop = false;
			bool sawExit = false;
			for (int tick = 0; tick < 360; tick++) {
				_sample.Tick();
				float speed = math.length(manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity);
				Assert.That(speed, Is.LessThanOrEqualTo(previous + 0.01f));
				previous = speed;
				var data = manager.GetComponentData<MmdDatabase>(_character).Value;
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				sawLoop |= manager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character) && data.Value.Clips[playback.ClipIndex].Loop;
				sawExit |= manager.HasComponent<MmdSampleState<MmdSampleSlideExit>>(_character);
				AssertHealthy();
			}
			Assert.That(sawLoop, Is.True);
			Assert.That(sawExit, Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
			Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz), Is.LessThan(0.1f));
		}

		[Test]
		public void SlideStopsAtAWallAndCanReturnToStanding() {
			CreateFlatCharacter();
			StartSlide();
			var manager = _sample.World.EntityManager;
			float wall = manager.GetComponentData<LocalTransform>(_character).Position.z + 2f;
			AddBox(new float3(0f, 1.5f, wall), new float3(10f, 3f, 1f), quaternion.identity);
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(300);
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.z, Is.LessThan(wall - 0.45f));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
			_sample.SetInput(new MmdSampleInput());
			Advance(180);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			Assert.That(control.SprintSpeed, Is.EqualTo(_configuration.Locomotion.SprintSpeed));
			Assert.That(control.WalkSpeed, Is.EqualTo(_configuration.Locomotion.WalkSpeed));
			Assert.That(manager.GetComponentData<MmdSampleMovement>(_character).CrouchCollider, Is.False);
			AssertHealthy();
		}

		[Test]
		public void LeavingTheEdgeInterruptsSlideAndRestoresStandingMovement() {
			AddBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 40f), quaternion.identity);
			_character = _sample.Characters.Create(_configuration, LocalTransform.Identity, true, false);
			Advance(30);
			StartSlide();
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(120);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character), Is.False);
			Assert.That(manager.GetComponentData<MmdSampleMovement>(_character).CrouchCollider, Is.False);
			Assert.That(manager.GetComponentData<MmdSampleCharacterControl>(_character).SprintSpeed, Is.EqualTo(_configuration.Locomotion.SprintSpeed));
			AssertHealthy();
		}

		[TestCase(false, true, true)]
		[TestCase(true, false, true)]
		[TestCase(true, true, false)]
		public void SlideRequiresSprintSpeedAndANewCrouchPress(bool sprint, bool moving, bool pressed) {
			CreateFlatCharacter();
			_sample.SetInput(new MmdSampleInput { Movement = moving ? new float2(0f, 1f) : float2.zero, Run = true, Sprint = sprint });
			Advance(150);
			_sample.SetInput(new MmdSampleInput { Movement = moving ? new float2(0f, 1f) : float2.zero, Run = true, Sprint = sprint, Crouch = true, CrouchPressed = pressed });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character), Is.False);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouchEnter>>(_character), Is.True);
		}

		[Test]
		public void DownhillSlideKeepsItsSpeedWithNoMovementInput() {
			float angle = math.radians(12f);
			AddBox(-new float3(0f, math.cos(angle), math.sin(angle)) * 0.5f, new float3(120f, 1f, 120f), quaternion.RotateX(angle));
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromPosition(new float3(0f, 10f * math.tan(angle), -10f)), true, false);
			Advance(30);
			StartSlide();
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			var manager = _sample.World.EntityManager;
			float initial = math.length(manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity);
			Advance(180);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character), Is.True);
			Assert.That(math.length(manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity), Is.GreaterThanOrEqualTo(initial * 0.95f));
			Assert.That(manager.GetComponentData<MmdSampleGroundState>(_character).IsGrounded, Is.True);
			AssertHealthy();
		}

		[TestCase(0f)]
		[TestCase(90f)]
		[TestCase(-45f)]
		public void DownhillSlideFollowsSlopeHeading(float yawDegrees) {
			float angle = math.radians(20f);
			float yaw = math.radians(yawDegrees);
			quaternion slope = math.mul(quaternion.RotateY(yaw), quaternion.RotateX(angle));
			float3 normal = math.rotate(slope, math.up());
			float3 downhill = math.rotate(slope, math.forward());
			AddBox(-normal * 0.5f, new float3(120f, 1f, 120f), slope);
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromPositionRotation(-downhill * 20f, quaternion.RotateY(yaw)), true, false);
			Advance(30);
			var manager = _sample.World.EntityManager;
			float2 direction = math.normalizesafe(downhill.xz);
			_sample.SetInput(new MmdSampleInput { Movement = direction, Sprint = true });
			Advance(150);
			_sample.SetInput(new MmdSampleInput { Movement = direction, Sprint = true, Crouch = true, CrouchPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character), Is.True);
			_sample.SetInput(new MmdSampleInput { Crouch = true, Facing = new float2(direction.y, -direction.x) });
			for (int tick = 0; tick < 180; tick++) {
				_sample.Tick();
				var transform = manager.GetComponentData<LocalTransform>(_character);
				float3 velocity = manager.GetComponentData<PhysicsVelocity>(_character).Linear;
				float3 forward = math.rotate(transform.Rotation, math.forward());
				Assert.That(math.dot(math.normalizesafe(forward.xz), math.normalizesafe(velocity.xz)), Is.GreaterThan(0.95f), "The ECS heading must follow the actual slide velocity.");
				if (tick >= 60) {
					Assert.That(math.dot(math.normalizesafe(velocity.xz), direction), Is.GreaterThan(0.98f), "A downhill slide must not drift toward the contour of the slope.");
				}
				AssertHealthy();
			}
		}

		[Test]
		public void PauseAndDisabledFreezeSlideWithoutLosingMomentum() {
			CreateFlatCharacter();
			StartSlide();
			var manager = _sample.World.EntityManager;
			var progress = manager.GetComponentData<MmdSampleSlideProgress>(_character);
			var current = manager.GetComponentData<MmdSampleState<MmdSampleSlide>>(_character);
			_sample.SetPaused(true);
			Advance(30);
			Assert.That(manager.GetComponentData<MmdSampleSlideProgress>(_character), Is.EqualTo(progress));
			_sample.SetPaused(false);
			manager.AddComponent<Disabled>(_character);
			Advance(30);
			Assert.That(manager.GetComponentData<MmdSampleSlideProgress>(_character), Is.EqualTo(progress));
			Assert.That(manager.GetComponentData<MmdSampleState<MmdSampleSlide>>(_character), Is.EqualTo(current));
			manager.RemoveComponent<Disabled>(_character);
			Advance(30);
			Assert.That(math.length(manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity), Is.LessThan(math.length(progress.Velocity)));
		}

		[TestCase(-1f)]
		[TestCase(1f)]
		public void SteeringPreservesSlopeSpeedAndLimitsTheTurn(float side) {
			float3 normal = math.rotate(quaternion.RotateX(math.radians(20f)), math.up());
			float3 initial = math.normalizesafe(MmdSampleSlideMath.FromPlanarVelocity(new float2(0f, 1f), normal)) * 8f;
			float3 target = math.rotate(quaternion.AxisAngle(normal, side * math.radians(40f)), initial);
			float3 next = MmdSampleSlideMath.Steer(initial, normal, math.normalizesafe(target.xz), 2f, Timestep);
			Assert.That(math.length(next), Is.EqualTo(8f).Within(0.00001f));
			Assert.That(math.abs(math.dot(next, normal)), Is.LessThan(0.00001f));
			float angle = math.atan2(math.dot(normal, math.cross(initial, next)), math.dot(initial, next));
			Assert.That(angle, Is.EqualTo(side * 2f * Timestep).Within(0.00001f));
			Assert.That(MmdSampleSlideMath.Steer(initial, normal, float2.zero, 2f, Timestep), Is.EqualTo(initial));
		}

		[TestCase(-1f, 0f, false)]
		[TestCase(1f, 90f, false)]
		[TestCase(1f, -45f, true)]
		public void DownhillSlideCanBeSteeredAndItsForecastFollowsTheSameInput(float side, float yawDegrees, bool characterInput) {
			float yaw = math.radians(yawDegrees);
			quaternion slope = math.mul(quaternion.RotateY(yaw), quaternion.RotateX(math.radians(20f)));
			float3 normal = math.rotate(slope, math.up());
			float3 downhill = math.rotate(slope, math.forward());
			float2 direction = math.normalizesafe(downhill.xz);
			AddBox(-normal * 0.5f, new float3(120f, 1f, 120f), slope);
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromPositionRotation(-downhill * 20f, quaternion.RotateY(yaw)), true, false);
			Advance(30);
			_sample.SetInput(new MmdSampleInput { Movement = direction, Sprint = true });
			Advance(150);
			_sample.SetInput(new MmdSampleInput { Movement = direction, Sprint = true, Crouch = true, CrouchPressed = true });
			_sample.Tick();
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(10);
			var manager = _sample.World.EntityManager;
			float speed = math.length(manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity);
			float2 desired = math.normalizesafe(math.rotate(quaternion.AxisAngle(normal, side * math.radians(35f)), downhill).xz);
			var input = new MmdSampleInput { Crouch = true, Movement = desired, Facing = -desired };
			if (characterInput) {
				manager.AddComponentData(_character, new MmdSampleCharacterInputOverride { Value = input });
				_sample.SetInput(new MmdSampleInput { Crouch = true, Movement = -desired });
			} else {
				_sample.SetInput(input);
			}
			Advance(30);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character), Is.True);
			float3 velocity = manager.GetComponentData<MmdSampleSlideProgress>(_character).Velocity;
			Assert.That(math.dot(math.normalizesafe(velocity.xz), desired), Is.GreaterThan(0.98f));
			Assert.That(math.length(velocity), Is.GreaterThanOrEqualTo(speed * 0.95f), "Steering must preserve sliding momentum.");
			float turn = math.atan2(direction.y * velocity.x - direction.x * velocity.z, math.dot(direction, velocity.xz));
			Assert.That(side * turn, Is.GreaterThan(math.radians(20f)));
			var points = manager.GetBuffer<MmdTrajectoryPoint>(_character);
			Assert.That(math.dot(math.normalizesafe(points[0].Forward.xz), desired), Is.GreaterThan(0.95f));
			float3 forward = math.rotate(manager.GetComponentData<LocalTransform>(_character).Rotation, math.forward());
			Assert.That(math.dot(math.normalizesafe(forward.xz), math.normalizesafe(velocity.xz)), Is.GreaterThan(0.95f));
			AssertHealthy();
		}

		[TestCase("FootOut")]
		[TestCase("KneesOut")]
		public void SlideExitMatchesTheCurrentVariant(string variant) {
			CreateFlatCharacter();
			StartSlide();
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var slide = manager.GetComponentData<MmdSampleSlide>(_character);
			var catalogs = new ColInt.MMD.Editor.Diagnostics.MmdDebugCatalogStore();
			catalogs.Reload();
			var catalog = catalogs.Resolve(data);
			int exit = -1;
			for (int clip = 0; clip < data.Value.Clips.Length; clip++) {
				if (catalog.Names[clip] == $"M_Neutral_Slide_{variant}_Out_Idle_Crouch") {
					exit = clip;
				}
			}
			Assert.That(exit, Is.GreaterThanOrEqualTo(0));
			Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[exit], slide.MotionGroup), Is.True);
			var query = manager.GetBuffer<MmdSearchQueryElement>(_character);
			for (int feature = 15; feature < query.Length; feature++) {
				query[feature] = new MmdSearchQueryElement { Value = data.Value.Features[data.Value.Clips[exit].FirstFrame * query.Length + feature] };
			}
			manager.SetComponentData(_character, new MmdSampleSlideProgress());
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlideExit>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdPlayback>(_character).ClipIndex, Is.EqualTo(exit));
			AssertHealthy();
		}

		[TestCase(12f, 0f)]
		[TestCase(20f, 90f)]
		[TestCase(40f, -45f)]
		public void GroundFeedbackPreservesPlanarMomentumWithoutTurningAcrossTheSlope(float slopeDegrees, float yawDegrees) {
			quaternion rotation = math.mul(quaternion.RotateY(math.radians(yawDegrees)), quaternion.RotateX(math.radians(slopeDegrees)));
			float3 normal = math.rotate(rotation, math.up());
			float2 initial = math.rotate(quaternion.RotateY(math.radians(yawDegrees + 25f)), math.forward()).xz * 8f;
			float2 planar = initial;
			var settings = new MmdSampleSlide { MaximumSpeed = 20f, MinimumDownhillNormalY = math.cos(math.radians(5f)) };
			for (int tick = 0; tick < 180; tick++) {
				float3 tangent = MmdSampleSlideMath.FromPlanarVelocity(planar, normal);
				Assert.That(math.abs(math.dot(tangent, normal)), Is.LessThan(0.00001f));
				planar = MmdSampleSlideMath.Advance(tangent, normal, settings, 0f, Timestep).xz;
			}
			Assert.That(math.distance(planar, initial), Is.LessThan(0.001f), "Reading grounded velocity must not introduce a sideways force.");
		}

		[TestCase(0f)]
		[TestCase(12f)]
		public void SlideSpeedMatchesBrakingAndDownhillGravity(float slope) {
			var settings = new MmdSampleSlide { Deceleration = 5f, MaximumSpeed = 14f, MinimumDownhillNormalY = math.cos(math.radians(5f)) };
			float3 normal = new float3(0f, math.cos(math.radians(slope)), math.sin(math.radians(slope)));
			float3 velocity = math.normalizesafe(new float3(0f, -math.sin(math.radians(slope)), math.cos(math.radians(slope)))) * 8f;
			for (int tick = 0; tick < 60; tick++) {
				velocity = MmdSampleSlideMath.Advance(velocity, normal, settings, 9.81f, Timestep);
			}
			Assert.That(math.length(velocity), slope == 0f ? Is.EqualTo(3f).Within(0.01f) : Is.GreaterThan(8f));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void MovementInterruptsSlideExitOnTheNextTick(bool sprint) {
			CreateFlatCharacter();
			StartSlide();
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			var manager = _sample.World.EntityManager;
			int ticks = 0;
			while (!manager.HasComponent<MmdSampleState<MmdSampleSlideExit>>(_character) && ticks < 300) {
				_sample.Tick();
				ticks++;
			}
			Assert.That(ticks, Is.LessThan(300));
			_sample.SetInput(new MmdSampleInput { Crouch = !sprint, Sprint = sprint, Movement = new float2(0f, 1f) });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleSlideExit>>(_character), Is.False);
			if (sprint) {
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouchExit>>(_character), Is.True);
				_sample.Tick();
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			} else {
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
				var data = manager.GetComponentData<MmdDatabase>(_character).Value;
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				var crouch = manager.GetComponentData<MmdSampleCrouch>(_character);
				Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[playback.ClipIndex], crouch.MoveGroup), Is.True);
			}
			AssertHealthy();
		}

		[Test]
		public void SlideEntryStartsTheLoweringPoseOnTheCrouchPress() {
			CreateFlatCharacter();
			StartSlide();
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var action = manager.GetComponentData<MmdActionState>(_character);
			var slide = manager.GetComponentData<MmdSampleSlide>(_character);
			var playback = manager.GetComponentData<MmdPlayback>(_character);
			Assert.That(action.ActionId, Is.EqualTo(slide.EnterAction));
			Assert.That(data.Value.Clips[playback.ClipIndex].ActionId, Is.EqualTo(slide.EnterAction));
			Assert.That(manager.GetComponentData<MmdInertialization>(_character).PoseDuration, Is.LessThanOrEqualTo(0.1f));
			float start = ColInt.MMD.Playback.Helpers.MmdAnimationMath.PoseAt(ref data.Value, playback.ClipIndex, 0f, 0).Position.y;
			float lowered = ColInt.MMD.Playback.Helpers.MmdAnimationMath.PoseAt(ref data.Value, playback.ClipIndex, 0.2f, 0).Position.y;
			Assert.That(lowered, Is.LessThan(start - 0.1f), "Entry must lower immediately instead of playing the preceding running step.");
			AssertHealthy();
		}

		private void CreateFlatCharacter() {
			AddBox(new float3(0f, -0.5f, 0f), new float3(120f, 1f, 120f), quaternion.identity);
			_character = _sample.Characters.Create(_configuration, LocalTransform.Identity, true, false);
			Advance(30);
		}

		private void StartSlide() {
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Sprint = true });
			Advance(150);
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Sprint = true, Crouch = true, CrouchPressed = true });
			_sample.Tick();
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleState<MmdSampleSlide>>(_character), Is.True);
		}

		private void AddBox(float3 position, float3 size, quaternion rotation) {
			var manager = _sample.World.EntityManager;
			var collider = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, size, 0f);
			var entity = manager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
			manager.SetComponentData(entity, LocalTransform.FromPositionRotation(position, rotation));
			manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
			manager.AddSharedComponent(entity, new PhysicsWorldIndex());
			manager.AddComponentData(entity, new MmdSamplePhysicsColliderOwner(collider));
		}

		private void Advance(int count) {
			for (int tick = 0; tick < count; tick++) {
				_sample.Tick();
			}
		}

		private void AssertHealthy() {
			var manager = _sample.World.EntityManager;
			Assert.That(math.all(math.isfinite(manager.GetComponentData<LocalTransform>(_character).Position)), Is.True);
			Assert.That(math.all(math.isfinite(manager.GetComponentData<PhysicsVelocity>(_character).Linear)), Is.True);
			Assert.That(manager.GetComponentData<MmdFault>(_character).Code, Is.EqualTo(ColInt.MMD.Lifecycle.Data.MmdFaultCode.None));
		}
	}
}
