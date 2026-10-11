using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateGroundGroup))]
	public partial struct MmdSampleGroundSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
			foreach (var (ground, movement, settings, collider, transform, velocity, entity) in
				SystemAPI.Query<RefRW<MmdSampleGroundState>, RefRW<MmdSampleMovement>, RefRO<MmdSampleMovementSettings>,
					RefRO<PhysicsCollider>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>>().WithEntityAccess()) {
				if (movement.ValueRO.AirControl) {
					movement.ValueRW.AirVelocity = velocity.ValueRO.Linear.xz;
				}
				bool followStep = ground.ValueRO.IsGrounded && !movement.ValueRO.IgnoreGround;
				ground.ValueRW.IsGrounded = false;
				if (movement.ValueRO.IgnoreGround && velocity.ValueRO.Linear.y > 0.1f) {
					continue;
				}
				var probe = settings.ValueRO.Ground;
				float contactDistance = settings.ValueRO.GroundContactDistance;
				float downwardReach = followStep ? math.max(contactDistance, probe.StepHeight) : contactDistance;
				probe.GroundProbeDistance = downwardReach;
				if (MmdGroundMovementQuery.Ground(world, entity, collider.ValueRO, transform.ValueRO, probe, transform.ValueRO.Position, out var contact)) {
					// Retain locomotion support across a descending step. Airborne states still require actual contact.
					float height = contact.y - transform.ValueRO.Position.y;
					ground.ValueRW.IsGrounded = height >= -downwardReach - probe.Skin && height <= contactDistance + probe.Skin;
				}
			}
		}
	}
}
