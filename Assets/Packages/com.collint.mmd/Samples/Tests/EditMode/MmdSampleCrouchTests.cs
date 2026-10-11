using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
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
	public class MmdSampleCrouchTests {
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
			_character = _sample.Characters.Create(_configuration, LocalTransform.Identity, true, false);
			Advance(30);
		}

		[TearDown]
		public void TearDown() {
			_sample.Dispose();
			Object.DestroyImmediate(_configuration);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void IdleWalkAndRunEnterCrouchAndRestoreStandingMovement(int gait) {
			var input = new MmdSampleInput { Movement = gait == 0 ? float2.zero : new float2(0f, 1f), Run = gait == 2 };
			_sample.SetInput(input);
			Advance(90);
			var manager = _sample.World.EntityManager;
			var standing = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			var smoothing = manager.GetComponentData<MmdInertialization>(_character);
			input.Crouch = true;
			_sample.SetInput(input);
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouchEnter>>(_character), Is.True);
			var owner = manager.GetComponentData<MmdSamplePhysicsColliderOwner>(_character);
			Assert.That(manager.GetComponentData<PhysicsCollider>(_character).Value, Is.EqualTo(owner.Crouched));
			if (gait == 0) {
				Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.EqualTo(manager.GetComponentData<MmdSampleCrouch>(_character).EnterAction));
			} else {
				Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.Zero, "Moving entry must keep root motion searchable instead of stopping for a stationary transition.");
				var database = manager.GetComponentData<MmdDatabase>(_character).Value;
				int clip = manager.GetComponentData<MmdPlayback>(_character).ClipIndex;
				var crouch = manager.GetComponentData<MmdSampleCrouch>(_character);
				Assert.That(database.Value.Clips[clip].Tags & (crouch.MoveTags | crouch.StartTags), Is.EqualTo(crouch.MoveTags | crouch.StartTags));
			}
			Advance(210);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			Assert.That(control.WalkSpeed, Is.EqualTo(_configuration.Crouch.Speed));
			Assert.That(control.RunSpeed, Is.EqualTo(_configuration.Crouch.Speed));
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.InRange(-0.05f, 0.1f));
			AssertCrouchedPose();
			input.Crouch = false;
			_sample.SetInput(input);
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouchExit>>(_character), Is.True);
			if (gait == 0) {
				Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.EqualTo(manager.GetComponentData<MmdSampleCrouch>(_character).ExitAction));
			} else {
				Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.Zero);
			}
			Advance(210);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleCharacterControl>(_character), Is.EqualTo(standing));
			Assert.That(manager.GetComponentData<MmdInertialization>(_character), Is.EqualTo(smoothing));
			Assert.That(manager.GetComponentData<PhysicsCollider>(_character).Value, Is.EqualTo(owner.Value));
			AssertHealthy();
		}

		[TestCase(false, 6)]
		[TestCase(false, 18)]
		[TestCase(false, 39)]
		[TestCase(false, 90)]
		[TestCase(true, 6)]
		[TestCase(true, 18)]
		[TestCase(true, 39)]
		[TestCase(true, 90)]
		public void ReleasingMovementBrakesCrouchWithoutPlayingTheStopsApproachSteps(bool run, int movingTicks) {
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(210);
			_sample.SetInput(new MmdSampleInput { Crouch = true, Run = run, Movement = new float2(0f, 1f) });
			Advance(movingTicks);
			AssertCrouchStopsAndResumes();
		}

		[TestCase(false, 2)]
		[TestCase(false, 12)]
		[TestCase(true, 2)]
		[TestCase(true, 12)]
		public void ReleasingMovementDuringMovingCrouchEntryStartsBrakingImmediately(bool run, int entryTicks) {
			var input = new MmdSampleInput { Run = run, Movement = new float2(0f, 1f) };
			_sample.SetInput(input);
			Advance(90);
			input.Crouch = true;
			_sample.SetInput(input);
			Advance(entryTicks);
			AssertCrouchStopsAndResumes();
		}

		[TestCase(-90f)]
		[TestCase(90f)]
		[TestCase(-180f)]
		[TestCase(180f)]
		public void CrouchedStartsMoveAndTurnWithoutAnExtraRevolution(float degrees) {
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(210);
			var manager = _sample.World.EntityManager;
			var database = manager.GetComponentData<MmdDatabase>(_character).Value;
			int hips = -1;
			for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
				if (database.Value.BoneNameHashes[bone] == BonePathHash.Calculate("root/pelvis")) {
					hips = bone;
				}
			}
			Assert.That(hips, Is.GreaterThanOrEqualTo(0));
			float3 axis = math.rotate(math.inverse(ModelRotation(ref database.Value, hips)), new float3(0f, 0f, 1f));
			float angle = math.radians(degrees);
			float2 direction = new float2(math.sin(angle), math.cos(angle));
			float side = math.sign(ColInt.MMD.Samples.Character.Movement.Facing.Helpers.MmdSampleFacing.YawError(
				manager.GetComponentData<LocalTransform>(_character).Rotation, direction));
			var initial = manager.GetComponentData<LocalTransform>(_character).Position;
			float2 previousBody = new float2(0f, 1f);
			float turn = 0f;
			bool steppedDuringTurn = false;
			_sample.SetInput(new MmdSampleInput { Crouch = true, Movement = direction });
			for (int tick = 0; tick < 180; tick++) {
				_sample.Tick();
				var transform = manager.GetComponentData<LocalTransform>(_character);
				float3 forward = math.rotate(math.mul(transform.Rotation, ModelRotation(ref database.Value, hips)), axis);
				float2 body = math.normalizesafe(forward.xz);
				turn += side * math.atan2(previousBody.y * body.x - previousBody.x * body.y, math.dot(previousBody, body));
				previousBody = body;
				Assert.That(turn, Is.InRange(-math.radians(20f), math.abs(angle) + math.radians(35f)));
				float error = math.abs(ColInt.MMD.Samples.Character.Movement.Facing.Helpers.MmdSampleFacing.YawError(transform.Rotation, direction));
				steppedDuringTurn |= math.distance(initial.xz, transform.Position.xz) > 0.03f && error > math.radians(20f);
				AssertHealthy();
			}
			Assert.That(steppedDuringTurn, Is.True);
			float2 velocity = manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz;
			Assert.That(math.length(velocity), Is.GreaterThan(_configuration.Crouch.Speed * 0.7f));
			Assert.That(math.dot(math.normalizesafe(velocity), direction), Is.GreaterThan(0.98f));
		}

		[Test]
		public void MovementCanInterruptTheHeldTailOfStationaryEnterAndExit() {
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(30);
			_sample.SetInput(new MmdSampleInput { Crouch = true, Movement = new float2(0f, 1f) });
			Advance(2);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.Zero);
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(120);
			_sample.SetInput(default);
			Advance(30);
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			Advance(2);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.Zero);
			AssertHealthy();
		}

		[Test]
		public void CeilingKeepsCharacterCrouchedUntilStandingCapsuleIsClear() {
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(210);
			AddBox(new float3(0f, 1.6f, 0f), new float3(4f, 0.5f, 4f));
			Advance(2);
			_sample.SetInput(default);
			Advance(90);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
			var owner = manager.GetComponentData<MmdSamplePhysicsColliderOwner>(_character);
			Assert.That(manager.GetComponentData<PhysicsCollider>(_character).Value, Is.EqualTo(owner.Crouched));
			manager.SetComponentData(_character, LocalTransform.FromPosition(new float3(5f, 0f, 0f)));
			Advance(210);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsCollider>(_character).Value, Is.EqualTo(owner.Value));
			AssertHealthy();
		}

		[Test]
		public void PauseAndDisabledEntityPreserveCrouchWithoutRestartingTransition() {
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(210);
			var manager = _sample.World.EntityManager;
			uint revision = manager.GetComponentData<MmdSampleStateMachine>(_character).Revision;
			float elapsed = manager.GetComponentData<MmdSampleState<MmdSampleCrouch>>(_character).Elapsed;
			_sample.SetPaused(true);
			Advance(30);
			Assert.That(manager.GetComponentData<MmdSampleState<MmdSampleCrouch>>(_character).Elapsed, Is.EqualTo(elapsed));
			_sample.SetPaused(false);
			manager.AddComponent<Disabled>(_character);
			Advance(10);
			Assert.That(manager.GetComponentData<MmdSampleState<MmdSampleCrouch>>(_character).Elapsed, Is.EqualTo(elapsed));
			manager.RemoveComponent<Disabled>(_character);
			Advance(60);
			Assert.That(manager.GetComponentData<MmdSampleStateMachine>(_character).Revision, Is.EqualTo(revision));
			AssertHealthy();
		}

		[Test]
		public void FallInterruptsCrouchAndRestoresStandingProfile() {
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			Advance(210);
			var manager = _sample.World.EntityManager;
			manager.SetComponentData(_character, LocalTransform.FromPosition(new float3(0f, 4f, 0f)));
			_sample.SetInput(default);
			bool fallen = false;
			for (int tick = 0; tick < 260; tick++) {
				_sample.Tick();
				if (manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character)) {
					fallen = true;
					Assert.That(manager.GetComponentData<MmdSampleCharacterControl>(_character).WalkSpeed, Is.EqualTo(_configuration.Locomotion.WalkSpeed));
				}
			}
			Assert.That(fallen, Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			AssertHealthy();
		}

		[Test]
		public void RemovedDisabledCharacterReleasesAllCapsuleBlobs() {
			var manager = _sample.World.EntityManager;
			var owner = manager.GetComponentData<MmdSamplePhysicsColliderOwner>(_character);
			manager.AddComponent<Disabled>(_character);
			manager.DestroyEntity(_character);
			_sample.World.GetExistingSystemManaged<InitializationSystemGroup>().Update();
			Assert.That(manager.Exists(_character), Is.False);
			Assert.Throws<System.InvalidOperationException>(() => { var value = owner.Value.Value; });
			Assert.Throws<System.InvalidOperationException>(() => { var value = owner.Crouched.Value; });
			Assert.Throws<System.InvalidOperationException>(() => { var value = owner.Traversal.Value; });
		}

		[Test]
		public void WorldDisposalReleasesAllCapsuleBlobs() {
			var owner = _sample.World.EntityManager.GetComponentData<MmdSamplePhysicsColliderOwner>(_character);
			_sample.Dispose();
			Assert.Throws<System.InvalidOperationException>(() => { var value = owner.Value.Value; });
			Assert.Throws<System.InvalidOperationException>(() => { var value = owner.Crouched.Value; });
			Assert.Throws<System.InvalidOperationException>(() => { var value = owner.Traversal.Value; });
		}

		private void AssertCrouchStopsAndResumes() {
			var manager = _sample.World.EntityManager;
			float3 releasedAt = manager.GetComponentData<LocalTransform>(_character).Position;
			// Early entry from a run still carries standing speed. Bound its travel
			// by the same 10 m/s² braking forecast instead of the crouch-only limit.
			float stoppingDistance = math.max(0.5f, math.lengthsq(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz) / 20f + 0.1f);
			_sample.SetInput(new MmdSampleInput { Crouch = true });
			for (int tick = 0; tick < 120; tick++) {
				_sample.Tick();
				float3 position = manager.GetComponentData<LocalTransform>(_character).Position;
				Assert.That(math.distance(releasedAt.xz, position.xz), Is.LessThan(stoppingDistance), "A stop must not replay the clip's approach steps.");
				if (tick >= 24) {
					Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz), Is.LessThan(0.1f));
				}
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCrouch>>(_character), Is.True);
				AssertHealthy();
			}
			var database = manager.GetComponentData<MmdDatabase>(_character).Value;
			int clip = manager.GetComponentData<MmdPlayback>(_character).ClipIndex;
			Assert.That(database.Value.Clips[clip].Loop, Is.True);
			Assert.That(database.Value.Clips[clip].Tags & manager.GetComponentData<MmdSampleCrouch>(_character).IdleTags, Is.Not.Zero);
			float3 stoppedAt = manager.GetComponentData<LocalTransform>(_character).Position;
			_sample.SetInput(new MmdSampleInput { Crouch = true, Movement = new float2(0f, 1f) });
			Advance(30);
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.z - stoppedAt.z, Is.GreaterThan(0.1f));
			Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz), Is.GreaterThan(0.5f));
			Assert.That(manager.GetComponentData<MmdSearchSettings>(_character).TrajectoryPositionWeight, Is.EqualTo(1f));
			AssertHealthy();
		}

		private quaternion ModelRotation(ref ColInt.MMD.Database.Data.MotionDatabase database, int bone) {
			var poses = _sample.World.EntityManager.GetBuffer<MmdLocalPose>(_character);
			quaternion rotation = quaternion.identity;
			for (int current = bone; current >= 0; current = database.Parents[current]) {
				rotation = math.mul(poses[current].Value.Rotation, rotation);
			}
			return rotation;
		}

		private void AssertCrouchedPose() {
			var manager = _sample.World.EntityManager;
			var database = manager.GetComponentData<MmdDatabase>(_character).Value;
			var poses = manager.GetBuffer<MmdLocalPose>(_character);
			var models = new float4x4[poses.Length];
			float top = 0f;
			for (int bone = 0; bone < poses.Length; bone++) {
				var pose = poses[bone].Value;
				var local = float4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
				int parent = database.Value.Parents[bone];
				models[bone] = parent < 0 ? local : math.mul(models[parent], local);
				top = math.max(top, models[bone].c3.y);
			}
			float feet = math.min(models[database.Value.LeftFootBone].c3.y, models[database.Value.RightFootBone].c3.y);
			Assert.That(feet, Is.InRange(-0.15f, 0.25f));
			Assert.That(top, Is.InRange(0.7f, 1.6f));
		}

		private void AssertHealthy() {
			var manager = _sample.World.EntityManager;
			Assert.That(manager.GetComponentData<MmdActionState>(_character).Rejected, Is.False);
			Assert.That(manager.GetComponentData<MmdFault>(_character).Code, Is.EqualTo(ColInt.MMD.Lifecycle.Data.MmdFaultCode.None));
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
