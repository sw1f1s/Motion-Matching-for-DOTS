using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.JumpState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleJumpUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (current, velocity, transition) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleJump>>,
				RefRO<PhysicsVelocity>, RefRW<MmdSampleStateTransition>>()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if (velocity.ValueRO.Linear.y <= 0f) {
					transition.ValueRW.Request<MmdSampleFall>(MmdSampleStatePriority.Fall);
				}
			}
		}
	}
}
