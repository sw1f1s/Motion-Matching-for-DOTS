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
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Systems {
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleStateStartGroup))]
	public partial struct MmdSampleCrouchExitStartSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdWorldState>();
			state.RequireForUpdate<MmdSampleInput>();
		}

		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new StartJob { Input = SystemAPI.GetSingleton<MmdSampleInput>() }.Schedule(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleCrouchExit>))]
		private partial struct StartJob : IJobEntity {
			public MmdSampleInput Input;

			private void Execute(in MmdSampleCrouch crouch, ref MmdSampleCrouchProgress progress, ref MmdSampleCharacterControl control,
				in PhysicsVelocity velocity, ref MmdSampleMovement movement, ref MmdSearchSettings settings, ref MmdPlayback playback,
				ref MmdInertialization smoothing, in MmdDatabase data, ref MmdActionRequest request, DynamicBuffer<MmdSearchQueryElement> query, DynamicBuffer<MmdMotionGroup> groups) {
				control = progress.StandingControl;
				settings.TrajectoryDirectionWeight = progress.StandingDirectionWeight;
				bool stationary = math.length(velocity.Linear.xz) < control.WalkSpeed * 0.3f && math.lengthsq(Input.Movement) < 0.001f;
				movement = default;
				int action = crouch.ExitAction;
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, stationary ? control.IdleGroup : MmdSampleGait.Group(control, Input), stationary ? 0 : control.StartGroup);
				smoothing.PoseDuration = math.max(smoothing.PoseDuration, crouch.TransitionDuration);
				MmdMotionGroups.Remove(groups, control.StartGroup, MmdMotionGroupUsage.Excluded);
				settings.TrajectoryPositionWeight = 1f;
				playback.Speed = 1f;
				playback.SearchCountdown = 0f;
				playback.TransitionElapsed = settings.MinimumTransitionTime;
				progress.Duration = crouch.TransitionDuration;
				if (stationary) {
					MmdSampleAnimationRequest.Play(ref data.Value.Value, query, action, ref request);
					progress.Duration = data.Value.Value.Clips[request.PreferredClip].Duration;
				} else {
					MmdSampleAnimationRequest.Cancel(ref request);
				}
			}
		}
	}
}
