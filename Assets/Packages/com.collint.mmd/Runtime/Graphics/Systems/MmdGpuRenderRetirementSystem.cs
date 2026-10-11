using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Helpers;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdGpuRenderCleanupGroup))]
	public partial struct MmdGpuRenderRetirementSystem : ISystem {
		private EntityQuery _owners;
		private BufferLookup<LinkedEntityGroup> _linked;
		private EntityStorageInfoLookup _entities;

		public void OnCreate(ref SystemState state) {
			_owners = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdGpuCharacterRender, LocalTransform>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
			state.RequireForUpdate(_owners);
			_linked = state.GetBufferLookup<LinkedEntityGroup>(true);
			_entities = state.GetEntityStorageInfoLookup();
		}

		public void OnUpdate(ref SystemState state) {
			ReleaseOwners(ref state, false);
		}

		public void OnDestroy(ref SystemState state) {
			ReleaseOwners(ref state, true);
		}

		private void ReleaseOwners(ref SystemState state, bool disposing) {
			state.Dependency.Complete();
			_linked.Update(ref state);
			_entities.Update(ref state);
			using (var commands = new EntityCommandBuffer(Allocator.Temp)) {
				foreach (var (render, entity) in SystemAPI.Query<RefRO<MmdGpuCharacterRender>>().WithAll<LocalTransform>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess()) {
					if (!disposing && (!SystemAPI.HasComponent<MmdGpuCharacterRenderInitialized>(entity) || (!SystemAPI.HasComponent<Disabled>(entity) && !SystemAPI.HasComponent<MmdGpuCharacterRenderDisabled>(entity)))) {
						continue;
					}
					MmdGpuVisualLifetime.Release(commands, entity, render.ValueRO, _entities, _linked, disposing);
				}
				commands.Playback(state.EntityManager);
			}
		}
	}
}
