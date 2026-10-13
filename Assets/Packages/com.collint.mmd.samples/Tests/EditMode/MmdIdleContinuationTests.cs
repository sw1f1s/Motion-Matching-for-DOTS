using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdIdleContinuationTests {
		[TestCase(false)]
		[TestCase(true)]
		public void UeStopFinishesIntoStableLoopingIdleAndMovementCanResume(bool run) {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(
				MmdSampleTestAssets.GetPath("Configurations/SampleCharacterConfiguration.asset"));
			Assert.That(configuration, Is.Not.Null);
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(100f, 1f, 100f), 0f)) {
				using (var sample = new MmdSampleWorld(1f / 60f)) {
					var manager = sample.World.EntityManager;
					AddFloor(manager, floor);
					var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
					for (int tick = 0; tick < 600; tick++) {
						sample.Tick();
					}
					Assert.That(manager.GetComponentData<MmdPlayback>(actor).Revision, Is.Zero);
					var moving = new MmdSampleInput { Movement = new float2(0f, 1f), Run = run };
					sample.World.EntityManager.SetComponentData(actor, moving);
					for (int tick = 0; tick < 120; tick++) {
						sample.Tick();
					}
					sample.World.EntityManager.SetComponentData(actor, default(MmdSampleInput));
					sample.Tick();
					var data = manager.GetComponentData<MmdDatabase>(actor);
					var stop = manager.GetComponentData<MmdPlayback>(actor);
					Assert.That(data.Value.Value.Clips[stop.ClipIndex].Loop, Is.False, "The stop transition must still play.");
					int leftFoot = FindBone(ref data.Value.Value, "root/pelvis/thigh_l/calf_l/foot_l");
					int rightFoot = FindBone(ref data.Value.Value, "root/pelvis/thigh_r/calf_r/foot_r");
					float3 previousFootCenter = FootCenter(manager, actor, ref data.Value.Value, leftFoot, rightFoot);
					float3 idleFootCenter = float3.zero;
					bool enteredIdle = false;
					for (int tick = 0; tick < 300; tick++) {
						sample.Tick();
						var current = manager.GetComponentData<MmdPlayback>(actor);
						float3 center = FootCenter(manager, actor, ref data.Value.Value, leftFoot, rightFoot);
						if (!enteredIdle && data.Value.Value.Clips[current.ClipIndex].Loop) {
							enteredIdle = true;
							idleFootCenter = previousFootCenter;
							Assert.That(stop.Time + tick * sample.Timestep, Is.LessThan(data.Value.Value.Clips[stop.ClipIndex].Duration - 0.3f),
								"A settled stop must enter idle before its long stationary tail finishes.");
						}
						if (enteredIdle) {
							Assert.That(math.distance(center.xz, idleFootCenter.xz), Is.LessThan(0.01f),
								"Changing to idle must not translate the planted footprint.");
						}
						previousFootCenter = center;
					}
					var idle = manager.GetComponentData<MmdPlayback>(actor);
					Assert.That(data.Value.Value.Clips[idle.ClipIndex].Loop, Is.True);
					float duration = data.Value.Value.Clips[idle.ClipIndex].Duration;
					float3 position = manager.GetComponentData<LocalTransform>(actor).Position;
					quaternion rotation = manager.GetComponentData<LocalTransform>(actor).Rotation;
					int wraps = 0;
					float previousTime = idle.Time;
					for (int tick = 0; tick < (int)math.ceil(duration * 120f); tick++) {
						sample.Tick();
						var playback = manager.GetComponentData<MmdPlayback>(actor);
						Assert.That(playback.ClipIndex, Is.EqualTo(idle.ClipIndex));
						Assert.That(playback.Revision, Is.EqualTo(idle.Revision), "Idle must not be reselected or rewound.");
						float elapsed = playback.Time - previousTime;
						if (elapsed < 0f) {
							elapsed += duration;
							wraps++;
						}
						Assert.That(elapsed, Is.EqualTo(1f / 60f).Within(0.00001f));
						Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
						previousTime = playback.Time;
					}
					Assert.That(wraps, Is.GreaterThanOrEqualTo(2));
					var transform = manager.GetComponentData<LocalTransform>(actor);
					Assert.That(math.distance(transform.Position.xz, position.xz), Is.LessThan(0.001f));
					Assert.That(math.abs(math.dot(transform.Rotation.value, rotation.value)), Is.GreaterThan(0.99999f));
					sample.World.EntityManager.SetComponentData(actor, moving);
					for (int tick = 0; tick < 120; tick++) {
						sample.Tick();
					}
					Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz), Is.GreaterThan(1f));
				}
			}
		}

		private static int FindBone(ref MotionDatabase database, string path) {
			int hash = BonePathHash.Calculate(path);
			for (int bone = 0; bone < database.BoneNameHashes.Length; bone++) {
				if (database.BoneNameHashes[bone] == hash) {
					return bone;
				}
			}
			Assert.Fail($"Missing sample bone: {path}");
			return -1;
		}

		private static float3 FootCenter(EntityManager manager, Entity actor, ref MotionDatabase database, int left, int right) {
			var poses = manager.GetBuffer<MmdLocalPose>(actor);
			float3 center = float3.zero;
			foreach (int foot in new[] { left, right }) {
				float3 point = float3.zero;
				for (int bone = foot; bone >= 0; bone = database.Parents[bone]) {
					var pose = poses[bone].Value;
					point = math.transform(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), point);
				}
				center += point * 0.5f;
			}
			var transform = manager.GetComponentData<LocalTransform>(actor);
			return transform.Position + math.rotate(transform.Rotation, center * transform.Scale);
		}

		private static void AddFloor(EntityManager manager, BlobAssetReference<Collider> collider) {
			var entity = manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider));
			manager.SetComponentData(entity, LocalTransform.Identity);
			manager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
			manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
			manager.AddSharedComponent(entity, new PhysicsWorldIndex());
		}
	}
}
