using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Baking;
using ColInt.MMD.Editor.Diagnostics;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Components;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColInt.MMD.Tests {
	public class MmdSampleTraversalTests {
		private const float Timestep = 1f / 60f;
		private static readonly string ConfigurationPath = MmdSampleTestAssets.GetPath("Configurations/SampleCharacterConfiguration.asset");
		private MmdSampleCharacterConfiguration _configuration;
		private MmdSampleWorld _sample;
		private Entity _character;
		private Entity _floor;

		[SetUp]
		public void SetUp() {
			_configuration = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(ConfigurationPath));
			_configuration.Assets.RenderPrefab = default;
			_sample = new MmdSampleWorld(Timestep);
			_floor = AddBox(new float3(0f, -0.5f, 0f), new float3(120f, 1f, 120f));
		}

		[TearDown]
		public void TearDown() {
			_sample.Dispose();
			Object.DestroyImmediate(_configuration);
		}

		[TestCase(0.6f)]
		[TestCase(1.1f)]
		[TestCase(1.35f)]
		[TestCase(1.5f)]
		[TestCase(2.2f)]
		[TestCase(2.8f)]
		public void SpaceClimbsWideObstaclesAcrossHeightRangesWithoutTeleport(float height) {
			Obstacle(height, 3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.False);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleClimb>>(_character), Is.EqualTo(height > _configuration.Traversal.MantleHeight));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleMantle>>(_character), Is.EqualTo(height <= _configuration.Traversal.MantleHeight));
			FinishSmoothTraversal();
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.EqualTo(height).Within(0.08f));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsMassOverride>(_character).IsKinematic, Is.Zero);
			Assert.That(manager.GetComponentData<PhysicsGravityFactor>(_character).Value, Is.EqualTo(1f));
		}

		[TestCase(0.6f, false)]
		[TestCase(1.1f, false)]
		[TestCase(0.9f, true)]
		public void NarrowObstacleUsesVaultOrHurdleAndReturnsToLowerFloor(float height, bool moving) {
			Obstacle(height, 0.6f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true, Movement = moving ? new float2(0f, 1f) : float2.zero });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleHurdle>>(_character), Is.EqualTo(moving));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleVault>>(_character), Is.EqualTo(!moving));
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.z, Is.GreaterThan(1.9f));
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.InRange(-0.03f, 0.08f));
		}

		[TestCase(1.2f, 3f, false)]
		[TestCase(2.4f, 3f, false)]
		[TestCase(1.2f, 0.6f, false)]
		[TestCase(1.2f, 0.6f, true)]
		public void SpaceWhileFallingCatchesTheLedgeWithoutReplayingSourceFall(float height, float depth, bool run) {
			Obstacle(height, depth);
			CreateCharacter(new float3(0f, 0.85f, 0.5f));
			Advance(16);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.True);
			float before = manager.GetComponentData<LocalTransform>(_character).Position.y;
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true, Run = run });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.GreaterThan(before - 0.05f));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCatchCliff>>(_character), Is.EqualTo(height > 2f));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCatchMantle>>(_character), Is.EqualTo(depth > 2f && height < 2f));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCatchVault>>(_character), Is.EqualTo(depth < 1f && !run));
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCatchHurdle>>(_character), Is.EqualTo(depth < 1f && run));
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.EqualTo(depth > 2f ? height : 0f).Within(0.08f));
		}

		[TestCase(0f)]
		[TestCase(1.5f)]
		[TestCase(4f)]
		public void AirborneCatchDoesNotStartBeforeCharacterReachesTheWall(float speed) {
			Obstacle(2.4f, 3f);
			CreateCharacter(new float3(0f, 0.85f, 0f));
			Advance(16);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.True);
			var velocity = manager.GetComponentData<PhysicsVelocity>(_character);
			velocity.Linear.z = speed;
			manager.SetComponentData(_character, velocity);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true, Movement = speed > 0f ? new float2(0f, 1f) : float2.zero, Run = speed > 2f });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False,
				"A buffered jump must not pull a distant airborne character onto the wall.");
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleJumpIntent>(_character).TraversalRemaining, Is.GreaterThan(0f));
		}

		[Test]
		public void SpaceWithoutReachableLedgeKeepsOrdinaryJump() {
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
		}

		[Test]
		public void CeilingOverLedgeRejectsTraversal() {
			Obstacle(1f, 3f);
			AddBox(new float3(0f, 2.4f, 2.5f), new float3(4f, 0.3f, 3f));
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
		}

		[Test]
		public void DestroyedLedgeAbortsIntoFallAndRestoresDynamicPhysics() {
			Entity obstacle = Obstacle(2.2f, 3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			Advance(65);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			manager.DestroyEntity(obstacle);
			Advance(3);
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.True);
			Assert.That(manager.GetComponentData<PhysicsMassOverride>(_character).IsKinematic, Is.Zero);
			Assert.That(manager.GetComponentData<PhysicsGravityFactor>(_character).Value, Is.EqualTo(1f));
		}

		[Test]
		public void PauseAndDisabledPreserveTraversalAndInputCannotRestartIt() {
			Obstacle(2.2f, 3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			Advance(40);
			var manager = _sample.World.EntityManager;
			float time = manager.GetComponentData<MmdPlayback>(_character).Time;
			float3 position = manager.GetComponentData<LocalTransform>(_character).Position;
			uint revision = manager.GetComponentData<MmdActionRequest>(_character).Revision;
			_sample.SetPaused(true);
			Advance(30);
			Assert.That(manager.GetComponentData<MmdPlayback>(_character).Time, Is.EqualTo(time));
			Assert.That(math.distance(position, manager.GetComponentData<LocalTransform>(_character).Position), Is.Zero);
			_sample.SetPaused(false);
			manager.AddComponent<Disabled>(_character);
			Advance(10);
			Assert.That(manager.GetComponentData<MmdPlayback>(_character).Time, Is.EqualTo(time));
			manager.RemoveComponent<Disabled>(_character);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.GetComponentData<MmdActionRequest>(_character).Revision, Is.EqualTo(revision));
			FinishSmoothTraversal();
		}

		[Test]
		public void ThinTallWallClimbsToItsTopRatherThanChoosingAnUnsupportedDestination() {
			Obstacle(2.2f, 0.3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleState<MmdSampleClimb>>(_character), Is.True);
			FinishSmoothTraversal();
			Assert.That(_sample.World.EntityManager.GetComponentData<LocalTransform>(_character).Position.y, Is.EqualTo(2.2f).Within(0.08f));
		}

		[Test]
		public void NewCeilingDuringClimbAbortsWithoutPassingThroughIt() {
			Obstacle(2.2f, 3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			Advance(35);
			AddBox(new float3(0f, 2.8f, 2f), new float3(4f, 0.3f, 4f));
			Advance(50);
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
			Assert.That(manager.GetComponentData<PhysicsMassOverride>(_character).IsKinematic, Is.Zero);
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.LessThan(1.2f));
		}

		[TestCase(0.4f, 3f)]
		[TestCase(3.2f, 3f)]
		[TestCase(1.1f, 0.3f)]
		public void OutOfHeightRangeAndTooNarrowLedgesRejectTraversal(float height, float width) {
			AddBox(new float3(0f, height * 0.5f, 2.5f), new float3(width, height, 3f));
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true });
			_sample.Tick();
			Assert.That(_sample.World.EntityManager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
		}

		[Test]
		public void VaultIntoEmptySpaceReturnsToFallWithItsExitMomentum() {
			var manager = _sample.World.EntityManager;
			manager.DestroyEntity(_floor);
			AddBox(new float3(0f, -0.5f, -9f), new float3(20f, 1f, 20f));
			Obstacle(0.9f, 0.6f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { JumpPressed = true, Movement = new float2(0f, 1f), Run = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			for (int tick = 0; tick < 200; tick++) {
				_sample.Tick();
				if (manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character)) {
					Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.z, Is.GreaterThan(0.5f));
					Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.y, Is.LessThan(-1f));
					Assert.That(manager.GetComponentData<PhysicsMassOverride>(_character).IsKinematic, Is.Zero);
					return;
				}
			}
			Assert.Fail("An unsupported vault must return to Fall.");
		}

		private void AssertPoseRemainsNearTheRoot() {
			var manager = _sample.World.EntityManager;
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
			Assert.That(feet, Is.InRange(-1.5f, 2.5f), "The source fall height must not leak into the rendered skeleton.");
		}

		[TestCase(1f, 0.6f)]
		[TestCase(2.2f, 3f)]
		public void RunningApproachSelectsRunningTraversalBeforeTheShortStandingProbe(float height, float depth) {
			CreateCharacter(float3.zero);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			Advance(90);
			var manager = _sample.World.EntityManager;
			float front = manager.GetComponentData<LocalTransform>(_character).Position.z + 2.1f;
			AddBox(new float3(0f, height * 0.5f, front + depth * 0.5f), new float3(3f, height, depth));
			_sample.Tick();
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.False);
			Assert.That(manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip.EntrySpeed, Is.GreaterThan(3f));
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
		}

		[TestCase(0f)]
		[TestCase(0.3f)]
		[TestCase(0.65f)]
		[TestCase(0.9f)]
		public void ReleasingRunAndForwardDuringMediumLedgeClimbReturnsToIdle(float releaseFraction) {
			CreateCharacter(float3.zero);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			Advance(90);
			var manager = _sample.World.EntityManager;
			float front = manager.GetComponentData<LocalTransform>(_character).Position.z + 2.1f;
			AddBox(new float3(0f, 0.7f, front + 0.75f), new float3(1.8f, 1.4f, 1.5f));
			_sample.Tick();
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleMantle>>(_character), Is.True);
			var clip = manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip;
			float remaining = clip.Duration - manager.GetComponentData<MmdPlayback>(_character).Time;
			int releaseTick = (int)(remaining / Timestep * releaseFraction);
			var target = manager.GetComponentData<MmdSampleTraversalTarget>(_character);
			float3 previous = manager.GetComponentData<LocalTransform>(_character).Position;
			bool settled = false;
			for (int tick = 0; tick < 150; tick++) {
				_sample.World.EntityManager.SetComponentData(_character, tick < releaseTick ? new MmdSampleInput { Movement = new float2(0f, 1f), Run = true } : default);
				_sample.Tick();
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				var action = manager.GetComponentData<MmdActionState>(_character);
				var position = manager.GetComponentData<LocalTransform>(_character).Position;
				var database = manager.GetComponentData<MmdDatabase>(_character).Value;

				if (tick > releaseTick && !manager.HasComponent<MmdSampleTraversalActive>(_character) && manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character)) {
					settled = true;
					int idle = manager.GetComponentData<ColInt.MMD.Samples.Character.Components.MmdSampleCharacterControl>(_character).IdleGroup;
					Assert.That(MotionDatabaseGroups.Contains(ref database.Value, database.Value.Clips[playback.ClipIndex], idle), Is.True);
					Assert.That(action.ActionId, Is.Zero);
					Assert.That(database.Value.Clips[playback.ClipIndex].Loop, Is.True, "Stopped traversal must settle into looping Idle.");
					Assert.That(math.distance(position.xz, target.End.xz), Is.LessThan(0.15f));
					var poses = manager.GetBuffer<MmdLocalPose>(_character);
					var transform = manager.GetComponentData<LocalTransform>(_character);
					var contact = manager.GetComponentData<MmdSampleFootContact>(_character);
					AssertFootOutsideObstacle(position + math.rotate(transform.Rotation, BonePosition(ref database.Value, poses, database.Value.LeftFootBone)), target);
					AssertFootOutsideObstacle(position + math.rotate(transform.Rotation, BonePosition(ref database.Value, poses, database.Value.RightFootBone)), target);
					AssertFootOutsideObstacle(position + math.rotate(transform.Rotation, BonePosition(ref database.Value, poses, contact.LeftToe)), target);
					AssertFootOutsideObstacle(position + math.rotate(transform.Rotation, BonePosition(ref database.Value, poses, contact.RightToe)), target);
				}
				previous = position;
			}
			Assert.That(settled, Is.True);
			Assert.That(previous.y, Is.EqualTo(1.4f).Within(0.08f));
			Assert.That(math.distance(previous.xz, target.End.xz), Is.LessThan(0.15f), "The stopped character must remain on the ledge after returning to Idle.");
		}

		[TestCase(0, 0.4f)]
		[TestCase(1, 0.4f)]
		[TestCase(0, 1.2f)]
		[TestCase(1, 1.2f)]
		[TestCase(0, 2.1f)]
		[TestCase(1, 2.1f)]
		public void RunningMantleSettlesOnTheTopAndHandsOffWithoutFallOrSpeedLoss(int foot, float approachDistance) {
			CreateCharacter(float3.zero);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			Advance(90);
			var manager = _sample.World.EntityManager;
			var clips = manager.GetBuffer<MmdSampleTraversalClip>(_character);
			int variant = 0;
			int selected = -1;
			foreach (var candidate in clips) {
				if (candidate.ActionId == 22 && candidate.EntrySpeed > 3f && variant++ == foot) {
					selected = candidate.ClipIndex;
				}
			}
			Assert.That(selected, Is.GreaterThanOrEqualTo(0));
			for (int index = clips.Length - 1; index >= 0; index--) {
				if (clips[index].ActionId == 22 && clips[index].ClipIndex != selected) {
					clips.RemoveAt(index);
				}
			}
			float front = manager.GetComponentData<LocalTransform>(_character).Position.z + approachDistance;
			AddBox(new float3(0f, 0.70000005f, front + 0.75f), new float3(1.8f, 1.4000001f, 1.5f));
			_sample.Tick();
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleMantle>>(_character), Is.True);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			var target = manager.GetComponentData<MmdSampleTraversalTarget>(_character);
			var profile = manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip;
			float previousHeight = float.PositiveInfinity;
			int locomotionTicks = 0;
			for (int tick = 0; tick < 150 && locomotionTicks < 6; tick++) {
				_sample.Tick();
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.False, "Supported pull-up must hand off directly to locomotion.");
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				var position = manager.GetComponentData<LocalTransform>(_character).Position;
				if (manager.HasComponent<MmdSampleTraversalActive>(_character) && playback.Time >= profile.PeakTime) {
					Assert.That(position.y, Is.GreaterThanOrEqualTo(target.End.y - 0.001f), "The capsule must remain above the top during settling.");
					Assert.That(position.y, Is.LessThanOrEqualTo(previousHeight + 0.001f), "The end of the path must not lift the character again.");
					previousHeight = position.y;
				}
				if (manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character)) {
					locomotionTicks++;
					Assert.That(manager.GetComponentData<MmdSampleGroundState>(_character).IsGrounded, Is.True, "Support must be available from the first locomotion tick.");
					Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.z, Is.GreaterThan(3f), "Consuming the last fraction of the traversal must not reset running speed.");
					var database = manager.GetComponentData<MmdDatabase>(_character).Value;
					var control = manager.GetComponentData<ColInt.MMD.Samples.Character.Components.MmdSampleCharacterControl>(_character);
					Assert.That(MotionDatabaseGroups.Contains(ref database.Value, database.Value.Clips[playback.ClipIndex], control.RunGroup), Is.True);
					Assert.That(MotionDatabaseGroups.Contains(ref database.Value, database.Value.Clips[playback.ClipIndex], control.StartGroup), Is.False);
					Assert.That(database.Value.Clips[playback.ClipIndex].Loop, Is.True, "A moving action handoff must not enter a gait transition or pivot.");
				}
			}
			Assert.That(locomotionTicks, Is.EqualTo(6));
		}

		[TestCase(false, 1.5f)]
		[TestCase(true, 1.5f)]
		[TestCase(false, 3f)]
		[TestCase(true, 3f)]
		public void HeldForwardContinuesMovingAfterRunningMediumLedgeClimb(bool runAfterClimb, float depth) {
			CreateCharacter(float3.zero);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
			Advance(90);
			var manager = _sample.World.EntityManager;
			float front = manager.GetComponentData<LocalTransform>(_character).Position.z + 2.1f;
			AddBox(new float3(0f, 0.7f, front + depth * 0.5f), new float3(1.8f, 1.4f, depth));
			_sample.Tick();
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleMantle>>(_character), Is.True);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = runAfterClimb });
			for (int tick = 0; tick < 150 && manager.HasComponent<MmdSampleTraversalActive>(_character); tick++) {
				_sample.Tick();
				var progress = manager.GetComponentData<MmdSampleTraversalProgress>(_character);
				var current = manager.GetComponentData<MmdPlayback>(_character);
				if (current.ClipIndex == progress.Clip.ClipIndex && current.Time >= progress.Clip.Duration - 0.1f) {
					Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.z, Is.GreaterThan(1f), "The running pull-up must not brake to a standstill before locomotion takes over.");
				}
			}
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
			float3 start = manager.GetComponentData<LocalTransform>(_character).Position;
			var control = manager.GetComponentData<ColInt.MMD.Samples.Character.Components.MmdSampleCharacterControl>(_character);
			int gait = runAfterClimb ? control.RunGroup : control.WalkGroup;
			for (int tick = 0; tick < 6; tick++) {
				var current = manager.GetComponentData<MmdPlayback>(_character);
				var database = manager.GetComponentData<MmdDatabase>(_character).Value;
				Assert.That(MotionDatabaseGroups.Contains(ref database.Value, database.Value.Clips[current.ClipIndex], gait), Is.True, "The first locomotion tick must select the held gait, without an intermediate stop.");
				Assert.That(MotionDatabaseGroups.Contains(ref database.Value, database.Value.Clips[current.ClipIndex], control.StartGroup), Is.False, "A running exit must not restart from rest.");
				Assert.That(manager.GetComponentData<PhysicsVelocity>(_character).Linear.z, Is.GreaterThan(1f), "The handoff must retain forward momentum.");
				_sample.Tick();
			}
			Advance(6);
			var position = manager.GetComponentData<LocalTransform>(_character).Position;
			var playback = manager.GetComponentData<MmdPlayback>(_character);
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[playback.ClipIndex], gait), Is.True);
			Assert.That(position.z - start.z, Is.GreaterThan(0.1f));
			Assert.That(position.y, Is.EqualTo(1.4f).Within(0.08f));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CompletedCrossingReleasesItsPoseEvenWhenPhysicsHasNotReachedTheEndpoint(bool run) {
			Obstacle(1f, 1f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = run ? new float2(0f, 1f) : float2.zero, Run = run, JumpPressed = true });
			_sample.Tick();
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			var manager = _sample.World.EntityManager;
			for (int tick = 0; tick < 200 && !manager.GetComponentData<MmdActionState>(_character).Completed; tick++) {
				_sample.Tick();
			}
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdActionState>(_character).Completed, Is.True);
			var transform = manager.GetComponentData<LocalTransform>(_character);
			transform.Position.x += 0.25f;
			manager.SetComponentData(_character, transform);
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False, "A completed crossing must hand back control even when external displacement prevents arrival.");
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.Zero);
			Assert.That(manager.GetComponentData<PhysicsMassOverride>(_character).IsKinematic, Is.Zero);
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.x, Is.EqualTo(transform.Position.x).Within(0.02f), "Recovery must not teleport the character to the endpoint.");
			Advance(60);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		[TestCase(7)]
		[TestCase(8)]
		[TestCase(9)]
		[TestCase(10)]
		[TestCase(11)]
		[TestCase(12)]
		public void EveryVaultAndHurdleClipReleasesControlWithinTwoTicksOfItsLastFrame(int variant) {
			AddBox(new float3(0f, 0.5f, 1.5f), new float3(1.8f, 1f, 1f));
			CreateCharacter(float3.zero);
			Advance(30);
			var manager = _sample.World.EntityManager;
			int vault = manager.GetComponentData<MmdSampleVault>(_character).ActionId;
			int hurdle = manager.GetComponentData<MmdSampleHurdle>(_character).ActionId;
			var clips = manager.GetBuffer<MmdSampleTraversalClip>(_character);
			var candidates = new System.Collections.Generic.List<MmdSampleTraversalClip>();
			foreach (var clip in clips) {
				if (clip.ActionId == vault || clip.ActionId == hurdle) {
					candidates.Add(clip);
				}
			}
			var chosen = candidates[variant];
			clips.Clear();
			clips.Add(chosen);
			bool moving = chosen.ActionId == hurdle;
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = moving ? new float2(0f, 1f) : float2.zero, Run = moving && chosen.EntrySpeed > 3f, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True, "Clip " + chosen.ClipIndex);
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			int heldTicks = 0;
			for (int tick = 0; tick < 220 && manager.HasComponent<MmdSampleTraversalActive>(_character); tick++) {
				_sample.Tick();
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				var action = manager.GetComponentData<MmdActionState>(_character);
				if (action.Completed && manager.HasComponent<MmdSampleTraversalActive>(_character)) {
					heldTicks++;
				}
				Assert.That(heldTicks, Is.LessThanOrEqualTo(2), "Completed clip " + chosen.ClipIndex + " remains in Traversal at " + playback.Time + "; position=" + manager.GetComponentData<LocalTransform>(_character).Position + "; end=" + manager.GetComponentData<MmdSampleTraversalTarget>(_character).End);
			}
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.False, "A supported landing must not use the abort timeout.");
			Assert.That(manager.GetComponentData<MmdActionState>(_character).ActionId, Is.Zero);
			Assert.That(manager.GetComponentData<PhysicsMassOverride>(_character).IsKinematic, Is.Zero);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void MediumLedgeUsesMantleInsteadOfTheTwoAndHalfMetreClimb(bool run) {
			Obstacle(1.4f, 3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = run, JumpPressed = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleMantle>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip.RootHeight, Is.LessThan(1.5f));
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.EqualTo(1.4f).Within(0.08f));
		}

		[TestCase(2f)]
		[TestCase(2.5f)]
		[TestCase(3f)]
		public void RunningClimbStopsAtTheNearEdgeWithoutAnExtraForwardExtension(float height) {
			Obstacle(height, 3f);
			CreateCharacter(float3.zero);
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, JumpPressed = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleClimb>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip.EntrySpeed, Is.GreaterThan(3f));
			var target = manager.GetComponentData<MmdSampleTraversalTarget>(_character);
			Assert.That(target.End.z, Is.InRange(1f, 1.1f), "The stop must remain at the edge instead of adding a full capsule radius to the landing depth.");
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.z, Is.EqualTo(target.End.z).Within(0.05f));
			float3 stopped = manager.GetComponentData<LocalTransform>(_character).Position;
			for (int tick = 0; tick < 60; tick++) {
				_sample.Tick();
				float3 position = manager.GetComponentData<LocalTransform>(_character).Position;
				Assert.That(position.z, Is.InRange(1f, 1.1f), "Released input must not inherit extra forward displacement after the climb.");
				Assert.That(math.distance(position.xz, stopped.xz), Is.LessThan(0.03f));
				Assert.That(position.y, Is.EqualTo(height).Within(0.08f));
				Assert.That(manager.HasComponent<MmdSampleState<MmdSampleFall>>(_character), Is.False);
			}
		}

		[Test]
		public void PressingIntoWallEntersClimbWithoutPlayingItsWalkingApproach() {
			Obstacle(2.2f, 3f);
			CreateCharacter(new float3(0f, 0f, 0.66f));
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f) });
			Advance(90);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), JumpPressed = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			var profile = manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip;
			Assert.That(profile.EntrySpeed, Is.LessThan(1.5f));
			Assert.That(profile.StartTime, Is.GreaterThan(0.2f));
			Assert.That(manager.GetComponentData<MmdInertialization>(_character).PoseDuration, Is.LessThanOrEqualTo(0.1f));
			Assert.That(manager.GetComponentData<MmdPlayback>(_character).Time, Is.GreaterThanOrEqualTo(profile.StartTime));
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
		}

		[TestCase(0.6f)]
		[TestCase(1.1f)]
		public void HurdleMovesForwardDuringLiftAndWarpsItsAuthoredRootToObstacleHeight(float height) {
			Obstacle(height, 0.6f);
			CreateCharacter(new float3(0f, 0f, -0.7f));
			Advance(30);
			var manager = _sample.World.EntityManager;
			manager.SetComponentData(_character, new PhysicsVelocity { Linear = new float3(0f, 0f, 4f) });
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleHurdle>>(_character), Is.True);
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			float highest = 0f;
			float forwardDuringLift = 0f;
			float3 previous = manager.GetComponentData<LocalTransform>(_character).Position;
			for (int tick = 0; tick < 240 && manager.HasComponent<MmdSampleTraversalActive>(_character); tick++) {
				_sample.Tick();
				float3 position = manager.GetComponentData<LocalTransform>(_character).Position;
				highest = math.max(highest, position.y);
				if (manager.HasComponent<MmdSampleTraversalActive>(_character)) {
					Assert.That(manager.GetComponentData<MmdRootMotion>(_character).Translation.y, Is.EqualTo(position.y - previous.y).Within(0.001f));
				}
				if (position.y > 0.15f && position.y < height * 0.9f && position.y > previous.y) {
					forwardDuringLift += position.z - previous.z;
				}
				Assert.That(position.z, Is.GreaterThanOrEqualTo(previous.z - 0.01f));
				previous = position;
			}
			Assert.That(forwardDuringLift, Is.GreaterThan(0.08f), "A hurdle must rise and travel together, without a separate vertical elevator phase.");
			Assert.That(highest, Is.InRange(height - 0.05f, height + 0.2f));
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
			Assert.That(manager.GetComponentData<PhysicsCollider>(_character).Value,
				Is.EqualTo(manager.GetComponentData<MmdSamplePhysicsColliderOwner>(_character).Value));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void OrdinaryJumpWaitsUntilNearTheWallBeforeCatchingIt(bool run) {
			float height = _configuration.Traversal.MaximumHeight + 0.1f;
			Obstacle(height, 3f);
			CreateCharacter(new float3(0f, 0f, 0.25f));
			Advance(30);
			var manager = _sample.World.EntityManager;
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = run, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			bool caught = false;
			for (int tick = 0; tick < 30 && !caught; tick++) {
				float before = manager.GetComponentData<LocalTransform>(_character).Position.z;
				_sample.Tick();
				caught = manager.HasComponent<MmdSampleTraversalActive>(_character);
				if (caught) {
					var settings = manager.GetComponentData<MmdSampleTraversalSettings>(_character);
					float capsuleGap = 1f - before - settings.Radius - settings.Skin;
					Assert.That(capsuleGap, Is.LessThanOrEqualTo(settings.CatchDistance + 0.001f));
				}
			}
			Assert.That(caught, Is.True, "The buffered jump must still catch when the character reaches the nearby wall.");
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCatchCliff>>(_character), Is.True);
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
		}

		[Test]
		public void BufferedSpaceCatchesTallWallWhileTheInitialJumpIsStillRising() {
			float height = _configuration.Traversal.MaximumHeight + 0.1f;
			Obstacle(height, 3f);
			CreateCharacter(new float3(0f, 0f, 0.66f));
			Advance(30);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), JumpPressed = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleJump>>(_character), Is.True);
			for (int tick = 0; tick < 22 && !manager.HasComponent<MmdSampleTraversalActive>(_character); tick++) {
				_sample.Tick();
			}
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleCatchCliff>>(_character), Is.True);
			Assert.That(manager.GetComponentData<MmdSampleJumpIntent>(_character).TraversalRemaining, Is.Zero);
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			FinishSmoothTraversal();
			Assert.That(manager.GetComponentData<LocalTransform>(_character).Position.y, Is.EqualTo(height).Within(0.08f));
		}

		[TestCase(2f, 0f, 0f, "M_Neutral_Traversal_Climb_Start_2_5_run_F_Lfoot")]
		[TestCase(2.5f, 0f, 0f, "M_Neutral_Traversal_Climb_Start_2_5_run_F_Lfoot")]
		[TestCase(3f, 0f, 0f, "M_Neutral_Traversal_Climb_Start_2_5_run_F_Lfoot")]
		[TestCase(2f, 0.66f, 0.55f, "M_Neutral_Traversal_Climb_Start_2_5_run_F_Rfoot")]
		[TestCase(2.5f, 0.66f, 0.55f, "M_Neutral_Traversal_Climb_Start_2_5_run_F_Rfoot")]
		[TestCase(3f, 0.66f, 0.55f, "M_Neutral_Traversal_Climb_Start_2_5_run_F_Rfoot")]
		public void ClimbContactFollowsTheActualLedge(float height, float startDistance, float yaw, string clipName) {
			quaternion rotation = quaternion.RotateY(yaw);
			float3 forward = math.forward(rotation);
			Entity obstacle = AddBox(math.rotate(rotation, new float3(0f, height * 0.5f, 2.5f)), new float3(3f, height, 3f));
			var manager = _sample.World.EntityManager;
			var obstacleTransform = manager.GetComponentData<LocalTransform>(obstacle);
			obstacleTransform.Rotation = rotation;
			manager.SetComponentData(obstacle, obstacleTransform);
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromPositionRotation(forward * startDistance, rotation), true, false);
			Advance(30);
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var rig = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleBakeConfiguration.asset")).Rig;
			var animator = rig.GetComponent<Animator>();
			int left = HumanoidBone(ref data.Value, animator, HumanBodyBones.LeftHand);
			int right = HumanoidBone(ref data.Value, animator, HumanBodyBones.RightHand);
			int leftToe = HumanoidBone(ref data.Value, animator, HumanBodyBones.LeftToes);
			int rightToe = HumanoidBone(ref data.Value, animator, HumanBodyBones.RightToes);
			var catalogs = new MmdDebugCatalogStore();
			catalogs.Reload();
			int forced = System.Array.IndexOf(catalogs.Resolve(data).Names, clipName);
			Assert.That(forced, Is.GreaterThanOrEqualTo(0));
			var profiles = manager.GetBuffer<MmdSampleTraversalClip>(_character);
			int action = manager.GetComponentData<MmdSampleClimb>(_character).ActionId;
			int anchors = 0;
			for (int index = profiles.Length - 1; index >= 0; index--) {
				if (profiles[index].ActionId == action) {
					Assert.That(profiles[index].ContactTime, Is.GreaterThan(profiles[index].LiftTime), "Every climb entry must identify its authored hand plant.");
					Assert.That(profiles[index].ContactTime, Is.LessThan(profiles[index].PeakTime));
					anchors++;
					if (profiles[index].ClipIndex != forced) {
						profiles.RemoveAt(index);
					}
				}
			}
			Assert.That(anchors, Is.EqualTo(6));
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = forward.xz, Run = true, JumpPressed = true });
			_sample.Tick();
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.True);
			_sample.World.EntityManager.SetComponentData(_character, default(MmdSampleInput));
			var chosen = manager.GetComponentData<MmdSampleTraversalProgress>(_character).Clip;
			var target = manager.GetComponentData<MmdSampleTraversalTarget>(_character);
			float3 previous = manager.GetComponentData<LocalTransform>(_character).Position;
			int contactSamples = 0;
			for (int tick = 0; tick < 240 && manager.HasComponent<MmdSampleTraversalActive>(_character); tick++) {
				_sample.Tick();
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				var transform = manager.GetComponentData<LocalTransform>(_character);
				Assert.That(math.distance(previous, transform.Position), Is.LessThan(0.3f), "Warping must preserve continuous root movement.");
				previous = transform.Position;
				var displayed = manager.GetBuffer<MmdLocalPose>(_character);
				float3 leftFoot = transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, displayed, data.Value.LeftFootBone));
				float3 rightFoot = transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, displayed, data.Value.RightFootBone));
				float3 leftBall = transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, displayed, leftToe));
				float3 rightBall = transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, displayed, rightToe));
				AssertFootOutsideObstacle(leftFoot, target);
				AssertFootOutsideObstacle(rightFoot, target);
				AssertFootOutsideObstacle(leftBall, target);
				AssertFootOutsideObstacle(rightBall, target);
				if (playback.Time >= chosen.ContactTime + 0.05f && playback.Time <= chosen.ContactTime + 0.25f) {
					var poses = manager.GetBuffer<MmdLocalPose>(_character);
					float3 hands = (BonePosition(ref data.Value, poses, left) + BonePosition(ref data.Value, poses, right)) * 0.5f;
					float3 world = transform.Position + math.rotate(transform.Rotation, hands);
					Assert.That(world.y, Is.EqualTo(target.Ledge.y).Within(0.15f), "The hand plant must follow the real height rather than the authored 2.5 m height.");
					Assert.That(math.abs(math.dot(world - target.Ledge, forward)), Is.LessThan(0.15f), "The hands must reach the wall edge from both distant and close approaches.");
					contactSamples++;
				}
			}
			Assert.That(contactSamples, Is.GreaterThan(5));
			Assert.That(manager.HasComponent<MmdSampleTraversalActive>(_character), Is.False);
			for (int tick = 0; tick < 45; tick++) {
				_sample.Tick();
				var transform = manager.GetComponentData<LocalTransform>(_character);
				var poses = manager.GetBuffer<MmdLocalPose>(_character);
				AssertFootOutsideObstacle(transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, poses, data.Value.LeftFootBone)), target);
				AssertFootOutsideObstacle(transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, poses, data.Value.RightFootBone)), target);
				AssertFootOutsideObstacle(transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, poses, leftToe)), target);
				AssertFootOutsideObstacle(transform.Position + math.rotate(transform.Rotation, BonePosition(ref data.Value, poses, rightToe)), target);
			}
			var finish = manager.GetComponentData<LocalTransform>(_character).Position;
			Assert.That(finish.y, Is.EqualTo(height).Within(0.08f));
			Assert.That(math.dot(finish, forward), Is.InRange(1f, 1.1f), "The climb must settle at the ledge without a capsule-radius run-out.");
			Assert.That(manager.HasComponent<MmdSampleState<MmdSampleLocomotion>>(_character), Is.True);
		}

		private static void AssertFootOutsideObstacle(float3 point, in MmdSampleTraversalTarget target) {
			float clearance = math.max(point.y - target.Ledge.y, -math.dot(point - target.Ledge, math.forward(target.Rotation)));
			Assert.That(clearance, Is.GreaterThanOrEqualTo(-0.005f), "Knees, ankles and toes must stay outside the front face until they clear the ledge's top.");
		}

		private static int HumanoidBone(ref MotionDatabase database, Animator animator, HumanBodyBones boneType) {
			string path = AnimationUtility.CalculateTransformPath(animator.GetBoneTransform(boneType), animator.transform);
			int hash = BonePathHash.Calculate(path);
			for (int bone = 0; bone < database.Parents.Length; bone++) {
				if (database.BoneNameHashes[bone] == hash) {
					return bone;
				}
			}
			Assert.Fail(path);
			return -1;
		}

		private static float3 BonePosition(ref MotionDatabase database, DynamicBuffer<MmdLocalPose> poses, int bone) {
			float3 position = float3.zero;
			for (int current = bone; current >= 0; current = database.Parents[current]) {
				var pose = poses[current].Value;
				position = pose.Position + math.rotate(pose.Rotation, position * pose.Scale);
			}
			return position;
		}

		private void FinishSmoothTraversal() {
			var manager = _sample.World.EntityManager;
			float3 previous = manager.GetComponentData<LocalTransform>(_character).Position;
			for (int tick = 0; tick < 320; tick++) {
				_sample.Tick();
				AssertPoseRemainsNearTheRoot();
				float3 next = manager.GetComponentData<LocalTransform>(_character).Position;
				Assert.That(math.distance(previous, next), Is.LessThan(0.3f), "Traversal must advance continuously without teleporting to the ledge.");
				Assert.That(next.y, Is.GreaterThan(-0.1f));
				Assert.That(manager.GetComponentData<MmdActionState>(_character).Rejected, Is.False);
				previous = next;
				if (!manager.HasComponent<MmdSampleTraversalActive>(_character)) {
					Advance(15);
					return;
				}
			}
			Assert.Fail("Traversal did not release control after its action.");
		}

		private Entity Obstacle(float height, float depth) {
			return AddBox(new float3(0f, height * 0.5f, 1f + depth * 0.5f), new float3(3f, height, depth));
		}

		private void CreateCharacter(float3 position) {
			_character = _sample.Characters.Create(_configuration, LocalTransform.FromPosition(position), true, false);
		}

		private void Advance(int ticks) {
			for (int tick = 0; tick < ticks; tick++) {
				_sample.Tick();
			}
		}

		private Entity AddBox(float3 position, float3 size) {
			var manager = _sample.World.EntityManager;
			var collider = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, size, 0f);
			Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
			manager.SetComponentData(entity, LocalTransform.FromPosition(position));
			manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
			manager.AddSharedComponent(entity, new PhysicsWorldIndex());
			manager.AddComponentData(entity, new MmdSamplePhysicsColliderOwner(collider));
			return entity;
		}
	}
}
