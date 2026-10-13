using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateFinishGroup))]
	public partial struct MmdSampleCrouchExitFinishSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (control, smoothing, progress, settings) in SystemAPI.Query<RefRW<MmdSampleCharacterControl>, RefRW<MmdInertialization>, RefRO<MmdSampleCrouchProgress>, RefRW<MmdSearchSettings>>()
				.WithAll<MmdSampleStateFinished<MmdSampleCrouchExit>>()) {
				control.ValueRW = progress.ValueRO.StandingControl;
				smoothing.ValueRW = progress.ValueRO.StandingInertialization;
				settings.ValueRW.TrajectoryDirectionWeight = progress.ValueRO.StandingDirectionWeight;
			}
		}
	}
}
