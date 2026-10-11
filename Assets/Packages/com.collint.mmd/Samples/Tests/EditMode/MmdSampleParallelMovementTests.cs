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
						typeof(MmdSampleMovementSettings), typeof(MmdSampleCharacterControl), typeof(MmdSampleCharacterInputOverride), typeof(MmdSampleJumpImpulse));
					entities[index] = entity;
					manager.SetComponentData(entity, new MmdSampleMovement { AirControl = true, IgnoreGround = true });
					manager.SetComponentData(entity, new MmdSampleMovementSettings {
						JumpHeight = 2f, AirAcceleration = 6f, MaximumFallSpeed = 55f, WalkSpeed = 2f, RunSpeed = 4f
					});
					manager.SetComponentData(entity, new MmdSampleCharacterInputOverride {
						Value = new MmdSampleInput { Movement = index % 2 == 0 ? new float2(1f, 0f) : new float2(-1f, 0f), Run = true }
					});
				}
				manager.AddComponent<Disabled>(entities[0]);
				sample.SetInput(new MmdSampleInput { Movement = new float2(0f, 1f) });
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
	}
}
