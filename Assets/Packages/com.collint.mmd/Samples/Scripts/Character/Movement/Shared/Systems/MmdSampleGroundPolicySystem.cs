using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStatePolicyGroup))]
	public partial struct MmdSampleGroundPolicySystem : ISystem {
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
			new GroundPolicyJob {
				Commands = commands,
				GroundModes = SystemAPI.GetComponentLookup<MmdGroundMovement>(true)
			}.Run();
			if (!commands.IsEmpty) {
				commands.Playback(state.EntityManager);
			}
		}

		[BurstCompile]
		private partial struct GroundPolicyJob : IJobEntity {
			public EntityCommandBuffer Commands;
			[ReadOnly] public ComponentLookup<MmdGroundMovement> GroundModes;

			private void Execute(Entity entity, in MmdSampleMovement movement, in MmdSampleMovementSettings settings) {
				bool hasGround = GroundModes.HasComponent(entity);
				if (movement.IgnoreGround && hasGround) {
					Commands.RemoveComponent<MmdGroundMovement>(entity);
				} else if (!movement.IgnoreGround && !hasGround) {
					Commands.AddComponent(entity, settings.Ground);
				}
			}
		}
	}
}
