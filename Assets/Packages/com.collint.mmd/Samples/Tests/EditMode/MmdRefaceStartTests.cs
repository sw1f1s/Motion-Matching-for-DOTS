using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
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
	public class MmdRefaceStartTests {
		[Test]
		public void SampleRefaceStartsShareTheIdleModelFacingBasis() {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<MmdSampleCharacterConfiguration>(
				"Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset");
			using (var database = MotionDatabaseCodec.Load(configuration.Assets.Database.GetData<byte>())) {
				var catalogs = new ColInt.MMD.Editor.Diagnostics.MmdDebugCatalogStore();
				catalogs.Reload();
				var catalog = catalogs.Resolve(database);
				int root = -1;
				for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
					if (database.Value.BoneNameHashes[bone] == BonePathHash.Calculate("root/pelvis")) {
						root = bone;
					}
				}
				Assert.That(root, Is.GreaterThanOrEqualTo(0));
				quaternion reference = SampledModelRotation(ref database.Value, 0, root);
				float3 referenceForward = math.rotate(math.inverse(reference), new float3(0f, 0f, 1f));
				int checkedClips = 0;
				for (int clip = 0; clip < database.Value.Clips.Length; clip++) {
					if (!catalog.Names[clip].Contains("Reface_Start") || catalog.Names[clip].Contains("_Crouch_")) {
						continue;
					}
					quaternion start = SampledModelRotation(ref database.Value, clip, root);
					float3 forward = math.rotate(start, referenceForward);
					Assert.That(math.abs(math.degrees(math.atan2(forward.x, forward.z))), Is.LessThan(0.5f), catalog.Names[clip]);
					checkedClips++;
				}
				Assert.That(checkedClips, Is.EqualTo(18));
			}
		}

		[TestCase(-90f, false)]
		[TestCase(90f, false)]
		[TestCase(-135f, false)]
		[TestCase(135f, false)]
		[TestCase(180f, false)]
		[TestCase(-180f, false)]
		[TestCase(-90f, true)]
		[TestCase(90f, true)]
		[TestCase(-135f, true)]
		[TestCase(135f, true)]
		[TestCase(180f, true)]
		[TestCase(-180f, true)]
		public void IdleStartUsesRefaceAndMovesBeforeCompletingTurn(float degrees, bool run) {
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
					var catalogs = new ColInt.MMD.Editor.Diagnostics.MmdDebugCatalogStore();
					catalogs.Reload();
					var catalog = catalogs.Resolve(database);
					int hips = -1;
					for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
						if (database.Value.BoneNameHashes[bone] == BonePathHash.Calculate("root/pelvis")) {
							hips = bone;
						}
					}
					Assert.That(hips, Is.GreaterThanOrEqualTo(0));
					var poses = manager.GetBuffer<MmdLocalPose>(actor);
					float3 bodyAxis = math.rotate(math.inverse(ModelRotation(ref database.Value, poses, hips)), new float3(0f, 0f, 1f));
					float angle = math.radians(degrees);
					var direction = new float2(math.sin(angle), math.cos(angle));
					var initial = manager.GetComponentData<LocalTransform>(actor);
					float side = math.sign(MmdSampleFacing.YawError(initial.Rotation, direction));
					float2 previousBody = new float2(0f, 1f);
					float bodyTurn = 0f;
					sample.SetInput(new MmdSampleInput { Movement = direction, Run = run });
					bool selectedReface = false;
					bool movedWhileTurning = false;
					float3 start = manager.GetComponentData<LocalTransform>(actor).Position;
					for (int tick = 0; tick < 180; tick++) {
						sample.Tick();
						var playback = manager.GetComponentData<MmdPlayback>(actor);
						var transform = manager.GetComponentData<LocalTransform>(actor);
						float error = math.abs(MmdSampleFacing.YawError(transform.Rotation, direction));
						poses = manager.GetBuffer<MmdLocalPose>(actor);
						float3 forward = math.rotate(math.mul(transform.Rotation, ModelRotation(ref database.Value, poses, hips)), bodyAxis);
						float2 body = math.normalizesafe(forward.xz);
						bodyTurn += side * math.atan2(previousBody.y * body.x - previousBody.x * body.y, math.dot(previousBody, body));
						previousBody = body;
						Assert.That(bodyTurn, Is.InRange(-math.radians(20f), math.abs(angle) + math.radians(35f)),
							"The displayed skeleton must turn to the same side as the actor without an extra revolution.");
						if (tick < 30) {
							selectedReface |= catalog.Names[playback.ClipIndex].Contains("Reface_Start");
						}
						movedWhileTurning |= math.distance(start.xz, transform.Position.xz) > 0.03f && error > math.radians(20f);
						Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
					}
					Assert.That(selectedReface, Is.True, "A start from idle must find the authored step-and-turn clip.");
					Assert.That(movedWhileTurning, Is.True, "The character must step before completing the turn.");
					var final = manager.GetComponentData<LocalTransform>(actor);
					Assert.That(math.abs(MmdSampleFacing.YawError(final.Rotation, direction)), Is.LessThan(math.radians(1f)));
					var velocity = manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz;
					Assert.That(math.length(velocity), Is.GreaterThan((run ? configuration.Locomotion.RunSpeed : configuration.Locomotion.WalkSpeed) * 0.85f));
					Assert.That(math.dot(math.normalizesafe(velocity), direction), Is.GreaterThan(0.98f));
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

		private static quaternion SampledModelRotation(ref MotionDatabase database, int clip, int bone) {
			quaternion rotation = quaternion.identity;
			for (int current = bone; current >= 0; current = database.Parents[current]) {
				rotation = math.mul(MmdAnimationMath.PoseAt(ref database, clip, 0f, current).Rotation, rotation);
			}
			return rotation;
		}
	}
}
