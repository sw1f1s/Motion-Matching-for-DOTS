using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStatePolicyGroup))]
	public partial struct MmdSampleCapsulePolicySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new CapsulePolicyJob {
				World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct CapsulePolicyJob : IJobEntity {
			[ReadOnly] public CollisionWorld World;

			private void Execute(Entity entity, in MmdSampleMovement movement, in MmdSamplePhysicsColliderOwner owner,
				ref PhysicsCollider collider, ref PhysicsMass mass, in LocalTransform transform) {
				var target = movement.TraversalCollider ? owner.Traversal : movement.CrouchCollider ? owner.Crouched : owner.Value;
				if (!target.IsCreated || target == collider.Value) {
					return;
				}
				if (!movement.CrouchCollider && !movement.TraversalCollider && !MmdSampleCapsuleQuery.CanStand(World, entity, target, transform)) {
					return;
				}
				collider.Value = target;
				float bodyMass = 1f / mass.InverseMass;
				mass = PhysicsMass.CreateDynamic(target.Value.MassProperties, bodyMass);
				mass.InverseInertia = float3.zero;
			}
		}
	}
}
