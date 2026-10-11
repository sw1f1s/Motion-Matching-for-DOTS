using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using ColInt.MMD.Graphics.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdGpuRenderInstantiationGroup))]
	public partial struct MmdGpuRenderBindingSystem : ISystem {
		private EntityQuery _loading;
		private BufferLookup<MmdSkeletonBone> _skeletons;
		private BufferLookup<MmdLocalPose> _poses;
		private BufferLookup<MmdModelMatrix> _matrices;
		private BufferLookup<LinkedEntityGroup> _linked;

		public void OnCreate(ref SystemState state) {
			_loading = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdGpuCharacterRender, PrefabLoadResult, MmdGpuCharacterRenderInitialized>().WithNone<MmdGpuCharacterRenderDisabled>().Build(ref state);
			state.RequireForUpdate(_loading);
			_skeletons = state.GetBufferLookup<MmdSkeletonBone>(true);
			_poses = state.GetBufferLookup<MmdLocalPose>(true);
			_matrices = state.GetBufferLookup<MmdModelMatrix>(true);
			_linked = state.GetBufferLookup<LinkedEntityGroup>(true);
		}

		public void OnUpdate(ref SystemState state) {
			_skeletons.Update(ref state);
			_poses.Update(ref state);
			_matrices.Update(ref state);
			_linked.Update(ref state);
			state.Dependency.Complete();
			using (var commands = new EntityCommandBuffer(Allocator.Temp)) {
				foreach (var (render, result, entity) in SystemAPI.Query<RefRW<MmdGpuCharacterRender>, RefRO<PrefabLoadResult>>().WithAll<MmdGpuCharacterRenderInitialized>().WithNone<MmdGpuCharacterRenderDisabled>().WithEntityAccess()) {
					var status = render.ValueRO.Status;
					if (status != MmdGpuRenderStatus.Loading && status != MmdGpuRenderStatus.WaitingForPose) {
						continue;
					}
					var resources = render.ValueRO;
					if (resources.VisualRoot != Entity.Null) {
						continue;
					}
					status = MmdGpuVisualBinding.Validate(entity, result.ValueRO.PrefabRoot, _skeletons, _poses, _matrices, _linked);
					if (status == MmdGpuRenderStatus.Ready) {
						resources.VisualRoot = commands.Instantiate(result.ValueRO.PrefabRoot);
						// The owner retains the asset; the visual instance needs no second request.
						commands.RemoveComponent<RequestEntityPrefabLoaded>(resources.VisualRoot);
						commands.RemoveComponent<PrefabLoadResult>(resources.VisualRoot);
						// Deferred entity references must be remapped by ECB playback, not written through RefRW.
					} else if (status != MmdGpuRenderStatus.WaitingForPose) {
						commands.RemoveComponent<RequestEntityPrefabLoaded>(entity);
						commands.RemoveComponent<PrefabLoadResult>(entity);
					}
					resources.Status = status == MmdGpuRenderStatus.Ready ? MmdGpuRenderStatus.Loading : status;
					commands.SetComponent(entity, resources);
				}
				commands.Playback(state.EntityManager);
			}
		}
	}
}
