using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
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
		private static readonly string ConfigurationPath = MmdSampleTestAssets.GetPath("Configurations/SampleCharacterConfiguration.asset");
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

		[Test]
		public void JumpAndCrouchIntentsRemainOnTheirOwnCharacters() {
			_character = _sample.Characters.CreateAutonomous(_configuration, LocalTransform.FromPosition(new float3(-3f, 0f, 0f)));
			Entity other = _sample.Characters.CreateAutonomous(_configuration, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
			Advance(30);
			var manager = _sample.World.EntityManager;
			manager.SetComponentData(_character, new MmdSampleInput { Crouch = true, JumpPressed = true });
			manager.SetComponentData(other, new MmdSampleInput { Crouch = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(other), Is.False);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouchEnter>>(other), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleInput>(_character).Crouch, Is.False);
			Assert.That(manager.GetComponentData<MmdSampleInput>(other).Crouch, Is.True);
			Advance(210);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(other), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleJumpIntent>(other).Remaining, Is.Zero);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void SpaceProducesOneJumpThenFallLandingAndLocomotion(int gait) {
			CreateCharacter(float3.zero);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = gait > 0 ? new float2(0f, 1f) : float2.zero, Run = gait == 2 });
			Advance(90);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = gait > 0 ? new float2(0f, 1f) : float2.zero, Run = gait == 2, JumpPressed = true });
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

		[TestCase(false)]
		[TestCase(true)]
		public void FallingLoopPlaysContinuouslyUntilItsNaturalWrap(bool steerInAir) {
			CreateCharacter(new float3(0f, 100f, 0f));
			var manager = _sample.World.EntityManager;
			bool playingFall = false;
			bool wrapped = false;
			int fallTicks = 0;
			MmdPlayback previous = default;
			for (int tick = 0; tick < 480; tick++) {
				if (steerInAir) {
					_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(tick % 120 < 60 ? -1f : 1f, 1f), Run = true });
				}
				_sample.Tick();
				Assert.That(manager.GetComponentData<MmdFault>(_character).Code, Is.EqualTo(ColInt.MMD.Lifecycle.Data.MmdFaultCode.None));
				if (!manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character)) {
					if (playingFall) {
						break;
					}
					continue;
				}
				var database = manager.GetComponentData<MmdDatabase>(_character).Value;
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				var clip = database.Value.Clips[playback.ClipIndex];
				int fall = manager.GetComponentData<MmdSampleFall>(_character).Group;
				if (!MotionDatabaseGroups.Contains(ref database.Value, clip, fall)) {
					continue;
				}
				Assert.That(clip.Loop, Is.True);
				if (playingFall) {
					float expected = ColInt.MMD.Playback.Helpers.MmdAnimationMath.LocalTime(clip, previous.Time + Timestep * playback.Speed);
					Assert.That(playback.ClipIndex, Is.EqualTo(previous.ClipIndex));
					Assert.That(playback.Revision, Is.EqualTo(previous.Revision), "Falling must not seek back to an earlier pose of the same clip.");
					Assert.That(playback.Time, Is.EqualTo(expected).Within(0.0001f));
					wrapped |= playback.Time < previous.Time;
				}
				playingFall = true;
				previous = playback;
				fallTicks++;
			}
			Assert.That(fallTicks, Is.GreaterThan(210), "Exercise a complete 3.33-second fall loop using the real Sample database.");
			Assert.That(wrapped, Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character), Is.True);
			Advance(120);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdPlayback>(_character).SearchCountdown, Is.LessThan(1f), "Grounded locomotion must resume its normal searches.");
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

		[TestCase(1f, 0f, 0f)]
		[TestCase(6f, 0f, 0.85f)]
		[TestCase(1f, 2f, -1.5f)]
		public void LandingAndIdleBlendKeepFeetAboveTheirActualSupport(float fallHeight, float surface, float yaw) {
			// Landing also works without Traversal, on an elevated support and with a rotated character.
			_configuration.Traversal.Groups.Mantle = string.Empty;
			if (surface > 0f) {
				AddBox(new float3(0f, surface * 0.5f, 0f), new float3(10f, surface, 10f));
			}
			_character = _sample.Characters.Create(_configuration,
				LocalTransform.FromPositionRotation(new float3(0f, surface + fallHeight, 0f), quaternion.RotateY(yaw)), true, false);
			var manager = _sample.World.EntityManager;
			bool landed = false;
			int idleTicks = 0;
			for (int tick = 0; tick < 300; tick++) {
				_sample.Tick();
				landed |= manager.HasComponent<MmdSampleState<MmdSampleLanding>>(_character);
				if (!landed) {
					continue;
				}
				AssertFeetAbove(surface);
				var transform = manager.GetComponentData<LocalTransform>(_character);
				Assert.That(math.length(transform.Position.xz), Is.LessThan(0.01f), "Pose support must not displace the entity horizontally.");
				Assert.That(transform.Position.y, Is.InRange(surface - 0.03f, surface + 0.08f), "Pose support must not lift the physical root.");
				if (manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character) && ++idleTicks >= 45) {
					break;
				}
			}
			Assert.That(landed, Is.True);
			Assert.That(idleTicks, Is.EqualTo(45));
		}

		[Test]
		public void RunningOffPlatformFallsAndResumesLocomotionOnLowerFloor() {
			AddBox(new float3(0f, 1.75f, 0f), new float3(4f, 3.5f, 4f));
			CreateCharacter(new float3(0f, 3.52f, 0f));
			Advance(45);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
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
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = run });
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
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
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
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
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
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
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
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
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
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			for (int tick = 0; tick < 10 && !manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character); tick++) {
				_sample.Tick();
			}
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.GreaterThan(3f));
		}

		[TestCase(0f)]
		[TestCase(0.85f)]
		[TestCase(-1.5f)]
		public void VerticalJumpKeepsItsPlanarPositionDuringFall(float yaw) {
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromRotation(quaternion.RotateY(yaw)), true, false);
			Advance(60);
			var manager = _sample.World.EntityManager;
			float2 start = manager.GetComponentData<LocalTransform>(_character).Position.xz;
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			float2 firstFallPose = float2.zero;
			bool fell = false;
			for (int tick = 0; tick < 100; tick++) {
				_sample.Tick();
				var transform = manager.GetComponentData<LocalTransform>(_character);
				if (manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character)) {
					var smoothing = manager.GetComponentData<MmdInertializationState>(_character);
					float2 pose = manager.GetBuffer<MmdLocalPose>(_character)[0].Value.Position.xz;
					if (!fell) {
						firstFallPose = pose;
					}
					fell = true;
					Assert.That(smoothing.StationaryPoseOffset, Is.EqualTo(float3.zero), "Airborne feet must not anchor a Fall loop as if it were grounded Idle.");
					Assert.That(smoothing.PoseDuration, Is.EqualTo(_configuration.Transitions.PoseDuration));
					Assert.That(math.distance(firstFallPose, pose), Is.LessThan(0.15f), "The Fall transition must not slide the whole skeleton backwards by the outgoing jump's foot offset.");
				}
				Assert.That(math.distance(transform.Position.xz, start), Is.LessThan(0.005f), "A vertical jump must not acquire horizontal movement from the Fall clip.");
			}
			Assert.That(fell, Is.True);
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
			var ground = manager.GetComponentData<MmdSampleGroundState>(_character);
			if (ground.IsGrounded) {
				AssertFeetAbove(ground.SupportHeight - manager.GetComponentData<MmdSampleMovementSettings>(_character).Ground.Skin);
			}
		}

		private void AssertFeetAbove(float surface) {
			var manager = _sample.World.EntityManager;
			var database = manager.GetComponentData<MmdDatabase>(_character).Value;
			var transform = manager.GetComponentData<LocalTransform>(_character);
			var poses = manager.GetBuffer<MmdLocalPose>(_character);
			var models = new float4x4[poses.Length];
			for (int bone = 0; bone < poses.Length; bone++) {
				var pose = poses[bone].Value;
				var local = float4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
				int parent = database.Value.Parents[bone];
				models[bone] = parent < 0 ? local : math.mul(models[parent], local);
				if (bone == database.Value.LeftFootBone || bone == database.Value.RightFootBone ||
					parent == database.Value.LeftFootBone || parent == database.Value.RightFootBone) {
					float height = transform.Position.y + math.rotate(transform.Rotation, models[bone].c3.xyz * transform.Scale).y;
					Assert.That(height, Is.GreaterThanOrEqualTo(surface - 0.005f), "Displayed ankle/toe penetrates the landing support: bone " + bone);
				}
			}
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
