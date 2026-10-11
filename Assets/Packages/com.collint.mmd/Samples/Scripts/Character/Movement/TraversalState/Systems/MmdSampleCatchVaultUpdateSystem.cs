using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleCatchVaultUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleState<MmdSampleCatchVault>>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var blocked = SystemAPI.GetComponentLookup<MmdSampleTraversalBlocked>(true);
			foreach (var (current, action, playback, target, progress, transform, transition, entity) in
				SystemAPI.Query<RefRW<MmdSampleState<MmdSampleCatchVault>>, RefRO<MmdActionState>, RefRO<MmdPlayback>, RefRO<MmdSampleTraversalTarget>,
					RefRO<MmdSampleTraversalProgress>, RefRO<LocalTransform>, RefRW<MmdSampleStateTransition>>().WithEntityAccess()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if (blocked.HasComponent(entity) || action.ValueRO.Rejected || current.ValueRO.Elapsed > progress.ValueRO.Clip.Duration - progress.ValueRO.Clip.StartTime + 0.5f) {
					transition.ValueRW.Request<MmdSampleFall>(MmdSampleStatePriority.Fall);
				} else if (action.ValueRO.Completed || playback.ValueRO.Time >= progress.ValueRO.Clip.Duration) {
					// A finished clip must release its pose even when physical arrival was interrupted.
					if (target.ValueRO.Supported && math.distancesq(transform.ValueRO.Position, target.ValueRO.End) < 0.0144f) {
						transition.ValueRW.Request<MmdSampleLocomotion>(MmdSampleStatePriority.Completion);
					} else {
						transition.ValueRW.Request<MmdSampleFall>(MmdSampleStatePriority.Fall);
					}
				}
			}
		}
	}
}
