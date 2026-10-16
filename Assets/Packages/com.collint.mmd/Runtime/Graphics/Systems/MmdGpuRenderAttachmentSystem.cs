using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using ColInt.MMD.Graphics.Helpers;
using ColInt.MMD.Presentation.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Transforms;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdGpuRenderAttachmentGroup))]
	public partial struct MmdGpuRenderAttachmentSystem : ISystem {
		private EntityQuery _owners;
		private ComponentLookup<LocalToWorld> _transforms;
		private ComponentLookup<MmdSkinSource> _sources;
		private BufferLookup<LinkedEntityGroup> _linked;
		private BufferLookup<MmdSkeletonBone> _skeletons;
		private BufferLookup<MmdSkinBinding> _bindings;

		public void OnCreate(ref SystemState state) {
			_owners = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdGpuCharacterRender, LocalTransform, MmdGpuCharacterRenderInitialized>().WithNone<MmdGpuCharacterRenderDisabled>().Build(ref state);
			state.RequireForUpdate(_owners);
			_transforms = state.GetComponentLookup<LocalToWorld>(true);
			_sources = state.GetComponentLookup<MmdSkinSource>(true);
			_linked = state.GetBufferLookup<LinkedEntityGroup>(true);
			_skeletons = state.GetBufferLookup<MmdSkeletonBone>(true);
			_bindings = state.GetBufferLookup<MmdSkinBinding>(true);
		}

		public void OnUpdate(ref SystemState state) {
			_transforms.Update(ref state);
			_sources.Update(ref state);
			_linked.Update(ref state);
			_skeletons.Update(ref state);
			_bindings.Update(ref state);
			state.Dependency.Complete();
			using (var commands = new EntityCommandBuffer(Allocator.Temp)) {
				foreach (var (render, transform, entity) in SystemAPI.Query<RefRW<MmdGpuCharacterRender>, RefRO<LocalTransform>>().WithAll<MmdGpuCharacterRenderInitialized>().WithNone<MmdGpuCharacterRenderDisabled>().WithEntityAccess()) {
					if (render.ValueRO.Status != MmdGpuRenderStatus.Loading || render.ValueRO.VisualRoot == Entity.Null) {
						continue;
					}
					var status = MmdGpuVisualBinding.Attach(commands, entity, transform.ValueRO, render.ValueRO.VisualRoot, _transforms, _sources, _linked, _skeletons, _bindings);
					if (status != MmdGpuRenderStatus.Ready) {
						commands.DestroyEntity(render.ValueRO.VisualRoot);
						commands.RemoveComponent<RequestEntityPrefabLoaded>(entity);
						commands.RemoveComponent<PrefabLoadResult>(entity);
						render.ValueRW.VisualRoot = Entity.Null;
					}
					render.ValueRW.Status = status;
				}
				commands.Playback(state.EntityManager);
			}
		}
	}
}
