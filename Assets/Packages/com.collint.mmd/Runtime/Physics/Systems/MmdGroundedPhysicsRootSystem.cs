using ColInt.MMD.Actions.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Physics.Helpers;
using ColInt.MMD.RootMotion.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Physics.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdPhysicsPreparationGroup))]
	public partial struct MmdGroundedPhysicsRootSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			state.Dependency = new RootJob { World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdPhysicsRoot))]
		[WithNone(typeof(MmdKinematicRoot))]
		private partial struct RootJob : IJobEntity {
			[ReadOnly] public CollisionWorld World;

			private void Execute(Entity entity, in MmdGroundMovement settings, in PhysicsCollider collider, in MmdActionState action,
				in LocalTransform transform, in PhysicsMass mass, ref PhysicsVelocity velocity, ref MmdRootMotion motion) {
				if (motion.Valid && motion.DeltaTime > 0f && action.ActionId == 0) {
					float3 desired = math.rotate(transform.Rotation, motion.Translation) * transform.Scale;
					float3 resolved = MmdGroundMovementQuery.Move(World, entity, collider, transform, settings, transform.Position, desired, out bool supported);
					motion.Translation = math.rotate(math.inverse(transform.Rotation), resolved - transform.Position) / transform.Scale;
					if (supported) {
						velocity.Linear.y = (resolved.y - transform.Position.y) / motion.DeltaTime;
					}
				}
				MmdPhysicsRootVelocity.Apply(transform, mass, ref velocity, ref motion);
			}
		}
	}
}
