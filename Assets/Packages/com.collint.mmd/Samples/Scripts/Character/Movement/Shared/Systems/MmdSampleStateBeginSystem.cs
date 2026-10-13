using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateBeginGroup))]
	public partial struct MmdSampleStateBeginSystem : ISystem {
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
			new BeginJob { Commands = commands }.Run();
			if (!commands.IsEmpty) {
				commands.Playback(state.EntityManager);
			}
		}

		[BurstCompile]
		private partial struct BeginJob : IJobEntity {
			public EntityCommandBuffer Commands;

			private void Execute(Entity entity, in MmdSampleStateMachine machine, ref MmdSampleStateTransition transition) {
				if (!transition.Target.IsValid) {
					return;
				}
				if (machine.Current.Component == transition.Target.Component) {
					transition = default;
					return;
				}
				transition.Preparing = true;
				if (machine.Current.IsValid) {
					Commands.AddComponent(entity, machine.Current.Finished);
				}
			}
		}
	}
}
