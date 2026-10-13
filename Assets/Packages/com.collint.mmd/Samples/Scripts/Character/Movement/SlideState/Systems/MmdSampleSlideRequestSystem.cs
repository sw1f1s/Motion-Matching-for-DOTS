using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateRequestGroup))]
	public partial struct MmdSampleSlideRequestSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new UpdateJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>(),
				CharacterInputs = SystemAPI.GetComponentLookup<MmdSampleCharacterInputOverride>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleCharacterControl), typeof(MmdSampleState<MmdSampleLocomotion>))]
		private partial struct UpdateJob : IJobEntity {
			public MmdSampleInput Input;
			[ReadOnly] public ComponentLookup<MmdSampleCharacterInputOverride> CharacterInputs;
			private void Execute(Entity entity, in MmdSampleSlide slide, in MmdSampleGroundState ground, in PhysicsVelocity velocity, ref MmdSampleStateTransition transition) {
				var input = CharacterInputs.HasComponent(entity) ? CharacterInputs[entity].Value : Input;
				if (input.Crouch && input.CrouchPressed && input.Sprint && ground.IsGrounded && math.length(velocity.Linear.xz) >= slide.MinimumEntrySpeed) {
					transition.Request<MmdSampleSlide>(MmdSampleStatePriority.Slide);
				}
			}
		}
	}
}
