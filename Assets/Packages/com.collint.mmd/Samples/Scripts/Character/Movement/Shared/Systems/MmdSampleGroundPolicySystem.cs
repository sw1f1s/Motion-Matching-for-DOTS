using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStatePolicyGroup))]
	public partial struct MmdSampleGroundPolicySystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			var groundModes = SystemAPI.GetComponentLookup<MmdGroundMovement>(true);
			foreach (var (movement, settings, entity) in SystemAPI.Query<RefRO<MmdSampleMovement>, RefRO<MmdSampleMovementSettings>>().WithEntityAccess()) {
				bool hasGround = groundModes.HasComponent(entity);
				if (movement.ValueRO.IgnoreGround && hasGround) {
					commands.RemoveComponent<MmdGroundMovement>(entity);
				} else if (!movement.ValueRO.IgnoreGround && !hasGround) {
					commands.AddComponent(entity, settings.ValueRO.Ground);
				}
			}
			commands.Playback(state.EntityManager);
		}
	}
}
