using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Presentation.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdPresentationGroup))]
	public partial struct MmdModelMatrixSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdSkeletonBone>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			state.Dependency = new MatrixJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct MatrixJob : IJobEntity {
			private void Execute(in DynamicBuffer<MmdSkeletonBone> skeleton, in DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdModelMatrix> matrices) {
				matrices.ResizeUninitialized(poses.Length);
				for (int bone = 0; bone < poses.Length; bone++) {
					var pose = poses[bone].Value;
					float4x4 local = float4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
					int parent = skeleton[bone].ParentIndex;
					matrices[bone] = new MmdModelMatrix { Value = parent < 0 ? local : math.mul(matrices[parent].Value, local) };
				}
			}
		}
	}

}
