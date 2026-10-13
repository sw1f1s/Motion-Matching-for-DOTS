using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.LandingState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateFinishGroup))]
	public partial struct MmdSampleLandingFinishSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var movement in SystemAPI.Query<RefRW<MmdSampleMovement>>().WithAll<MmdSampleStateFinished<MmdSampleLanding>>()) {
				movement.ValueRW = default;
			}
		}
	}
}
