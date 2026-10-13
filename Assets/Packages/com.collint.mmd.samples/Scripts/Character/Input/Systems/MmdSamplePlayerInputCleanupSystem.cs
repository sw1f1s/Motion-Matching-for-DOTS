using ColInt.MMD.Samples.Character.Input.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePlayerInputGroup), OrderFirst = true)]
	public partial struct MmdSamplePlayerInputCleanupSystem : ISystem {
		private EntityQuery _released;

		public void OnCreate(ref SystemState state) {
			_released = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSamplePlayerInputInitialized>().WithNone<MmdSamplePlayerInput>()
				.WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
			state.RequireForUpdate(_released);
		}

		public void OnUpdate(ref SystemState state) {
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (_, entity) in SystemAPI.Query<RefRO<MmdSamplePlayerInputInitialized>>().WithNone<MmdSamplePlayerInput>()
				.WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess()) {
				commands.RemoveComponent<MmdSamplePlayerInputInitialized>(entity);
			}
			commands.Playback(state.EntityManager);
		}
	}
}
