using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Tests {
	public class MmdSampleParallelMovementTests {
		[TestCase(500)]
		[TestCase(1000)]
		public void ParallelMovementConsumesEachJumpOnceAndKeepsIndividualAirControlPauseAndDisabled(int count) {
			using (var sample = new MmdSampleWorld(0.1f)) {
				var manager = sample.World.EntityManager;
				var entities = new Entity[count];
				for (int index = 0; index < count; index++) {
					Entity entity = manager.CreateEntity(typeof(PhysicsVelocity), typeof(MmdSampleMovement), typeof(MmdSampleGroundState),
						typeof(MmdSampleMovementSettings), typeof(MmdSampleCharacterControl), typeof(MmdSampleInput), typeof(MmdSampleJumpImpulse));
					entities[index] = entity;
					manager.SetComponentData(entity, new MmdSampleMovement { AirControl = true, IgnoreGround = true });
					manager.SetComponentData(entity, new MmdSampleMovementSettings {
						JumpHeight = 2f, AirAcceleration = 6f, MaximumFallSpeed = 55f, WalkSpeed = 2f, RunSpeed = 4f
					});
					manager.SetComponentData(entity, new MmdSampleInput { Movement = index % 2 == 0 ? new float2(1f, 0f) : new float2(-1f, 0f), Run = true });
				}
				manager.AddComponent<Disabled>(entities[0]);
				sample.Tick();
				float impulse = math.sqrt(4f * math.abs(PhysicsStep.Default.Gravity.y));
				for (int index = 1; index < count; index++) {
					Entity entity = entities[index];
					Assert.That(manager.HasComponent<MmdSampleJumpImpulse>(entity), Is.False);
					var velocity = manager.GetComponentData<PhysicsVelocity>(entity).Linear;
					Assert.That(velocity.x, Is.EqualTo(index % 2 == 0 ? 0.6f : -0.6f).Within(0.00001f));
					Assert.That(velocity.z, Is.Zero);
					Assert.That(velocity.y, Is.EqualTo(impulse).Within(0.00001f));
					manager.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(velocity.x, -3f, velocity.z) });
				}
				Assert.That(manager.HasComponent<MmdSampleJumpImpulse>(entities[0]), Is.True);
				Assert.That(manager.GetComponentData<PhysicsVelocity>(entities[0]).Linear, Is.EqualTo(float3.zero));
				sample.SetPaused(true);
				sample.Tick();
				Assert.That(manager.GetComponentData<PhysicsVelocity>(entities[1]).Linear.x, Is.EqualTo(-0.6f).Within(0.00001f));
				sample.SetPaused(false);
				manager.RemoveComponent<Disabled>(entities[0]);
				sample.Tick();
				Assert.That(manager.HasComponent<MmdSampleJumpImpulse>(entities[0]), Is.False);
				Assert.That(manager.GetComponentData<PhysicsVelocity>(entities[0]).Linear.y, Is.EqualTo(impulse).Within(0.00001f));
				for (int index = 1; index < count; index++) {
					var velocity = manager.GetComponentData<PhysicsVelocity>(entities[index]).Linear;
					Assert.That(velocity.y, Is.EqualTo(-3f), "A consumed impulse cannot be applied on the next tick.");
					Assert.That(velocity.x, Is.EqualTo(index % 2 == 0 ? 1.2f : -1.2f).Within(0.00001f));
				}
			}
		}

		[TestCase(500)]
		[TestCase(1000)]
		public void StatePhaseJobsKeepTransitionsAndGroundPoliciesIndependentWithPauseAndDisabled(int count) {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var world = sample.World;
				var manager = world.EntityManager;
				var begin = world.GetExistingSystemManaged<MmdSampleStateBeginGroup>();
				var commit = world.GetExistingSystemManaged<MmdSampleStateCommitGroup>();
				var cleanup = world.GetExistingSystemManaged<MmdSampleStateCleanupGroup>();
				var policy = world.GetExistingSystem<MmdSampleGroundPolicySystem>();
				var current = MmdSampleStateDescriptor.For<MmdSampleLocomotion>();
				var jump = MmdSampleStateDescriptor.For<MmdSampleJump>();
				var fall = MmdSampleStateDescriptor.For<MmdSampleFall>();
				var entities = new Entity[count];
				var archetype = manager.CreateArchetype(typeof(MmdSampleStateMachine), typeof(MmdSampleStateTransition), typeof(MmdSampleState<MmdSampleLocomotion>),
					typeof(MmdSampleMovement), typeof(MmdSampleMovementSettings), typeof(MmdGroundMovement));
				for (int index = 0; index < count; index++) {
					Entity entity = manager.CreateEntity(archetype);
					entities[index] = entity;
					manager.SetComponentData(entity, new MmdSampleStateMachine { Current = current, Revision = 9 });
					manager.SetComponentData(entity, new MmdSampleStateTransition { Target = index % 3 == 0 ? current : index % 3 == 1 ? jump : fall });
					manager.SetComponentData(entity, new MmdSampleMovement { IgnoreGround = index % 2 == 0 });
				}
				manager.AddComponent<Disabled>(entities[0]);
				sample.SetPaused(true);
				begin.Update();
				commit.Update();
				cleanup.Update();
				policy.Update(world.Unmanaged);
				for (int index = 0; index < count; index++) {
					Assert.That(manager.GetComponentData<MmdSampleStateMachine>(entities[index]).Revision, Is.EqualTo(9u));
					Assert.That(manager.GetComponentData<MmdSampleStateTransition>(entities[index]).Preparing, Is.False);
					Assert.That(manager.HasComponent<MmdGroundMovement>(entities[index]), Is.True);
				}
				sample.SetPaused(false);
				begin.Update();
				for (int index = 1; index < count; index++) {
					Entity entity = entities[index];
					bool changesState = index % 3 != 0;
					Assert.That(manager.GetComponentData<MmdSampleStateTransition>(entity).Preparing, Is.EqualTo(changesState));
					Assert.That(manager.HasComponent<MmdSampleStateFinished<MmdSampleLocomotion>>(entity), Is.EqualTo(changesState), "Finish markers must be visible before the finish phase.");
					Assert.That(manager.GetComponentData<MmdSampleStateMachine>(entity).Current.Component, Is.EqualTo(current.Component));
				}
				commit.Update();
				for (int index = 1; index < count; index++) {
					Entity entity = entities[index];
					bool changesState = index % 3 != 0;
					var next = index % 3 == 0 ? current : index % 3 == 1 ? jump : fall;
					var machine = manager.GetComponentData<MmdSampleStateMachine>(entity);
					Assert.That(machine.Current.Component, Is.EqualTo(next.Component));
					Assert.That(machine.Revision, Is.EqualTo(changesState ? 10u : 9u));
					Assert.That(manager.HasComponent(entity, next.Component), Is.True);
					Assert.That(manager.HasComponent(entity, next.Started), Is.EqualTo(changesState), "Start markers must be visible in the same tick.");
					Assert.That(manager.HasComponent<MmdSampleStateEntering>(entity), Is.EqualTo(changesState));
					Assert.That(manager.GetComponentData<MmdSampleStateTransition>(entity).Target.IsValid, Is.False);
				}
				cleanup.Update();
				policy.Update(world.Unmanaged);
				for (int index = 1; index < count; index++) {
					Entity entity = entities[index];
					var machine = manager.GetComponentData<MmdSampleStateMachine>(entity);
					Assert.That(manager.HasComponent(entity, machine.Current.Started), Is.False);
					Assert.That(manager.HasComponent<MmdSampleStateFinished<MmdSampleLocomotion>>(entity), Is.False);
					Assert.That(manager.HasComponent<MmdSampleStateEntering>(entity), Is.False);
					Assert.That(manager.HasComponent<MmdGroundMovement>(entity), Is.EqualTo(index % 2 != 0));
				}
				Assert.That(manager.GetComponentData<MmdSampleStateMachine>(entities[0]).Revision, Is.EqualTo(9u));
				Assert.That(manager.GetComponentData<MmdSampleStateTransition>(entities[0]).Target.Component, Is.EqualTo(current.Component));
				Assert.That(manager.HasComponent<MmdGroundMovement>(entities[0]), Is.True);
				manager.RemoveComponent<Disabled>(entities[0]);
				begin.Update();
				policy.Update(world.Unmanaged);
				Assert.That(manager.GetComponentData<MmdSampleStateTransition>(entities[0]).Target.IsValid, Is.False, "A request for the current state must not restart it.");
				Assert.That(manager.GetComponentData<MmdSampleStateMachine>(entities[0]).Revision, Is.EqualTo(9u));
				Assert.That(manager.HasComponent<MmdGroundMovement>(entities[0]), Is.False);
			}
		}
	}
}
