using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Graphics.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSkinningGroup))]
	public partial struct MmdSkinMatrixSystem : ISystem {
		private BufferLookup<MmdModelMatrix> _matrices;

		public void OnCreate(ref SystemState state) {
			_matrices = state.GetBufferLookup<MmdModelMatrix>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			_matrices.Update(ref state);
			state.Dependency = new SkinJob { Matrices = _matrices }.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		private partial struct SkinJob : IJobEntity {
			[ReadOnly] public BufferLookup<MmdModelMatrix> Matrices;

			private void Execute(in MmdSkinSource source, in DynamicBuffer<MmdSkinBinding> bindings, DynamicBuffer<SkinMatrix> skin) {
				var matrices = Matrices[source.PoseEntity];
				if (matrices.Length == 0) {
					return;
				}
				skin.ResizeUninitialized(bindings.Length);
				for (int i = 0; i < bindings.Length; i++) {
					MmdSkinBinding binding = bindings[i];
					float4x4 value = math.mul(source.ModelToMesh, math.mul(matrices[binding.BoneIndex].Value, binding.BindPose));
					skin[i] = new SkinMatrix { Value = new float3x4(value.c0.xyz, value.c1.xyz, value.c2.xyz, value.c3.xyz) };
				}
			}
		}
	}

}
