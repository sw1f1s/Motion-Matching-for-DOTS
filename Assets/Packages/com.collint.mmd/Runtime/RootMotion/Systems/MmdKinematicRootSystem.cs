using ColInt.MMD.RootMotion.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.RootMotion.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdRootApplyGroup))]
	public partial struct MmdKinematicRootSystem : ISystem {
		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			state.Dependency = new ApplyJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdKinematicRoot))]
		private partial struct ApplyJob : IJobEntity {
			private void Execute(ref LocalTransform transform, ref MmdRootMotion motion) {
				if (!motion.Valid) {
					return;
				}
				transform.Position += math.rotate(transform.Rotation, motion.Translation) * transform.Scale;
				transform.Rotation = math.normalize(math.mul(transform.Rotation, motion.Rotation));
				motion.Valid = false;
			}
		}
	}

}
