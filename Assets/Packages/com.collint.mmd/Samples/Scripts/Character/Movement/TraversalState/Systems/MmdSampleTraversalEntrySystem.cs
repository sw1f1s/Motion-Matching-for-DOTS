using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleActionEntryGroup))]
	public partial struct MmdSampleTraversalEntrySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleTraversalEntry>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (progress, playback, entity) in SystemAPI.Query<RefRO<MmdSampleTraversalProgress>, RefRW<MmdPlayback>>()
				.WithAll<MmdSampleTraversalEntry>().WithEntityAccess()) {
				playback.ValueRW.Time = progress.ValueRO.Clip.StartTime;
				commands.RemoveComponent<MmdSampleTraversalEntry>(entity);
			}
			commands.Playback(state.EntityManager);
		}
	}
}
