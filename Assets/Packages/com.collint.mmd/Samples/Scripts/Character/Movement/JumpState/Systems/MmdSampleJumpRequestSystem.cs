using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleJumpRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (ground, intent, transition) in SystemAPI.Query<RefRO<MmdSampleGroundState>, RefRO<MmdSampleJumpIntent>,
				RefRW<MmdSampleStateTransition>>().WithAll<MmdSampleCharacterControl, MmdSampleJump>()
				.WithAny<MmdSampleState<MmdSampleLocomotion>, MmdSampleState<MmdSampleLanding>, MmdSampleState<MmdSampleFall>>()) {
				if (intent.ValueRO.Remaining > 0f && (ground.ValueRO.IsGrounded || intent.ValueRO.CoyoteRemaining > 0f)) {
					transition.ValueRW.Request<MmdSampleJump>(MmdSampleStatePriority.Jump);
				}
			}
		}
	}
}
