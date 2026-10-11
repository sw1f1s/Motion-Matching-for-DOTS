using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Components;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ColInt.MMD.Tests {
	public class MmdSampleMovementTests {
		private const float Timestep = 1f / 60f;
		private const string ConfigurationPath = "Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset";
		private MmdSampleCharacterConfiguration _configuration;
		private MmdSampleWorld _sample;
		private Entity _character;

		[SetUp]
		public void SetUp() {
			_configuration = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(ConfigurationPath));
			_configuration.Assets.RenderPrefab = default;
			_sample = new MmdSampleWorld(Timestep);
			AddBox(new float3(0f, -0.5f, 0f), new float3(120f, 1f, 120f));
		}

		[TearDown]
		public void TearDown() {
			_sample.Dispose();
			Object.DestroyImmediate(_configuration);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void SpaceProducesOneJumpThenFallLandingAndLocomotion(int gait) {
			CreateCharacter(float3.zero);
			_sample.SetInput(new MmdSampleInput { Movement = gait > 0 ? new float2(0f, 1f) : float2.zero, Run = gait == 2 });
			Advance(90);
			_sample.SetInput(new MmdSampleInput { Movement = gait > 0 ? new float2(0f, 1f) : float2.zero, Run = gait == 2, JumpPressed = true });
			bool jump = false;
			bool fall = false;
			bool landing = false;
			float highest = 0f;
			uint jumpRevision = 0;
			for (int tick = 0; tick < 230; tick++) {
				_sample.Tick();
				AssertLandingPose();
				var manager = _sample.World.EntityManager;
				if (manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character)) {
					jump = true;
					uint revision = manager.GetComponentData<MmdActionRequest>(_character).Revision;
					if (jumpRevision == 0) {
						jumpRevision = revision;
						var data = manager.GetComponentData<MmdDatabase>(_character).Value;
						string group = gait == 0 ? _configuration.Jump.Groups.Stand : gait == 1 ? _configuration.Jump.Groups.Walk : _configuration.Jump.Groups.Run;
						int action = MotionDatabaseGroups.GetActionId(ref data.Value, new FixedString64Bytes(group));
						Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.EqualTo(action));
						Assert.That(manager.GetComponentData<MmdSampleJumpProgress>(_character).TakeoffTime, Is.GreaterThan(0.15f));
					}
					Assert.That(revision, Is.EqualTo(jumpRevision), "An unchanged request must not restart the takeoff clip.");
				}
				fall |= manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character);
				landing |= manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character);
				highest = math.max(highest, manager.GetComponentData<LocalTransform>(_character).Position.y);
				Assert.That(manager.GetComponentData<MmdActionState>(_character).Rejected, Is.False);
				Assert.That(manager.GetComponentData<MmdFault>(_character).Code, Is.EqualTo(ColInt.MMD.Lifecycle.Data.MmdFaultCode.None));
			}
			var finalManager = _sample.World.EntityManager;
			Assert.That(jump && fall && landing, Is.True);
			Assert.That(highest, Is.InRange(1f, 1.4f));
			Assert.That(finalManager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(finalManager.GetComponentData<LocalTransform>(_character).Position.y, Is.InRange(-0.03f, 0.08f));
			Assert.That(finalManager.GetComponentData<MmdSampleGroundState>(_character).LandingStrength, Is.LessThan(_configuration.Landing.HeavyHeight));
		}

		[TestCase(1f, false)]
		[TestCase(6f, true)]
		public void FallingUsesImpactSpeedToSelectLightOrHeavyLanding(float height, bool heavy) {
			CreateCharacter(new float3(0f, height, 0f));
			bool fall = false;
			bool landed = false;
			for (int tick = 0; tick < 300; tick++) {
				_sample.Tick();
				AssertLandingPose();
				var manager = _sample.World.EntityManager;
				fall |= manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character);
				if (!landed && manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character)) {
					landed = true;
					var database = manager.GetComponentData<MmdDatabase>(_character).Value;
					int expected = MotionDatabaseGroups.GetActionId(ref database.Value,
						new FixedString64Bytes(heavy ? _configuration.Landing.HeavyGroups.Stand : _configuration.Landing.LightGroups.Stand));
					Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.EqualTo(expected));
					float strength = manager.GetComponentData<MmdSampleGroundState>(_character).LandingStrength;
					Assert.That(strength >= _configuration.Landing.HeavyHeight, Is.EqualTo(heavy));
				}
			}
			Assert.That(fall && landed, Is.True);
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
		}

		[Test]
		public void RunningOffPlatformFallsAndResumesLocomotionOnLowerFloor() {
			AddBox(new float3(0f, 1.75f, 0f), new float3(4f, 3.5f, 4f));
			CreateCharacter(new float3(0f, 3.52f, 0f));
			Advance(45);
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			bool fall = false;
			bool landing = false;
			for (int tick = 0; tick < 230; tick++) {
				_sample.Tick();
				AssertLandingPose();
				fall |= _sample.World.EntityManager.HasComponent<MmdSampleState<MmdSampleFall>>(_character);
				landing |= _sample.World.EntityManager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character);
			}
			Assert.That(fall && landing, Is.True);
			Assert.That(_sample.World.EntityManager.GetComponentData<LocalTransform>(_character).Position.y, Is.LessThan(0.08f));
		}

		[TestCase(0.2f, false)]
		[TestCase(0.2f, true)]
		[TestCase(0.25f, false)]
		[TestCase(0.25f, true)]
		public void DescendingStairsKeepsLocomotionWithoutFallOrLanding(float riser, bool run) {
			for (int step = 0; step < 8; step++) {
				float height = (8 - step) * riser;
				AddBox(new float3(0f, height * 0.5f, step * 0.65f), new float3(3f, height, 0.65f));
			}
			CreateCharacter(new float3(0f, 8f * riser + 0.02f, -0.15f));
			Advance(30);
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = run });
			var manager = _sample.World.EntityManager;
			for (int tick = 0; tick < 400 && manager.GetComponentData<LocalTransform>(_character).Position.z < 5.5f; tick++) {
				_sample.Tick();
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.False, "A walkable descending step must not start Fall.");
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character), Is.False, "A stair descent must not play a landing action.");
			}
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.z, Is.GreaterThan(5.4f));
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.LessThan(0.08f));
		}

		[Test]
		public void PauseDisabledAndSameStateRequestPreserveFlightWithoutSecondImpulse() {
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.SetInput(new MmdSampleInput { JumpPressed = true });
			Advance(10);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.GreaterThan(0f));
			float elapsed = manager.GetComponentData<MmdSampleState<MmdSampleJump>>(_character).Elapsed;
			float3 position = manager.GetComponentData<LocalTransform>(_character).Position;
			uint revision = manager.GetComponentData<MmdSampleStateMachine>(_character).Revision;
			_sample.SetPaused(true);
			Advance(30);
			Assert.That(manager.GetComponentData<MmdSampleState<MmdSampleJump>>(_character).Elapsed, Is.EqualTo(elapsed));
			Assert.That(math.distance(position, manager.GetComponentData<LocalTransform>(_character).Position), Is.Zero);
			_sample.SetPaused(false);
			manager.AddComponent<Disabled>(_character);
			Advance(10);
			Assert.That(manager.GetComponentData<MmdSampleState<MmdSampleJump>>(_character).Elapsed, Is.EqualTo(elapsed));
			manager.RemoveComponent<Disabled>(_character);
			var transition = new MmdSampleStateTransition();
			transition.Request<MmdSampleJump>(MmdSampleStatePriority.Jump);
			manager.SetComponentData(_character, transition);
			_sample.Tick();
			Assert.That(manager.GetComponentData<MmdSampleStateMachine>(_character).Revision, Is.EqualTo(revision));
			Assert.That(manager.GetComponentData<MmdSampleState<MmdSampleJump>>(_character).Elapsed, Is.EqualTo(elapsed + Timestep).Within(0.0001f));
			Assert.That(manager.HasComponent<MmdSampleJumpImpulse>(_character), Is.False);
		}

		[Test]
		public void TransitionPriorityPreservesFirstEqualRequestAndLandingWinsCompletion() {
			var transition = new MmdSampleStateTransition();
			transition.Request<MmdSampleJump>(20);
			transition.Request<MmdSampleFall>(20);
			Assert.That(transition.Target.Component, Is.EqualTo(MmdSampleStateDescriptor.For<MmdSampleJump>().Component));
			transition.Request<MmdSampleLanding>(40);
			transition.Request<MmdSampleLocomotion>(10);
			Assert.That(transition.Target.Component, Is.EqualTo(MmdSampleStateDescriptor.For<MmdSampleLanding>().Component));
		}

		[Test]
		public void SpaceAppliesJumpImpulseOnItsFirstTick() {
			CreateCharacter(float3.zero);
			Advance(20);
			_sample.SetInput(new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.GreaterThan(3f));
			Assert.That(manager.GetComponentData<MmdPlayback>(_character).Time,
				Is.GreaterThanOrEqualTo(manager.GetComponentData<MmdSampleJumpProgress>(_character).TakeoffTime));
		}

		[Test]
		public void SpaceImmediatelyAfterLosingSupportStillLaunchesJump() {
			CreateCharacter(float3.zero);
			Advance(20);
			var manager = _sample.World.EntityManager;
			manager.SetComponentData(_character, LocalTransform.FromPosition(new float3(0f, 0.2f, 0f)));
			manager.SetComponentData(_character, new PhysicsVelocity { Linear = new float3(0f, -0.8f, 0f) });
			_sample.SetInput(new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.GreaterThan(3f));
		}

		[TestCase(1f)]
		[TestCase(6f)]
		public void LandingCanBeInterruptedByAnotherJumpWithoutWaitingForTheClip(float height) {
			CreateCharacter(new float3(0f, height, 0f));
			var manager = _sample.World.EntityManager;
			for (int tick = 0; tick < 180 && !manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character); tick++) {
				_sample.Tick();
			}
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character), Is.True);
			_sample.SetInput(new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.GreaterThan(3f));
		}

		[Test]
		public void JumpPressedJustBeforeGroundContactIsBufferedUntilSupportArrives() {
			CreateCharacter(new float3(0f, 0.8f, 0f));
			var manager = _sample.World.EntityManager;
			for (int tick = 0; tick < 120 && manager.GetComponentData<LocalTransform>(_character).Position.y > 0.16f; tick++) {
				_sample.Tick();
			}
			Assert.That(manager.GetComponentData<MmdSampleGroundState>(_character).IsGrounded, Is.False);
			_sample.SetInput(new MmdSampleInput { JumpPressed = true });
			for (int tick = 0; tick < 10 && !manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character); tick++) {
				_sample.Tick();
			}
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.GreaterThan(3f));
		}

		private void AssertLandingPose() {
			var manager = _sample.World.EntityManager;
			if (!manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character)) {
				return;
			}
			var database = manager.GetComponentData<MmdDatabase>(_character).Value;
			var poses = manager.GetBuffer<MmdLocalPose>(_character);
			var models = new float4x4[poses.Length];
			for (int bone = 0; bone < poses.Length; bone++) {
				var pose = poses[bone].Value;
				var local = float4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
				int parent = database.Value.Parents[bone];
				models[bone] = parent < 0 ? local : math.mul(models[parent], local);
			}
			float feet = math.min(models[database.Value.LeftFootBone].c3.y, models[database.Value.RightFootBone].c3.y);
			Assert.That(feet, Is.InRange(-0.3f, 0.8f), "The landing pose must remain near the grounded entity instead of replaying the source fall height.");
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.LessThan(0.5f), "Landing must not introduce another upward impulse.");
		}

		private void CreateCharacter(float3 position) {
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromPosition(position), true, false);
		}

		private void Advance(int ticks) {
			for (int tick = 0; tick < ticks; tick++) {
				_sample.Tick();
			}
		}

		private void AddBox(float3 position, float3 size) {
			var manager = _sample.World.EntityManager;
			var collider = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, size, 0f);
			Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
			manager.SetComponentData(entity, LocalTransform.FromPosition(position));
			manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
			manager.AddSharedComponent(entity, new PhysicsWorldIndex());
			manager.AddComponentData(entity, new MmdSamplePhysicsColliderOwner(collider));
		}
	}
}
