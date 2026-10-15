using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Burst;
using Unity.Collections;
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
			state.Dependency = new MatrixJob { Composed = SystemAPI.GetBufferLookup<MmdComposedPose>(true), Definitions = SystemAPI.GetComponentLookup<MmdAnimationLayersDefinitionReference>(true) }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct MatrixJob : IJobEntity {
			[ReadOnly] public BufferLookup<MmdComposedPose> Composed;
			[ReadOnly] public ComponentLookup<MmdAnimationLayersDefinitionReference> Definitions;

			private void Execute(Entity entity, in DynamicBuffer<MmdSkeletonBone> skeleton, in DynamicBuffer<MmdLocalPose> poses, DynamicBuffer<MmdModelMatrix> matrices) {
				bool layered = Definitions.HasComponent(entity) && Composed.HasBuffer(entity);
				var composed = layered ? Composed[entity] : default;
				layered = layered && composed.Length == poses.Length;
				matrices.ResizeUninitialized(poses.Length);
				for (int bone = 0; bone < poses.Length; bone++) {
					var pose = layered ? composed[bone].Value : poses[bone].Value;
					float4x4 local = float4x4.TRS(pose.Position, pose.Rotation, pose.Scale);
					int parent = skeleton[bone].ParentIndex;
					matrices[bone] = new MmdModelMatrix { Value = parent < 0 ? local : math.mul(matrices[parent].Value, local) };
				}
			}
		}
	}

}
