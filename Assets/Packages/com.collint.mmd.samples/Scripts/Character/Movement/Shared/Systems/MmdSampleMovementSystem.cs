using ColInt.MMD.Samples.Character.Movement.LocomotionState.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleMovementGroup))]
	public partial struct MmdSampleMovementSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsStep>();
			state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var commands = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
				.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
			state.Dependency = new MovementJob {
				Impulses = SystemAPI.GetComponentLookup<MmdSampleJumpImpulse>(true),
				Controls = SystemAPI.GetComponentLookup<MmdSampleCharacterControl>(true),
				Gravity = math.abs(SystemAPI.GetSingleton<PhysicsStep>().Gravity.y),
				DeltaTime = SystemAPI.Time.DeltaTime, Commands = commands
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct MovementJob : IJobEntity {
			[ReadOnly] public ComponentLookup<MmdSampleJumpImpulse> Impulses;
			[ReadOnly] public ComponentLookup<MmdSampleCharacterControl> Controls;
			public float Gravity;
			public float DeltaTime;
			public EntityCommandBuffer.ParallelWriter Commands;

			private void Execute(in MmdSampleInput input, [ChunkIndexInQuery] int sortKey, Entity entity, ref PhysicsVelocity velocity, ref MmdSampleMovement movement,
				ref MmdSampleGroundState ground, in MmdSampleMovementSettings settings) {
				if (Impulses.HasComponent(entity)) {
					velocity.Linear.y = math.sqrt(2f * Gravity * settings.JumpHeight);
					// The fixed-step ECB consumes the impulse before the next tick, without completing this job here.
					Commands.RemoveComponent<MmdSampleJumpImpulse>(sortKey, entity);
				}
				if (movement.AirControl) {
					float2 planar = movement.AirVelocity;
					if (Controls.HasComponent(entity)) {
								float speed = MmdSampleGait.Speed(settings, input);
						float2 desired = input.Movement * speed;
						float2 change = desired - planar;
						planar += math.normalizesafe(change) * math.min(math.length(change), settings.AirAcceleration * DeltaTime);
					}
					velocity.Linear.x = planar.x;
					velocity.Linear.z = planar.y;
					// Reconcile retained momentum with contact resolution from the preceding physics step.
					movement.AirVelocity = planar;
				}
				velocity.Linear.y = math.max(velocity.Linear.y, -settings.MaximumFallSpeed + Gravity * DeltaTime);
				if (!ground.IsGrounded || movement.IgnoreGround) {
					ground.MaximumDownwardSpeed = math.max(ground.MaximumDownwardSpeed,
						math.min(settings.MaximumFallSpeed, math.max(0f, -velocity.Linear.y + Gravity * DeltaTime)));
				}
			}
		}
	}
}
