using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdReversalTests {
		[TestCase(-1f, false, 180)]
		[TestCase(1f, false, 180)]
		[TestCase(-1f, true, 180)]
		[TestCase(1f, true, 180)]
		[TestCase(-1f, false, 25)]
		[TestCase(-1f, true, 25)]
		public void UeReversalKeepsOneTurnAndDoesNotRestartFootPhasesDuringBlend(float initialX, bool run, int leadTicks) {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(
				"Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset");
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(100f, 1f, 100f), 0f)) {
				using (var sample = new MmdSampleWorld(1f / 60f)) {
					var manager = sample.World.EntityManager;
					var ground = manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider));
					manager.SetComponentData(ground, LocalTransform.Identity);
					manager.SetComponentData(ground, new LocalToWorld { Value = float4x4.identity });
					manager.SetComponentData(ground, new PhysicsCollider { Value = floor });
					manager.AddSharedComponent(ground, new PhysicsWorldIndex());
					var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
					for (int tick = 0; tick < 120; tick++) {
						sample.Tick();
					}
					var database = manager.GetComponentData<ColInt.MMD.Database.Components.MmdDatabase>(actor).Value;
					int hips = -1;
					for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
						if (database.Value.BoneNameHashes[bone] == BonePathHash.Calculate("root/pelvis")) {
							hips = bone;
						}
					}
					Assert.That(hips, Is.GreaterThanOrEqualTo(0));
					var poses = manager.GetBuffer<MmdLocalPose>(actor);
					float3 bodyAxis = math.rotate(math.inverse(ModelRotation(ref database.Value, poses, hips)), new float3(0f, 0f, 1f));
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(initialX, 0f), Run = run });
					for (int tick = 0; tick < leadTicks; tick++) {
						sample.Tick();
					}
					var direction = new float2(-initialX, 0f);
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = direction, Run = run });
					float error = math.abs(MmdSampleFacing.YawError(manager.GetComponentData<LocalTransform>(actor).Rotation, direction));
					var initial = manager.GetComponentData<LocalTransform>(actor);
					float side = math.sign(MmdSampleFacing.YawError(initial.Rotation, direction,
						manager.GetComponentData<MmdSampleTrajectoryState>(actor).TurnError));
					poses = manager.GetBuffer<MmdLocalPose>(actor);
					float2 previousBody = math.normalizesafe(math.rotate(math.mul(initial.Rotation, ModelRotation(ref database.Value, poses, hips)), bodyAxis).xz);
					float bodyTurn = 0f;
					float bodyError = side * math.atan2(previousBody.y * direction.x - previousBody.x * direction.y, math.dot(previousBody, direction));
					float maximumBodyTurn = (bodyError < 0f ? bodyError + 2f * math.PI : bodyError) + math.radians(35f);
					int lastSwitch = -100;
					uint revision = manager.GetComponentData<MmdPlayback>(actor).Revision;
					int changesDuringTurn = 0;
					float turnDistance = 0f;
					float3 previousPosition = manager.GetComponentData<LocalTransform>(actor).Position;
					for (int tick = 0; tick < 240; tick++) {
						sample.Tick();
						var transform = manager.GetComponentData<LocalTransform>(actor);
						poses = manager.GetBuffer<MmdLocalPose>(actor);
						float2 body = math.normalizesafe(math.rotate(math.mul(transform.Rotation, ModelRotation(ref database.Value, poses, hips)), bodyAxis).xz);
						bodyTurn += side * math.atan2(previousBody.y * body.x - previousBody.x * body.y, math.dot(previousBody, body));
						previousBody = body;
						Assert.That(bodyTurn, Is.InRange(-math.radians(20f), maximumBodyTurn),
							"A moving reversal must not spin the displayed skeleton away or through a second turn.");
						float nextError = math.abs(MmdSampleFacing.YawError(transform.Rotation, direction));
						Assert.That(nextError, Is.LessThanOrEqualTo(error + 0.0001f), "A held reversal must not turn away again.");
						if (error > math.radians(5f)) {
							turnDistance += math.distance(previousPosition.xz, transform.Position.xz);
						}
						previousPosition = transform.Position;
						error = nextError;
						var playback = manager.GetComponentData<MmdPlayback>(actor);
						if (playback.Revision != revision) {
							Assert.That(tick - lastSwitch, Is.GreaterThanOrEqualTo(12), "Do not interrupt a foot-phase blend while input stays held.");
							lastSwitch = tick;
							revision = playback.Revision;
							if (tick < 44) {
								changesDuringTurn++;
							}
						}
						Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
						if (tick >= 44) {
							Assert.That(error, Is.LessThan(math.radians(1f)), "Turn speed must agree with trajectory prediction.");
						}
						if (tick >= 90) {
							var velocity = manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz;
							Assert.That(math.length(velocity), Is.GreaterThan((run ? configuration.Locomotion.RunSpeed : configuration.Locomotion.WalkSpeed) * 0.85f));
							Assert.That(math.dot(math.normalizesafe(velocity), direction), Is.GreaterThan(0.98f));
						}
					}
					Assert.That(changesDuringTurn, Is.LessThanOrEqualTo(4));
					Assert.That(turnDistance, Is.LessThan(run ? 3.5f : 1.5f), "A reversal must not take a large circular detour.");
				}
			}
		}

		private static quaternion ModelRotation(ref MotionDatabase database, in DynamicBuffer<MmdLocalPose> poses, int bone) {
			quaternion rotation = quaternion.identity;
			for (int current = bone; current >= 0; current = database.Parents[current]) {
				rotation = math.mul(poses[current].Value.Rotation, rotation);
			}
			return rotation;
		}
	}
}
