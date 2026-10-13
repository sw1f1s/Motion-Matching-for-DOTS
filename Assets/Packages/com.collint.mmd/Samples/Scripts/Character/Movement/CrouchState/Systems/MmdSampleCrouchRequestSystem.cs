using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleCrouchRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (ground, transition, input) in SystemAPI.Query<RefRO<MmdSampleGroundState>, RefRW<MmdSampleStateTransition>, RefRO<MmdSampleInput>>()
				.WithAll<MmdSampleCharacterControl, MmdSampleCrouch, MmdSampleState<MmdSampleLocomotion>>()) {
				if (input.ValueRO.Crouch && !input.ValueRO.Sprint && ground.ValueRO.IsGrounded) {
					transition.ValueRW.Request<MmdSampleCrouchEnter>(MmdSampleStatePriority.Crouch);
				}
			}
		}
	}
}
