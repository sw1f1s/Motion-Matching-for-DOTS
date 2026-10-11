using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateBeginGroup))]
	public partial struct MmdSampleStateBeginSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (machine, transition, entity) in SystemAPI.Query<RefRO<MmdSampleStateMachine>, RefRW<MmdSampleStateTransition>>().WithEntityAccess()) {
				if (!transition.ValueRO.Target.IsValid) {
					continue;
				}
				if (machine.ValueRO.Current.Component == transition.ValueRO.Target.Component) {
					transition.ValueRW = default;
					continue;
				}
				transition.ValueRW.Preparing = true;
				if (machine.ValueRO.Current.IsValid) {
					commands.AddComponent(entity, machine.ValueRO.Current.Finished);
				}
			}
			commands.Playback(state.EntityManager);
		}
	}
}
