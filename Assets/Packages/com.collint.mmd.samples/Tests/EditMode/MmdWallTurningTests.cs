using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdWallTurningTests {
		[TestCase(1f, 0f, false)]
		[TestCase(-1f, 0f, false)]
		[TestCase(0f, -1f, false)]
		[TestCase(1f, 0f, true)]
		[TestCase(-1f, 0f, true)]
		[TestCase(0f, -1f, true)]
		public void UeCharacterTurnsAtWallWithoutSlowMotionOrGaitOscillationAndResumes(float x, float z, bool run) {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(
				MmdSampleTestAssets.GetPath("Configurations/SampleCharacterConfiguration.asset"));
			Assert.That(configuration, Is.Not.Null);
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(100f, 1f, 100f), 0f)) {
				using (var wall = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 1.5f, 3f), new float3(80f, 3f, 0.5f), 0f)) {
					using (var sample = new MmdSampleWorld(1f / 60f)) {
						var manager = sample.World.EntityManager;
						AddStatic(manager, floor);
						AddStatic(manager, wall);
						var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f), Run = run });
						for (int tick = 0; tick < 480; tick++) {
							sample.Tick();
						}
						var control = manager.GetComponentData<MmdSampleCharacterControl>(actor);
						Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(control.IdleGroup));
						var movement = new float2(x, z);
						float error = math.abs(MmdSampleFacing.YawError(manager.GetComponentData<LocalTransform>(actor).Rotation, movement));
						int previousGroup = control.IdleGroup;
						int changes = 0;
						int turnTicks = 0;
						bool movedWhileTurning = false;
						float3 initialPosition = manager.GetComponentData<LocalTransform>(actor).Position;
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = movement, Run = run });
						for (int tick = 0; tick < 240; tick++) {
							sample.Tick();
							var settings = manager.GetComponentData<MmdSearchSettings>(actor);
							var playback = manager.GetComponentData<MmdPlayback>(actor);
							int gait = MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required, control.StartGroup);
							if (gait != previousGroup) {
								changes++;
								previousGroup = gait;
							}
							if (MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required) == control.TurnGroup) {
								turnTicks++;
								Assert.That(playback.Speed, Is.EqualTo(1f), "Wall contact must not slow a turn in place.");
							}
							var transform = manager.GetComponentData<LocalTransform>(actor);
							float nextError = math.abs(MmdSampleFacing.YawError(transform.Rotation, movement));
							Assert.That(nextError, Is.LessThanOrEqualTo(error + 0.0001f), "A held direction must not reverse the turn.");
							movedWhileTurning |= math.distance(initialPosition.xz, transform.Position.xz) > 0.03f && nextError > math.radians(20f);
							error = nextError;
							Assert.That(transform.Position.z, Is.LessThan(2.46f), "Turning must not pass through the wall.");
							Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
							if (tick >= 89) {
								Assert.That(error, Is.LessThan(math.radians(5f)));
							}
						}
						if (z < 0f) {
							Assert.That(movedWhileTurning, Is.True, "An unobstructed escape from the wall uses a step-and-turn start.");
						} else {
							Assert.That(turnTicks, Is.GreaterThan(0));
						}
						Assert.That(changes, Is.LessThanOrEqualTo(3));
						Assert.That(previousGroup, Is.EqualTo(run ? control.RunGroup : control.WalkGroup));
						float2 velocity = manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz;
						Assert.That(math.length(velocity), Is.GreaterThan((run ? configuration.Locomotion.RunSpeed : configuration.Locomotion.WalkSpeed) * 0.9f));
						Assert.That(math.dot(math.normalizesafe(velocity), movement), Is.GreaterThan(0.98f));
					}
				}
			}
		}

		[Test]
		public void UeCameraOrbitAtWallKeepsStableGaitAndSlidesWithoutRepeatedBoundarySwitches() {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(
				MmdSampleTestAssets.GetPath("Configurations/SampleCharacterConfiguration.asset"));
			Assert.That(configuration, Is.Not.Null);
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(100f, 1f, 100f), 0f)) {
				using (var wall = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 1.5f, 3f), new float3(80f, 3f, 0.5f), 0f)) {
					using (var sample = new MmdSampleWorld(1f / 60f)) {
						var manager = sample.World.EntityManager;
						AddStatic(manager, floor);
						AddStatic(manager, wall);
						var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
						for (int tick = 0; tick < 480; tick++) {
							sample.Tick();
						}
						var control = manager.GetComponentData<MmdSampleCharacterControl>(actor);
						int previousGroup = MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required, control.StartGroup);
						int changes = 0;
						for (int tick = 0; tick < 360; tick++) {
							float angle = math.radians(30f) * math.min(tick / 180f, 1f);
							var direction = new float2(math.sin(angle), math.cos(angle));
							sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = direction, Facing = direction, Run = true });
							sample.Tick();
							int group = MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required, control.StartGroup);
							if (group != previousGroup) {
								changes++;
								previousGroup = group;
							}
							var transform = manager.GetComponentData<LocalTransform>(actor);
							Assert.That(math.abs(MmdSampleFacing.YawError(transform.Rotation, direction)), Is.LessThan(math.radians(5f)));
							Assert.That(transform.Position.z, Is.LessThan(2.46f));
							Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
							if (tick >= 300) {
								Assert.That(manager.GetComponentData<MmdPlayback>(actor).Speed, Is.EqualTo(0.5f).Within(0.02f),
									"Steady sliding tempo must reflect the wall's 50% projection, without repeated braking slowdown.");
							}
						}
						Assert.That(changes, Is.LessThanOrEqualTo(2));
						var velocity = manager.GetComponentData<PhysicsVelocity>(actor).Linear;
						Assert.That(velocity.x, Is.GreaterThan(configuration.Locomotion.RunSpeed * 0.45f));
						Assert.That(math.abs(velocity.z), Is.LessThan(0.01f));
					}
				}
			}
		}

		private static void AddStatic(EntityManager manager, BlobAssetReference<Collider> collider) {
			var entity = manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider));
			manager.SetComponentData(entity, LocalTransform.Identity);
			manager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
			manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
			manager.AddSharedComponent(entity, new PhysicsWorldIndex());
		}
	}
}
