using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateCleanupGroup))]
	public partial struct MmdSampleStateCleanupSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (machine, entity) in SystemAPI.Query<RefRO<MmdSampleStateMachine>>().WithAll<MmdSampleStateEntering>().WithEntityAccess()) {
				commands.RemoveComponent(entity, machine.ValueRO.Current.Started);
				commands.RemoveComponent<MmdSampleStateEntering>(entity);
			}
			commands.Playback(state.EntityManager);
			SystemAPI.GetSingletonRW<MmdSampleInput>().ValueRW.JumpPressed = false;
		}
	}
}
