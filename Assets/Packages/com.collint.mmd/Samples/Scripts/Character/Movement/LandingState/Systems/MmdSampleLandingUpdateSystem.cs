using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.LandingState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleLandingUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (current, ground, action, transition) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleLanding>>,
				RefRO<MmdSampleGroundState>, RefRO<MmdActionState>, RefRW<MmdSampleStateTransition>>()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if (current.ValueRO.Elapsed >= ground.ValueRO.RecoveryDuration || action.ValueRO.Completed || action.ValueRO.Rejected) {
					transition.ValueRW.Request<MmdSampleLocomotion>(MmdSampleStatePriority.Completion);
				}
			}
		}
	}
}
