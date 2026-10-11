using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateGroundGroup))]
	public partial struct MmdSampleGroundSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new GroundJob {
				World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct GroundJob : IJobEntity {
			[ReadOnly] public CollisionWorld World;

			private void Execute(Entity entity, ref MmdSampleGroundState ground, ref MmdSampleMovement movement,
				in MmdSampleMovementSettings settings, in PhysicsCollider collider, in LocalTransform transform, in PhysicsVelocity velocity) {
				if (movement.AirControl) {
					movement.AirVelocity = velocity.Linear.xz;
				}
				bool followStep = ground.IsGrounded && !movement.IgnoreGround;
				ground.IsGrounded = false;
				if (movement.IgnoreGround && velocity.Linear.y > 0.1f) {
					return;
				}
				var probe = settings.Ground;
				float contactDistance = settings.GroundContactDistance;
				float downwardReach = followStep ? math.max(contactDistance, probe.StepHeight) : contactDistance;
				probe.GroundProbeDistance = downwardReach;
				if (MmdGroundMovementQuery.Ground(World, entity, collider, transform, probe, transform.Position, out var contact)) {
					// Retain locomotion support across a descending step. Airborne states still require actual contact.
					float height = contact.y - transform.Position.y;
					ground.IsGrounded = height >= -downwardReach - probe.Skin && height <= contactDistance + probe.Skin;
				}
			}
		}
	}
}
