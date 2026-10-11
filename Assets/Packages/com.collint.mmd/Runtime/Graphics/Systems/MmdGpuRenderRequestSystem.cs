using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Graphics.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Transforms;

namespace ColInt.MMD.Graphics.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdGpuRenderRequestGroup))]
	public partial struct MmdGpuRenderRequestSystem : ISystem {
		private EntityQuery _pending;

		public void OnCreate(ref SystemState state) {
			_pending = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdGpuCharacterRender, LocalTransform>().WithNone<MmdGpuCharacterRenderInitialized, MmdGpuCharacterRenderDisabled>().Build(ref state);
			state.RequireForUpdate(_pending);
		}

		public void OnUpdate(ref SystemState state) {
			using (var commands = new EntityCommandBuffer(Allocator.Temp)) {
				foreach (var (render, entity) in SystemAPI.Query<RefRW<MmdGpuCharacterRender>>().WithAll<LocalTransform>().WithNone<MmdGpuCharacterRenderInitialized, MmdGpuCharacterRenderDisabled>().WithEntityAccess()) {
					var prefab = render.ValueRO.Prefab;
					var status = MmdGpuRenderStatus.InvalidPrefab;
					if (prefab.IsReferenceValid) {
						commands.AddComponent(entity, new RequestEntityPrefabLoaded { Prefab = prefab });
						status = MmdGpuRenderStatus.Loading;
					}
					commands.AddComponent<MmdGpuCharacterRenderInitialized>(entity);
					render.ValueRW.Status = status;
				}
				commands.Playback(state.EntityManager);
			}
		}
	}
}
