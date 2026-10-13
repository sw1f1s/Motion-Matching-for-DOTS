using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleCrouchStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (crouch, progress, smoothing, movement, request, settings, playback) in SystemAPI.Query<RefRO<MmdSampleCrouch>, RefRO<MmdSampleCrouchProgress>, RefRW<MmdInertialization>, RefRW<MmdSampleMovement>, RefRW<MmdActionRequest>,
				RefRW<MmdSearchSettings>, RefRW<MmdPlayback>>().WithAll<MmdSampleStateStarted<MmdSampleCrouch>>()) {
				smoothing.ValueRW = progress.ValueRO.StandingInertialization;
				movement.ValueRW = new MmdSampleMovement { CrouchCollider = true };
				MmdSampleAnimationRequest.Cancel(ref request.ValueRW);
				settings.ValueRW.RequiredTags = crouch.ValueRO.IdleTags;
				playback.ValueRW.SearchCountdown = 0f;
				playback.ValueRW.Speed = 1f;
			}
		}
	}
}
