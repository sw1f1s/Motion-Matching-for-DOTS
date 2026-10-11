using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Physics.Systems;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdPhysicsTests {
		[Test]
		public void PhysicsAdapterConsumesPlanarDeltaPreservesGravityAndDoesNotMoveTransform() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.AddComponent<MmdPhysicsRoot>(entity);
				manager.AddComponentData(entity, new PhysicsMass { Transform = RigidTransform.identity });
				manager.AddComponentData(entity, new PhysicsVelocity { Linear = new float3(0f, -2f, 0f) });
				LocomotionSchema.WriteTrajectory(manager.GetBuffer<MmdSearchQueryElement>(entity), new float3(0f, 0f, 1f), 0f);
				world.Tick();
				PhysicsVelocity velocity = manager.GetComponentData<PhysicsVelocity>(entity);
				Assert.That(velocity.Linear.z, Is.EqualTo(1f).Within(0.0001f));
				Assert.That(velocity.Linear.y, Is.EqualTo(-2f));
				Assert.That(manager.GetComponentData<LocalTransform>(entity).Position.z, Is.Zero);
				Assert.That(manager.GetComponentData<MmdRootMotion>(entity).Valid, Is.False);
			}
		}

		[TestCase(false, 0.1f)]
		[TestCase(true, 0f)]
		[TestCase(true, -0.1f)]
		public void RuntimeAdapterStopsOwnedBodyWithoutMotionAndPreservesVerticalVelocity(bool valid, float deltaTime) {
			using (var world = new World("Runtime physics owner fixture")) {
				var manager = world.EntityManager;
				Entity owner = CreateBody(manager);
				manager.AddComponent<MmdPhysicsRoot>(owner);
				manager.SetComponentData(owner, new MmdRootMotion { Valid = valid, DeltaTime = deltaTime });
				Entity other = CreateBody(manager);
				Entity kinematic = CreateBody(manager);
				manager.AddComponent<MmdPhysicsRoot>(kinematic);
				manager.AddComponent<MmdKinematicRoot>(kinematic);
				var original = manager.GetComponentData<PhysicsVelocity>(other);
				var system = world.GetOrCreateSystem<MmdPhysicsRootSystem>();
				system.Update(world.Unmanaged);
				manager.CompleteAllTrackedJobs();
				var velocity = manager.GetComponentData<PhysicsVelocity>(owner);
				Assert.That(velocity.Linear, Is.EqualTo(new float3(0f, -2f, 0f)));
				Assert.That(velocity.Angular, Is.EqualTo(float3.zero));
				Assert.That(manager.GetComponentData<MmdRootMotion>(owner).Valid, Is.False);
				Assert.That(manager.GetComponentData<LocalTransform>(owner).Position, Is.EqualTo(new float3(3f, 4f, 5f)));
				Assert.That(manager.GetComponentData<PhysicsVelocity>(other).Linear, Is.EqualTo(original.Linear));
				Assert.That(manager.GetComponentData<PhysicsVelocity>(other).Angular, Is.EqualTo(original.Angular));
				Assert.That(manager.GetComponentData<PhysicsVelocity>(kinematic).Linear, Is.EqualTo(original.Linear));
				Assert.That(manager.GetComponentData<PhysicsVelocity>(kinematic).Angular, Is.EqualTo(original.Angular));
			}
		}

		[Test]
		public void RuntimeAdapterDoesNotReuseConsumedMotionOnTheFollowingTick() {
			using (var world = new World("Root motion expiry fixture")) {
				var manager = world.EntityManager;
				Entity owner = CreateBody(manager);
				manager.AddComponent<MmdPhysicsRoot>(owner);
				manager.SetComponentData(owner, new MmdRootMotion {
					Valid = true,
					DeltaTime = 0.1f,
					Translation = new float3(0f, 0f, 0.4f),
					Rotation = quaternion.RotateY(0.1f)
				});
				var system = world.GetOrCreateSystem<MmdPhysicsRootSystem>();
				system.Update(world.Unmanaged);
				manager.CompleteAllTrackedJobs();
				Assert.That(manager.GetComponentData<PhysicsVelocity>(owner).Linear.z, Is.EqualTo(4f).Within(0.0001f));
				Assert.That(manager.GetComponentData<PhysicsVelocity>(owner).Angular.y, Is.GreaterThan(0.9f));
				Assert.That(manager.GetComponentData<MmdRootMotion>(owner).Valid, Is.False);
				system.Update(world.Unmanaged);
				manager.CompleteAllTrackedJobs();
				Assert.That(manager.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(new float3(0f, -2f, 0f)));
				Assert.That(manager.GetComponentData<PhysicsVelocity>(owner).Angular, Is.EqualTo(float3.zero));
			}
		}

		private static Entity CreateBody(EntityManager manager) {
			Entity entity = manager.CreateEntity(typeof(LocalTransform), typeof(PhysicsMass), typeof(PhysicsVelocity), typeof(MmdRootMotion));
			manager.SetComponentData(entity, LocalTransform.FromPosition(3f, 4f, 5f));
			manager.SetComponentData(entity, new PhysicsMass { Transform = RigidTransform.identity });
			manager.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(1f, -2f, 3f), Angular = new float3(0f, 2f, 0f) });
			return entity;
		}

	}

}
