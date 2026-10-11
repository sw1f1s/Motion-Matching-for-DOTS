using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.FallState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleFallRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			foreach (var (ground, velocity, settings, transition) in SystemAPI.Query<RefRO<MmdSampleGroundState>, RefRO<PhysicsVelocity>,
				RefRO<MmdSampleMovementSettings>, RefRW<MmdSampleStateTransition>>().WithAll<MmdSampleFall>()
				.WithNone<MmdSampleState<MmdSampleJump>, MmdSampleState<MmdSampleFall>, MmdSampleTraversalActive>()) {
				if (!ground.ValueRO.IsGrounded && velocity.ValueRO.Linear.y <= -settings.ValueRO.FallEntrySpeed) {
					transition.ValueRW.Request<MmdSampleFall>(MmdSampleStatePriority.Fall);
				}
			}
		}
	}
}
