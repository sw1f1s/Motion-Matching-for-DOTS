using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleCrouchEnterUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var input = SystemAPI.GetSingleton<MmdSampleInput>();
			bool moving = math.lengthsq(input.Movement) > 0.001f;
			var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
			foreach (var (current, progress, action, transition, transform, owner, entity) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleCrouchEnter>>, RefRO<MmdSampleCrouchProgress>,
				RefRO<MmdActionState>, RefRW<MmdSampleStateTransition>, RefRO<LocalTransform>, RefRO<MmdSamplePhysicsColliderOwner>>().WithEntityAccess()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if (!input.Crouch && MmdSampleCapsuleQuery.CanStand(world, entity, owner.ValueRO.Value, transform.ValueRO)) {
					transition.ValueRW.Request<MmdSampleCrouchExit>(MmdSampleStatePriority.Completion);
				} else if (current.ValueRO.Elapsed >= progress.ValueRO.Duration || moving || action.ValueRO.ActionId == 0 ||
					action.ValueRO.Completed || action.ValueRO.Rejected) {
					transition.ValueRW.Request<MmdSampleCrouch>(MmdSampleStatePriority.Completion);
				}
			}
		}
	}
}
