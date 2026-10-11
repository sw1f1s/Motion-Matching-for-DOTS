using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateFinishGroup))]
	public partial struct MmdSampleMantleFinishSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleStateFinished<MmdSampleMantle>>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			var actions = SystemAPI.GetComponentLookup<MmdActionState>(true);
			var blocked = SystemAPI.GetComponentLookup<MmdSampleTraversalBlocked>(true);
			foreach (var (progress, movement, massOverride, gravity, smoothing, target, velocity, entity) in SystemAPI.Query<RefRO<MmdSampleTraversalProgress>,
				RefRW<MmdSampleMovement>, RefRW<PhysicsMassOverride>, RefRW<PhysicsGravityFactor>, RefRW<MmdInertialization>, RefRO<MmdSampleTraversalTarget>, RefRW<PhysicsVelocity>>()
				.WithAll<MmdSampleStateFinished<MmdSampleMantle>>().WithEntityAccess()) {
				MmdSampleTraversalAnimation.Finish(progress.ValueRO, ref movement.ValueRW, ref massOverride.ValueRW, ref gravity.ValueRW, ref smoothing.ValueRW);
				if (!target.ValueRO.Supported && actions[entity].Completed && !blocked.HasComponent(entity)) {
					velocity.ValueRW.Linear = target.ValueRO.ExitVelocity;
					velocity.ValueRW.Angular = float3.zero;
				}
				commands.RemoveComponent<MmdSampleTraversalActive>(entity);
				commands.RemoveComponent<MmdSampleTraversalBlocked>(entity);
			}
			commands.Playback(state.EntityManager);
		}
	}
}
