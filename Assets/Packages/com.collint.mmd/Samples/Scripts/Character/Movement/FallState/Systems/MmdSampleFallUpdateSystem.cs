using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.FallState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleFallUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (current, ground, transition) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleFall>>, RefRO<MmdSampleGroundState>,
				RefRW<MmdSampleStateTransition>>()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if (ground.ValueRO.IsGrounded) {
					transition.ValueRW.Request<MmdSampleLanding>(MmdSampleStatePriority.Landing);
				}
			}
		}
	}
}
