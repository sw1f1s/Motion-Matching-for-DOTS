using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleMovementGroup))]
	public partial struct MmdSampleMovementSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsStep>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var input = SystemAPI.GetSingleton<MmdSampleInput>();
			float gravity = math.abs(SystemAPI.GetSingleton<PhysicsStep>().Gravity.y);
			float deltaTime = SystemAPI.Time.DeltaTime;
			var impulses = SystemAPI.GetComponentLookup<MmdSampleJumpImpulse>(true);
			var players = SystemAPI.GetComponentLookup<MmdSampleCharacterControl>(true);
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (velocity, movement, ground, settings, entity) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRW<MmdSampleMovement>,
				RefRW<MmdSampleGroundState>, RefRO<MmdSampleMovementSettings>>().WithEntityAccess()) {
				if (impulses.HasComponent(entity)) {
					velocity.ValueRW.Linear.y = math.sqrt(2f * gravity * settings.ValueRO.JumpHeight);
					commands.RemoveComponent<MmdSampleJumpImpulse>(entity);
				}
				if (movement.ValueRO.AirControl) {
					float2 planar = movement.ValueRO.AirVelocity;
					if (players.HasComponent(entity)) {
						float speed = input.Run ? settings.ValueRO.RunSpeed : settings.ValueRO.WalkSpeed;
						float2 desired = input.Movement * speed;
						float2 change = desired - planar;
						planar += math.normalizesafe(change) * math.min(math.length(change), settings.ValueRO.AirAcceleration * deltaTime);
					}
					velocity.ValueRW.Linear.x = planar.x;
					velocity.ValueRW.Linear.z = planar.y;
					// Reconcile retained momentum with contact resolution from the preceding physics step.
					movement.ValueRW.AirVelocity = planar;
				}
				velocity.ValueRW.Linear.y = math.max(velocity.ValueRO.Linear.y, -settings.ValueRO.MaximumFallSpeed + gravity * deltaTime);
				if (!ground.ValueRO.IsGrounded || movement.ValueRO.IgnoreGround) {
					ground.ValueRW.MaximumDownwardSpeed = math.max(ground.ValueRO.MaximumDownwardSpeed,
						math.min(settings.ValueRO.MaximumFallSpeed, math.max(0f, -velocity.ValueRO.Linear.y + gravity * deltaTime)));
				}
			}
			commands.Playback(state.EntityManager);
		}
	}
}
