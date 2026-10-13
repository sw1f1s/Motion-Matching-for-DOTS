using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Physics.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Physics.Helpers;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdGroundMovementTests {
		[Test]
		public void CapsuleStopsBeforeWallAndSlidesAlongIt() {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
					using (var wall = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 1.5f, 1.1f), new float3(20f, 3f, 0.2f), 0f)) {
						var world = BuildWorld(Body(floor, 1), Body(wall, 2));
						try {
							float3 straight = Move(world.CollisionWorld, capsule, float3.zero, new float3(0f, 0f, 2f));
							Assert.That(straight.z, Is.InRange(0.65f, 0.7f));
							float3 sliding = Move(world.CollisionWorld, capsule, float3.zero, new float3(2f, 0f, 2f));
							Assert.That(sliding.z, Is.LessThanOrEqualTo(0.7f));
							Assert.That(sliding.x, Is.GreaterThan(1.8f));
						} finally {
							world.Dispose();
						}
					}
				}
			}
		}

		[TestCase(0.2f, true)]
		[TestCase(0.6f, false)]
		public void SmallStepsAreReachableButTallObstaclesRemainBlocked(float height, bool reachable) {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
					using (var step = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, height * 0.5f, 2f), new float3(4f, height, 2f), 0f)) {
						var world = BuildWorld(Body(floor, 1), Body(step, 2));
						try {
							float3 position = float3.zero;
							for (int i = 0; i < 40; i++) {
								position = Move(world.CollisionWorld, capsule, position, new float3(0f, 0f, 0.05f));
							}
							if (reachable) {
								Assert.That(position.z, Is.GreaterThan(1.9f));
								Assert.That(position.y, Is.EqualTo(height + MmdGroundMovement.Default.Skin).Within(0.01f));
							} else {
								Assert.That(position.z, Is.LessThan(0.75f));
							}
						} finally {
							world.Dispose();
						}
					}
				}
			}
		}

		[TestCase(20f, true)]
		[TestCase(60f, false)]
		public void GroundProbeFollowsWalkableSlopeAndRejectsSteepSurface(float angle, bool walkable) {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
					var body = Body(floor, 1);
					body.WorldFromBody.rot = quaternion.RotateX(math.radians(-angle));
					var world = BuildWorld(body);
					try {
						bool supported = MmdGroundMovementQuery.Ground(world.CollisionWorld, Entity.Null, new PhysicsCollider { Value = capsule },
							LocalTransform.Identity, MmdGroundMovement.Default, float3.zero, out var ground);
						Assert.That(supported, Is.EqualTo(walkable));
						if (walkable) {
							float3 uphill = Move(world.CollisionWorld, capsule, ground, new float3(0f, 0f, 0.2f));
							Assert.That(uphill.y - ground.y, Is.EqualTo(math.tan(math.radians(angle)) * 0.2f).Within(0.005f));
						}
					} finally {
						world.Dispose();
					}
				}
			}
		}

		[Test]
		public void CapsuleClimbsFromFlatGroundOntoRaisedRamp() {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 30f), 0f)) {
					using (var ramp = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, new float3(2.5f, 0.3f, 4f), 0f)) {
						var rampBody = Body(ramp, 2);
						rampBody.WorldFromBody = new RigidTransform(quaternion.RotateX(math.radians(-15f)), new float3(3f, 0.55f, 13f));
						var world = BuildWorld(Body(floor, 1), rampBody);
						try {
							float3 position = new float3(3f, 0f, 10.5f);
							for (int i = 0; i < 80; i++) {
								position = Move(world.CollisionWorld, capsule, position, new float3(0f, 0f, 0.05f));
							}
							Assert.That(position.z, Is.GreaterThan(14.4f));
							Assert.That(position.y, Is.GreaterThan(1.1f));
						} finally {
							world.Dispose();
						}
					}
				}
			}
		}

		[Test]
		public void GroundLocomotionContinuesAcrossUnsupportedDropWithoutClaimingSupport() {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, -2f), new float3(10f, 1f, 4f), 0f)) {
					var world = BuildWorld(Body(floor, 1));
					try {
						float3 start = new float3(0f, 0.015f, -0.5f);
						float3 target = MmdGroundMovementQuery.Move(world.CollisionWorld, Entity.Null, new PhysicsCollider { Value = capsule },
							LocalTransform.Identity, MmdGroundMovement.Default, start, new float3(0f, 0f, 1.5f), out bool supported);
						Assert.That(target.z, Is.EqualTo(1f).Within(0.001f));
						Assert.That(target.y, Is.EqualTo(start.y).Within(0.001f));
						Assert.That(supported, Is.False);
					} finally {
						world.Dispose();
					}
				}
			}
		}

		[Test]
		public void SlidingAlongWallCanLeaveGroundAndDoesNotClaimSupport() {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, -2f), new float3(10f, 1f, 4f), 0f)) {
					using (var wall = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(1f, 1.5f, 0f), new float3(0.2f, 3f, 10f), 0f)) {
						var world = BuildWorld(Body(floor, 1), Body(wall, 2));
						try {
							float3 start = new float3(0f, 0.015f, -0.5f);
							float3 target = MmdGroundMovementQuery.Move(world.CollisionWorld, Entity.Null, new PhysicsCollider { Value = capsule },
								LocalTransform.Identity, MmdGroundMovement.Default, start, new float3(2f, 0f, 1.5f), out bool supported);
							Assert.That(target.x, Is.InRange(0.55f, 0.61f));
							Assert.That(target.z, Is.GreaterThan(0.9f));
							Assert.That(supported, Is.False);
						} finally {
							world.Dispose();
						}
					}
				}
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CharacterPredictsPastEdgeFallsUnderGravityAndLandsOnLowerGround(bool realDatabase) {
			using (var platform = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, -2f), new float3(10f, 1f, 4f), 0f)) {
				using (var lowerFloor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -2.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
					using (var sample = new MmdSampleWorld(1f / 60f)) {
						var manager = sample.World.EntityManager;
						AddStatic(manager, platform);
						AddStatic(manager, lowerFloor);
						var spawn = LocalTransform.FromPosition(new float3(0f, 0.015f, -0.6f));
						Entity actor;
						if (realDatabase) {
							var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<ColInt.MMD.Samples.Character.Configurations.MmdSampleCharacterConfiguration>(
								"Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset");
							Assert.That(configuration, Is.Not.Null);
							actor = sample.Characters.Create(configuration, spawn, true, false);
						} else {
							actor = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, spawn);
							MmdSampleCharacterPhysicsBuilder.Add(manager, actor);
							manager.AddComponent<MmdSampleMovement>(actor);
							manager.AddComponentData(actor, new MmdSampleCharacterControl { WalkSpeed = 1f, TurnSpeed = 4.5f, IdleGroup = 1, WalkGroup = 2 });
						}
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f) });
						bool predictedPastEdge = false;
						bool fellUnderGravity = false;
						for (int i = 0; i < 180; i++) {
							sample.Tick();
							float3 position = manager.GetComponentData<LocalTransform>(actor).Position;
							float3 velocity = manager.GetComponentData<PhysicsVelocity>(actor).Linear;
							var points = manager.GetBuffer<MmdTrajectoryPoint>(actor);
							predictedPastEdge |= position.z < 0.1f && points[2].Position.z > 0.35f;
							fellUnderGravity |= position.z > 0.3f && position.y < -0.1f && velocity.y < -0.5f;
							Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
						}
						Assert.That(predictedPastEdge, Is.True, "Prediction must allow leaving the platform.");
						Assert.That(fellUnderGravity, Is.True, "Root motion must preserve gravity while unsupported.");
						float3 landed = manager.GetComponentData<LocalTransform>(actor).Position;
						Assert.That(landed.z, Is.GreaterThan(1f));
						Assert.That(landed.y, Is.EqualTo(-2f + MmdGroundMovement.Default.Skin).Within(0.08f));
						Assert.That(math.abs(manager.GetComponentData<PhysicsVelocity>(actor).Linear.y), Is.LessThan(0.3f));
					}
				}
			}
		}

		[Test]
		public void BlockedPredictionSelectsIdleAndActualVelocityStopsThenResumesAwayFromWall() {
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
				using (var wall = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 1.5f, 0.6f), new float3(20f, 3f, 0.2f), 0f)) {
					using (var sample = new MmdSampleWorld(1f / 60f)) {
						var manager = sample.World.EntityManager;
						AddStatic(manager, floor);
						AddStatic(manager, wall);
						var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
						MmdSampleCharacterPhysicsBuilder.Add(manager, actor);
						manager.AddComponent<MmdSampleMovement>(actor);
						manager.AddComponentData(actor, new MmdSampleCharacterControl { WalkSpeed = 1f, TurnSpeed = 4.5f, IdleGroup = 1, WalkGroup = 2 });
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f) });
						for (int i = 0; i < 180; i++) {
							sample.Tick();
						}
						float3 position = manager.GetComponentData<LocalTransform>(actor).Position;
						Assert.That(position.z, Is.LessThanOrEqualTo(0.21f));
						Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(1));
						Assert.That(math.length(manager.GetComponentData<MmdSampleTrajectoryState>(actor).Velocity.xz), Is.LessThan(0.05f));
						var points = manager.GetBuffer<MmdTrajectoryPoint>(actor);
						Assert.That(points[2].Position.z, Is.LessThanOrEqualTo(0.21f));
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, -1f) });
						for (int i = 0; i < 360; i++) {
							sample.Tick();
						}
						Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(2));
						Assert.That(manager.GetComponentData<LocalTransform>(actor).Position.z, Is.LessThan(position.z - 1f));
					}
				}
			}
		}

		[Test]
		public void RealUeDatabaseStopsAtWallWithoutSelectingPersistentRunningOrPivots() {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<ColInt.MMD.Samples.Character.Configurations.MmdSampleCharacterConfiguration>(
				"Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset");
			Assert.That(configuration, Is.Not.Null);
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
				using (var wall = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 1.5f, 0.6f), new float3(20f, 3f, 0.2f), 0f)) {
					using (var sample = new MmdSampleWorld(1f / 60f)) {
						var manager = sample.World.EntityManager;
						AddStatic(manager, floor);
						AddStatic(manager, wall);
						var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
						for (int i = 0; i < 240; i++) {
							sample.Tick();
						}
						float3 stopped = manager.GetComponentData<LocalTransform>(actor).Position;
						for (int i = 0; i < 120; i++) {
							sample.Tick();
							Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(1));
							Assert.That(math.distance(manager.GetComponentData<LocalTransform>(actor).Position, stopped), Is.LessThan(0.01f));
							Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
						}
						Assert.That(stopped.z, Is.LessThanOrEqualTo(0.21f));
						Assert.That(manager.GetBuffer<MmdTrajectoryPoint>(actor)[2].Position.z, Is.LessThanOrEqualTo(0.21f));
						Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz), Is.LessThan(0.01f));
					}
				}
			}
		}

		[Test]
		public void LowCeilingPreventsSteppingUp() {
			using (var capsule = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(new float3(0f, 0.9f, 0f), 0.3f, 1.8f, 0f)) {
				using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
					using (var step = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 0.1f, 2f), new float3(4f, 0.2f, 2f), 0f)) {
						using (var ceiling = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, 2.05f, 2f), new float3(4f, 0.3f, 6f), 0f)) {
							var world = BuildWorld(Body(floor, 1), Body(step, 2), Body(ceiling, 3));
							try {
								float3 position = float3.zero;
								for (int i = 0; i < 40; i++) {
									position = Move(world.CollisionWorld, capsule, position, new float3(0f, 0f, 0.05f));
								}
								Assert.That(position.z, Is.LessThan(0.8f));
							} finally {
								world.Dispose();
							}
						}
					}
				}
			}
		}

		[Test]
		public void PhysicsPredictionPublishesSurfaceHeightInWorldTrajectory() {
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(20f, 1f, 20f), 0f)) {
				using (var sample = new MmdSampleWorld(1f / 60f)) {
					var manager = sample.World.EntityManager;
					var slope = AddStatic(manager, floor);
					manager.SetComponentData(slope, LocalTransform.FromRotation(quaternion.RotateX(math.radians(-20f))));
					var actor = sample.Characters.Create(default, false, LocalTransform.Identity);
					MmdSampleCharacterPhysicsBuilder.Add(manager, actor);
					manager.AddComponent<MmdSampleMovement>(actor);
					manager.AddComponentData(actor, new MmdActionRequest { PreferredClip = -1 });
					manager.AddComponentData(actor, new MmdSampleCharacterControl { WalkSpeed = 1f, TurnSpeed = 4.5f });
					sample.Tick();
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f) });
					sample.Tick();
					var points = manager.GetBuffer<MmdTrajectoryPoint>(actor);
					Assert.That(points.Length, Is.EqualTo(3));
					float3 difference = points[2].Position - points[0].Position;
					Assert.That(difference.z, Is.GreaterThan(0.1f));
					Assert.That(difference.y, Is.EqualTo(difference.z * math.tan(math.radians(20f))).Within(0.01f));
				}
			}
		}

		[TestCase(0f, 1f, 0f)]
		[TestCase(0f, -1f, 0f)]
		[TestCase(0f, -1f, 5f)]
		[TestCase(1f, 0f, 5f)]
		public void FreeAccelerationAndTurnBrakingDoNotImposeCollisionTempo(float x, float z, float initialSpeed) {
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(40f, 1f, 40f), 0f)) {
				using (var sample = new MmdSampleWorld(1f / 60f)) {
					var manager = sample.World.EntityManager;
					AddStatic(manager, floor);
					var actor = sample.Characters.Create(default, false, LocalTransform.Identity);
					MmdSampleCharacterPhysicsBuilder.Add(manager, actor);
					manager.AddComponent<MmdSampleMovement>(actor);
					manager.AddComponentData(actor, new MmdActionRequest { PreferredClip = -1 });
					manager.AddComponentData(actor, new MmdSampleCharacterControl {
						WalkSpeed = 2f, RunSpeed = 5f, TurnSpeed = 4.5f, IdleGroup = 1, WalkGroup = 2, RunGroup = 4
					});
					manager.AddComponent<MmdSearchSettings>(actor);
					manager.AddComponentData(actor, new MmdPlayback { Speed = 1f });
					sample.Tick();
					manager.SetComponentData(actor, new PhysicsVelocity { Linear = new float3(0f, 0f, initialSpeed) });
					manager.SetComponentData(actor, new MmdTrajectoryEnvironment { MaximumSpeed = 0f });
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(x, z), Run = true });
					sample.Tick();
					Assert.That(manager.GetComponentData<MmdTrajectoryEnvironment>(actor).MaximumSpeed, Is.EqualTo(float.PositiveInfinity));
					Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(4));
					Assert.That(manager.GetComponentData<MmdPlayback>(actor).Speed, Is.EqualTo(1f));
					Assert.That(math.distance(manager.GetBuffer<MmdTrajectoryPoint>(actor)[2].Position,
						manager.GetComponentData<MmdSearchQuerySpace>(actor).Position), Is.GreaterThan(0.1f));
				}
			}
		}

		[TestCase(0f, -1f)]
		[TestCase(1f, 0f)]
		[TestCase(-1f, 0f)]
		public void RealUeRunTurnsAtAuthoredTempoWithoutGaitOscillation(float x, float z) {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<ColInt.MMD.Samples.Character.Configurations.MmdSampleCharacterConfiguration>(
				"Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset");
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(100f, 1f, 100f), 0f)) {
				using (var sample = new MmdSampleWorld(1f / 60f)) {
					var manager = sample.World.EntityManager;
					AddStatic(manager, floor);
					var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
					for (int i = 0; i < 180; i++) {
						sample.Tick();
					}
					var movement = new float2(x, z);
					float previousError = math.abs(ColInt.MMD.Samples.Character.Movement.Facing.Helpers.MmdSampleFacing.YawError(
						manager.GetComponentData<LocalTransform>(actor).Rotation, movement));
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = movement, Run = true });
					for (int i = 0; i < 180; i++) {
						sample.Tick();
						Assert.That(manager.GetComponentData<MmdTrajectoryEnvironment>(actor).MaximumSpeed, Is.EqualTo(float.PositiveInfinity));
						Assert.That(manager.GetComponentData<MmdPlayback>(actor).Speed, Is.EqualTo(1f));
						Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(4));
						Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
						float error = math.abs(ColInt.MMD.Samples.Character.Movement.Facing.Helpers.MmdSampleFacing.YawError(
							manager.GetComponentData<LocalTransform>(actor).Rotation, movement));
						Assert.That(error, Is.LessThanOrEqualTo(previousError + 0.0001f), "A held direction must not alternate turn directions.");
						previousError = error;
						if (i >= 89) {
							Assert.That(error, Is.LessThan(0.0872665f));
						}
					}
					float2 velocity = manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz;
					Assert.That(math.length(velocity), Is.GreaterThan(configuration.Locomotion.RunSpeed * 0.9f));
					Assert.That(math.dot(math.normalizesafe(velocity), movement), Is.GreaterThan(0.98f));
				}
			}
		}

		[Test]
		public void RealUeCameraOrbitKeepsRunningTempoAndFollowsRequestedDirection() {
			var configuration = UnityEditor.AssetDatabase.LoadAssetAtPath<ColInt.MMD.Samples.Character.Configurations.MmdSampleCharacterConfiguration>(
				"Assets/Packages/com.collint.mmd/Samples/Configurations/SampleCharacterConfiguration.asset");
			using (var floor = MmdSampleWorldPhysicsColliderFactory.CreateBox(new float3(0f, -0.5f, 0f), new float3(100f, 1f, 100f), 0f)) {
				using (var sample = new MmdSampleWorld(1f / 60f)) {
					var manager = sample.World.EntityManager;
					AddStatic(manager, floor);
					var actor = sample.Characters.Create(configuration, LocalTransform.Identity, true, false);
					sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = new float2(0f, 1f), Run = true });
					for (int i = 0; i < 180; i++) {
						sample.Tick();
					}
					for (int i = 0; i < 360; i++) {
						float angle = i / 60f * math.PI * 0.5f;
						var direction = new float2(math.sin(angle), math.cos(angle));
						sample.World.EntityManager.SetComponentData(actor, new MmdSampleInput { Movement = direction, Facing = direction, Run = true });
						sample.Tick();
						Assert.That(manager.GetComponentData<MmdPlayback>(actor).Speed, Is.EqualTo(1f));
						Assert.That(MmdMotionGroups.First(manager.GetBuffer<MmdMotionGroup>(actor), MmdMotionGroupUsage.Required), Is.EqualTo(4));
						Assert.That(manager.GetComponentData<MmdTrajectoryEnvironment>(actor).MaximumSpeed, Is.EqualTo(float.PositiveInfinity));
						float error = math.abs(ColInt.MMD.Samples.Character.Movement.Facing.Helpers.MmdSampleFacing.YawError(
							manager.GetComponentData<LocalTransform>(actor).Rotation, direction));
						Assert.That(error, Is.LessThan(0.6f));
						Assert.That(math.length(manager.GetComponentData<PhysicsVelocity>(actor).Linear.xz), Is.GreaterThan(3.5f));
						Assert.That(manager.GetComponentData<MmdFault>(actor).Code, Is.EqualTo(MmdFaultCode.None));
					}
				}
			}
		}

		[Test]
		public void CastCollectorsIgnoreOwnerAndTriggerContacts() {
			var owner = new Entity { Index = 2, Version = 1 };
			var collector = new MmdGroundCastCollector(owner, 0.7f, new float3(0f, 0f, 1f), false);
			var hit = new ColliderCastHit { Entity = owner, Fraction = 0.1f, SurfaceNormal = new float3(0f, 0f, -1f) };
			Assert.That(collector.AddHit(hit), Is.False);
			hit.Entity = new Entity { Index = 3, Version = 1 };
			hit.Material = new Material { CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents };
			Assert.That(collector.AddHit(hit), Is.False);
			hit.Material = Material.Default;
			Assert.That(collector.AddHit(hit), Is.True);
			Assert.That(collector.NumHits, Is.EqualTo(1));
		}

		private static float3 Move(CollisionWorld world, BlobAssetReference<Collider> capsule, float3 start, float3 displacement) {
			return MmdGroundMovementQuery.Move(world, Entity.Null, new PhysicsCollider { Value = capsule }, LocalTransform.Identity,
				MmdGroundMovement.Default, start, displacement, out _);
		}

		private static RigidBody Body(BlobAssetReference<Collider> collider, int index) {
			return new RigidBody { Collider = collider, WorldFromBody = RigidTransform.identity, Scale = 1f, Entity = new Entity { Index = index, Version = 1 } };
		}

		private static PhysicsWorld BuildWorld(params RigidBody[] bodies) {
			var world = new PhysicsWorld(bodies.Length, 0, 0);
			var staticBodies = world.StaticBodies;
			for (int i = 0; i < bodies.Length; i++) {
				staticBodies[i] = bodies[i];
			}
			world.CollisionWorld.BuildBroadphase(ref world, 1f / 60f, float3.zero);
			return world;
		}

		private static Entity AddStatic(EntityManager manager, BlobAssetReference<Collider> collider) {
			var entity = manager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider));
			manager.SetComponentData(entity, LocalTransform.Identity);
			manager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
			manager.SetComponentData(entity, new PhysicsCollider { Value = collider });
			manager.AddSharedComponent(entity, new PhysicsWorldIndex());
			return entity;
		}
	}
}
