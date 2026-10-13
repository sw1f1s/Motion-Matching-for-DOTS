using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.Trajectory.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleMotionFeedbackGroup))]
	public partial struct MmdSamplePhysicsMotionFeedbackSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new FeedbackJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdPhysicsRoot))]
		private partial struct FeedbackJob : IJobEntity {
			private void Execute(in PhysicsVelocity velocity, ref MmdSampleTrajectoryState trajectory) {
				trajectory.Velocity = velocity.Linear;
			}
		}
	}
}
