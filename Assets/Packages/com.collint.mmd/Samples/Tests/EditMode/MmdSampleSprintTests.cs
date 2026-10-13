using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
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
	public class MmdSampleSprintTests {
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

		[TestCase(12, false)]
		[TestCase(36, false)]
		[TestCase(90, false)]
		[TestCase(12, true)]
		[TestCase(36, true)]
		public void SharpSprintTurnsKeepKneesStable(int interval, bool orbit) {
			CreateFlatCharacter();
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Sprint = true });
			Advance(150);
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			int left = FindBone(ref data.Value, "root/pelvis/thigh_l/calf_l");
			int right = FindBone(ref data.Value, "root/pelvis/thigh_r/calf_r");
			for (int tick = 0; tick < 360; tick++) {
				float angle = orbit ? tick * math.PI / interval : (tick / interval) % 2 == 0 ? math.PI * 0.75f : -math.PI * 0.75f;
				var direction = new float2(math.sin(angle), math.cos(angle));
				_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Run = true, Sprint = true, Movement = direction });
				_sample.Tick();
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[playback.ClipIndex], control.StartGroup), Is.False, "Moving sprint turns must not splice stationary reface starts.");
				var poses = manager.GetBuffer<MmdLocalPose>(_character);
				// UEFN knees flex around local Z. Authored sprint pivots contain up to
				// 21 degrees of lateral swing; retain headroom for continuous blending.
				Assert.That(KneeSwing(poses[left].Value.Rotation), Is.LessThan(math.radians(30f)));
				Assert.That(KneeSwing(poses[right].Value.Rotation), Is.LessThan(math.radians(30f)));
				AssertHealthy();
			}
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Sprint = true });
			Advance(180);
			var settled = manager.GetComponentData<MmdPlayback>(_character);
			Assert.That(data.Value.Clips[settled.ClipIndex].Loop, Is.True, "An aligned sprint must return to its stride.");
			Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[settled.ClipIndex], control.SprintGroup), Is.True);
		}

		[TestCase(-1f, 170f, true)]
		[TestCase(1f, 170f, true)]
		[TestCase(1f, 180f, true)]
		[TestCase(-1f, 170f, false)]
		[TestCase(1f, 170f, false)]
		[TestCase(1f, 180f, false)]
		public void SharpMovingReversalSelectsTheAuthoredHalfTurn(float side, float angleDegrees, bool sprint) {
			CreateFlatCharacter();
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, Sprint = sprint });
			Advance(150);
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var catalogs = new ColInt.MMD.Editor.Diagnostics.MmdDebugCatalogStore();
			catalogs.Reload();
			var catalog = catalogs.Resolve(data);
			Assert.That(catalog, Is.Not.Null);
			float angle = side * math.radians(angleDegrees);
			var direction = new float2(math.sin(angle), math.cos(angle));
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = direction, Run = true, Sprint = sprint });
			bool halfTurn = false;
			var trace = new System.Collections.Generic.List<string>();
			float startX = manager.GetComponentData<LocalTransform>(_character).Position.x;
			float maximumSideways = 0f;
			for (int tick = 0; tick < 90; tick++) {
				_sample.Tick();
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				string name = catalog.Names[playback.ClipIndex];
				if (tick < 12) {
					halfTurn |= name.Contains((sprint ? "Sprint" : "Run") + (side < 0f ? "_Turn_L_180" : "_Turn_R_180"));
				}
				if (tick % 6 == 0) {
					var transform = manager.GetComponentData<LocalTransform>(_character);
					float3 forward = math.rotate(transform.Rotation, new float3(0f, 0f, 1f));
					trace.Add(name + " @ " + playback.Time.ToString("F3") + " x=" + (transform.Position.x - startX).ToString("F2") + " yaw=" + math.degrees(math.atan2(forward.x, forward.z)).ToString("F1"));
				}
				maximumSideways = math.max(maximumSideways, math.abs(manager.GetComponentData<LocalTransform>(_character).Position.x - startX));
				AssertHealthy();
			}
			Assert.That(halfTurn, Is.True, string.Join("; ", trace));
			Assert.That(maximumSideways, Is.LessThan(1.5f), "A sprint reversal must brake and pivot rather than trace a wide arc. " + string.Join("; ", trace));
			float2 facing = math.rotate(manager.GetComponentData<LocalTransform>(_character).Rotation, new float3(0f, 0f, 1f)).xz;
			Assert.That(math.dot(facing, direction), Is.GreaterThan(0.95f));
			Assert.That(data.Value.Clips[manager.GetComponentData<MmdPlayback>(_character).ClipIndex].Loop, Is.True, "Aligned movement must leave the reversal clip and resume its gait cycle.");
		}

		[TestCase(-90f, false, 0)]
		[TestCase(-90f, false, 60)]
		[TestCase(90f, false, 0)]
		[TestCase(90f, false, 60)]
		[TestCase(180f, false, 0)]
		[TestCase(180f, false, 60)]
		[TestCase(-90f, true, 0)]
		[TestCase(-90f, true, 60)]
		[TestCase(90f, true, 0)]
		[TestCase(90f, true, 60)]
		[TestCase(180f, true, 0)]
		[TestCase(180f, true, 60)]
		public void HeldMovementLeavesWallAfterTurningWithoutAnotherPress(float angleDegrees, bool sprint, int blockedTicks) {
			CreateFlatCharacter();
			AddBox(new float3(0f, 2f, 5f), new float3(80f, 4f, 1f), quaternion.identity);
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true, Sprint = sprint });
			var manager = _sample.World.EntityManager;
			for (int tick = 0; tick < 180; tick++) {
				_sample.Tick();
				if (manager.GetComponentData<LocalTransform>(_character).Position.z > 3.5f && math.length(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz) < 0.2f) {
					break;
				}
			}
			Advance(blockedTicks);
			float3 start = manager.GetComponentData<LocalTransform>(_character).Position;
			Assert.That(start.z, Is.InRange(3.5f, 4.5f), "The character must first reach the wall.");
			Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz), Is.LessThan(0.2f));
			float angle = math.radians(angleDegrees);
			var direction = new float2(math.sin(angle), math.cos(angle));
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = direction, Run = true, Sprint = sprint });
			var trace = new System.Collections.Generic.List<string>();
			for (int tick = 0; tick < 180; tick++) {
				_sample.Tick();
				if (tick % 12 == 0) {
					var transform = manager.GetComponentData<LocalTransform>(_character);
					var playback = manager.GetComponentData<MmdPlayback>(_character);
					var environment = manager.GetComponentData<MmdTrajectoryEnvironment>(_character);
					trace.Add(tick + " group=" + MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(_character), MmdMotionGroupUsage.Required) + " clip=" + playback.ClipIndex + " time=" + playback.Time.ToString("F3") + " yaw=" + math.degrees(math.atan2(transform.Forward().x, transform.Forward().z)).ToString("F1") + " fraction=" + environment.MovementFraction.ToString("F3"));
				}
				if (tick == 89) {
					float2 forward = manager.GetComponentData<LocalTransform>(_character).Forward().xz;
					Assert.That(math.dot(forward, direction), Is.GreaterThan(0.99f), "The wall turn must align within 1.5 seconds. " + string.Join("; ", trace));
				}
				AssertHealthy();
			}
			var final = manager.GetComponentData<LocalTransform>(_character);
			Assert.That(math.dot((final.Position - start).xz, direction), Is.GreaterThan(5f), string.Join("; ", trace));
			Assert.That(math.dot(final.Forward().xz, direction), Is.GreaterThan(0.99f), string.Join("; ", trace));
			Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(_character).Linear.xz), Is.GreaterThan(_configuration.Locomotion.RunSpeed * 0.8f), string.Join("; ", trace));
		}

		[TestCase(-1f)]
		[TestCase(1f)]
		public void BackwardSprintFromIdleStillUsesTurningStarts(float side) {
			CreateFlatCharacter();
			float angle = side * math.PI * 0.75f;
			_sample.World.EntityManager.SetComponentData(_character, new MmdSampleInput { Movement = new float2(math.sin(angle), math.cos(angle)), Sprint = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			var playback = manager.GetComponentData<MmdPlayback>(_character);
			Assert.That(MotionDatabaseGroups.Contains(ref data.Value, data.Value.Clips[playback.ClipIndex], control.StartGroup), Is.True);
			Advance(180);
			playback = manager.GetComponentData<MmdPlayback>(_character);
			Assert.That(data.Value.Clips[playback.ClipIndex].Loop, Is.True);
			AssertHealthy();
		}

		private int FindBone(ref MotionDatabase database, string path) {
			int hash = BonePathHash.Calculate(path);
			for (int bone = 0; bone < database.Parents.Length; bone++) {
				if (database.BoneNameHashes[bone] == hash) {
					return bone;
				}
			}
			Assert.Fail(path);
			return -1;
		}

		private void CreateFlatCharacter() {
			AddBox(new float3(0f, -0.5f, 0f), new float3(120f, 1f, 120f), quaternion.identity);
			_character = _sample.Characters.Create(_configuration, LocalTransform.Identity, true, false);
			Advance(30);
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

		private static float KneeSwing(quaternion rotation) {
			return 2f * math.asin(math.min(1f, math.length(rotation.value.xy)));
		}
	}
}
