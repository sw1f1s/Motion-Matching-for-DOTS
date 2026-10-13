using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateCommitGroup))]
	public partial struct MmdSampleStateCommitSystem : ISystem {
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
			new CommitJob { Commands = commands }.Run();
			if (!commands.IsEmpty) {
				commands.Playback(state.EntityManager);
			}
		}

		[BurstCompile]
		private partial struct CommitJob : IJobEntity {
			public EntityCommandBuffer Commands;

			private void Execute(Entity entity, ref MmdSampleStateMachine machine, ref MmdSampleStateTransition transition) {
				if (!transition.Preparing) {
					return;
				}
				var previous = machine.Current;
				var next = transition.Target;
				if (previous.IsValid) {
					Commands.RemoveComponent(entity, previous.Component);
					Commands.RemoveComponent(entity, previous.Finished);
				}
				Commands.AddComponent(entity, next.Component);
				Commands.AddComponent(entity, next.Started);
				Commands.AddComponent<MmdSampleStateEntering>(entity);
				machine.Current = next;
				machine.Revision++;
				transition = default;
			}
		}
	}
}
