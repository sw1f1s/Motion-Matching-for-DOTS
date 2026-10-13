using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
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
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Sprint = true });
			Advance(150);
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			int left = FindBone(ref data.Value, "root/pelvis/thigh_l/calf_l");
			int right = FindBone(ref data.Value, "root/pelvis/thigh_r/calf_r");
			for (int tick = 0; tick < 360; tick++) {
				float angle = orbit ? tick * math.PI / interval : (tick / interval) % 2 == 0 ? math.PI * 0.75f : -math.PI * 0.75f;
				var direction = new float2(math.sin(angle), math.cos(angle));
				_sample.SetInput(new MmdSampleInput { Run = true, Sprint = true, Movement = direction });
				_sample.Tick();
				var playback = manager.GetComponentData<MmdPlayback>(_character);
				Assert.That(data.Value.Clips[playback.ClipIndex].Tags & control.StartTags, Is.Zero, "Moving sprint turns must not splice stationary reface starts.");
				var poses = manager.GetBuffer<MmdLocalPose>(_character);
				// UEFN knees flex around local Z. Authored sprint pivots contain up to
				// 21 degrees of lateral swing; retain headroom for continuous blending.
				Assert.That(KneeSwing(poses[left].Value.Rotation), Is.LessThan(math.radians(30f)));
				Assert.That(KneeSwing(poses[right].Value.Rotation), Is.LessThan(math.radians(30f)));
				AssertHealthy();
			}
			_sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f), Sprint = true });
			Advance(180);
			var settled = manager.GetComponentData<MmdPlayback>(_character);
			Assert.That(data.Value.Clips[settled.ClipIndex].Loop, Is.True, "An aligned sprint must return to its stride.");
			Assert.That(data.Value.Clips[settled.ClipIndex].Tags & control.SprintTags, Is.EqualTo(control.SprintTags));
		}

		[TestCase(-1f)]
		[TestCase(1f)]
		public void BackwardSprintFromIdleStillUsesTurningStarts(float side) {
			CreateFlatCharacter();
			float angle = side * math.PI * 0.75f;
			_sample.SetInput(new MmdSampleInput { Movement = new float2(math.sin(angle), math.cos(angle)), Sprint = true });
			_sample.Tick();
			var manager = _sample.World.EntityManager;
			var data = manager.GetComponentData<MmdDatabase>(_character).Value;
			var control = manager.GetComponentData<MmdSampleCharacterControl>(_character);
			var playback = manager.GetComponentData<MmdPlayback>(_character);
			Assert.That(data.Value.Clips[playback.ClipIndex].Tags & control.StartTags, Is.EqualTo(control.StartTags));
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
