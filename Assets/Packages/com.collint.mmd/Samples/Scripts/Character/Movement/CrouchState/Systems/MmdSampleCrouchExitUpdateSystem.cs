using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleCrouchExitUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (current, progress, action, transition, input) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleCrouchExit>>, RefRO<MmdSampleCrouchProgress>,
				RefRO<MmdActionState>, RefRW<MmdSampleStateTransition>, RefRO<MmdSampleInput>>()) {
				bool moving = math.lengthsq(input.ValueRO.Movement) > 0.001f;
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if (current.ValueRO.Elapsed >= progress.ValueRO.Duration || moving ||
					action.ValueRO.Completed || action.ValueRO.Rejected) {
					transition.ValueRW.Request<MmdSampleLocomotion>(MmdSampleStatePriority.Completion);
				}
			}
		}
	}
}
