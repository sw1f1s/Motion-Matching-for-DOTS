using ColInt.MMD.Physics.Components;
using ColInt.MMD.RootMotion.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Physics.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdPhysicsPreparationGroup))]
	public partial struct MmdPhysicsRootSystem : ISystem {
		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			state.Dependency = new PhysicsRootJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdPhysicsRoot))]
		[WithNone(typeof(MmdKinematicRoot), typeof(MmdGroundMovement))]
		private partial struct PhysicsRootJob : IJobEntity {
			private void Execute(in LocalTransform transform, in PhysicsMass mass, ref PhysicsVelocity velocity, ref MmdRootMotion motion) {
				MmdPhysicsRootVelocity.Apply(transform, mass, ref velocity, ref motion);
			}
		}
	}

}
