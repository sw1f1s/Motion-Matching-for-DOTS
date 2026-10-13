using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Helpers;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.LocomotionState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleLocomotionStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new StartJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>(), CharacterInputs = SystemAPI.GetComponentLookup<MmdSampleCharacterInputOverride>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleLocomotion>))]
		private partial struct StartJob : IJobEntity {
			public MmdSampleInput Input;
			[ReadOnly] public ComponentLookup<MmdSampleCharacterInputOverride> CharacterInputs;

			private void Execute(Entity entity, in MmdDatabase data, in MmdSampleFall fall, in MmdSampleCharacterControl control,
				ref MmdSampleMovement movement, ref MmdSampleGroundState ground, ref MmdActionRequest request,
				ref MmdSearchSettings settings, ref MmdPlayback playback, ref PhysicsVelocity velocity, ref MmdInertializationState smoothing, DynamicBuffer<MmdMotionGroup> groups) {
				var input = CharacterInputs.HasComponent(entity) ? CharacterInputs[entity].Value : Input;
				// The action has already completed its physical path. Released input must not
				// inherit the source clip's running speed and select another moving stop.
				if (data.Value.Value.Clips[playback.ClipIndex].ActionId != 0 && math.lengthsq(input.Movement) == 0f) {
					velocity = default;
					smoothing.RootVelocity = float3.zero;
					smoothing.RootAngularVelocity = float3.zero;
					smoothing.RootVelocityOffset = float3.zero;
					smoothing.RootAngularOffset = float3.zero;
				}
				movement = default;
				ground.MaximumDownwardSpeed = 0f;
				MmdSampleAnimationRequest.Cancel(ref request);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, control.IdleGroup);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Excluded, fall.Group);
				settings.PreferLoop = false;
				if (math.lengthsq(input.Movement) > 0f && math.lengthsq(velocity.Linear.xz) > math.square(control.WalkSpeed * 0.3f)) {
					// Resume the commanded gait directly when the action already exits in motion.
					MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, MmdSampleGait.Group(control, input));
				}
				playback.SearchCountdown = 0f;
				playback.TransitionElapsed = settings.MinimumTransitionTime;
			}
		}
	}
}
