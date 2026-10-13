using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateCleanupGroup))]
	public partial struct MmdSampleStateCleanupSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			// Structural commands must observe completed writers from the preceding phase.
			state.CompleteDependency();
			using var commands = new EntityCommandBuffer(Allocator.TempJob);
			new CleanupJob { Commands = commands }.Run();
			if (!commands.IsEmpty) {
				commands.Playback(state.EntityManager);
			}
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateEntering))]
		private partial struct CleanupJob : IJobEntity {
			public EntityCommandBuffer Commands;

			private void Execute(Entity entity, in MmdSampleStateMachine machine) {
				Commands.RemoveComponent(entity, machine.Current.Started);
				Commands.RemoveComponent<MmdSampleStateEntering>(entity);
			}
		}
	}
}
