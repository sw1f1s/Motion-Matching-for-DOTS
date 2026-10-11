using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Physics.Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Physics.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdTrajectoryConstraintGroup))]
	public partial struct MmdPhysicsTrajectorySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdGroundMovement>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new TrajectoryJob { World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct TrajectoryJob : IJobEntity {
			[ReadOnly] public CollisionWorld World;

			private void Execute(Entity entity, in MmdGroundMovement settings, in PhysicsCollider collider, in LocalTransform transform,
				in MmdSearchQuerySpace space, in MmdActionState action, ref MmdTrajectoryEnvironment environment,
				DynamicBuffer<MmdTrajectoryPoint> points, DynamicBuffer<MmdSearchQueryElement> query) {
				if (action.ActionId != 0 || points.Length != 3 || query.Length < 12) {
					return;
				}
				float3 resolved = space.Position;
				float3 desiredStart = space.Position;
				environment.MaximumSpeed = float.PositiveInfinity;
				environment.MovementFraction = 1f;
				for (int point = 0; point < points.Length; point++) {
					var sample = points[point];
					float3 desiredEnd = sample.Position;
					float3 delta = (desiredEnd - desiredStart) / 8f;
					float distance = 0f;
					bool constrained = false;
					for (int step = 0; step < 8; step++) {
						float3 start = resolved;
						resolved = MmdGroundMovementQuery.Move(World, entity, collider, transform, settings, resolved, delta, out _);
						float2 movement = (resolved - start).xz;
						distance += math.length(movement);
						constrained |= math.distancesq(movement, delta.xz) > 0.00000001f;
					}
					sample.Position = resolved;
					points[point] = sample;
					float3 local = math.rotate(math.inverse(space.Rotation), resolved - space.Position);
					query[point * 4] = new MmdSearchQueryElement { Value = local.x };
					query[point * 4 + 1] = new MmdSearchQueryElement { Value = local.z };
					if (point == 0 && constrained) {
						// Acceleration and turn braking are already represented by the requested trajectory.
						// Limit animation tempo only when collision changes its planar movement.
						environment.MaximumSpeed = distance / sample.Time;
						float requestedDistance = math.length((desiredEnd - desiredStart).xz);
						environment.MovementFraction = math.saturate(distance / math.max(0.00001f, requestedDistance));
					}
					desiredStart = desiredEnd;
				}
			}
		}
	}
}
