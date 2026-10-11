using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateCommitGroup))]
	public partial struct MmdSampleStateCommitSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (machine, transition, entity) in SystemAPI.Query<RefRW<MmdSampleStateMachine>, RefRW<MmdSampleStateTransition>>().WithEntityAccess()) {
				if (!transition.ValueRO.Preparing) {
					continue;
				}
				var previous = machine.ValueRO.Current;
				var next = transition.ValueRO.Target;
				if (previous.IsValid) {
					commands.RemoveComponent(entity, previous.Component);
					commands.RemoveComponent(entity, previous.Finished);
				}
				commands.AddComponent(entity, next.Component);
				commands.AddComponent(entity, next.Started);
				commands.AddComponent<MmdSampleStateEntering>(entity);
				machine.ValueRW.Current = next;
				machine.ValueRW.Revision++;
				transition.ValueRW = default;
			}
			commands.Playback(state.EntityManager);
		}
	}
}
