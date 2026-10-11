using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStatePolicyGroup))]
	public partial struct MmdSampleCapsulePolicySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
			foreach (var (movement, owner, collider, mass, transform, entity) in SystemAPI.Query<RefRO<MmdSampleMovement>, RefRO<MmdSamplePhysicsColliderOwner>,
				RefRW<PhysicsCollider>, RefRW<PhysicsMass>, RefRO<LocalTransform>>().WithEntityAccess()) {
				var target = movement.ValueRO.TraversalCollider ? owner.ValueRO.Traversal :
					movement.ValueRO.CrouchCollider ? owner.ValueRO.Crouched : owner.ValueRO.Value;
				if (!target.IsCreated || target == collider.ValueRO.Value) {
					continue;
				}
				if (!movement.ValueRO.CrouchCollider && !movement.ValueRO.TraversalCollider && !MmdSampleCapsuleQuery.CanStand(world, entity, target, transform.ValueRO)) {
					continue;
				}
				collider.ValueRW.Value = target;
				float bodyMass = 1f / mass.ValueRO.InverseMass;
				mass.ValueRW = PhysicsMass.CreateDynamic(target.Value.MassProperties, bodyMass);
				mass.ValueRW.InverseInertia = float3.zero;
			}
		}
	}
}
