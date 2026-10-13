using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateUpdateGroup))]
	public partial struct MmdSampleCrouchUpdateSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
			foreach (var (current, transform, owner, transition, input, entity) in SystemAPI.Query<RefRW<MmdSampleState<MmdSampleCrouch>>, RefRO<LocalTransform>,
				RefRO<MmdSamplePhysicsColliderOwner>, RefRW<MmdSampleStateTransition>, RefRO<MmdSampleInput>>().WithEntityAccess()) {
				current.ValueRW.Elapsed += SystemAPI.Time.DeltaTime;
				if ((!input.ValueRO.Crouch || input.ValueRO.Sprint) && MmdSampleCapsuleQuery.CanStand(world, entity, owner.ValueRO.Value, transform.ValueRO)) {
					transition.ValueRW.Request<MmdSampleCrouchExit>(MmdSampleStatePriority.Completion);
				}
			}
		}
	}
}
