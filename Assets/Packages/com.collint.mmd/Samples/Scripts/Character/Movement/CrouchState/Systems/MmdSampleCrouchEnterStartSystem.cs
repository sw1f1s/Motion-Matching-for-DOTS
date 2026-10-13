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
	public partial struct MmdSampleCrouchEnterStartSystem : ISystem {
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
		[WithAll(typeof(MmdSampleStateStarted<MmdSampleCrouchEnter>))]
		private partial struct StartJob : IJobEntity {
			public MmdSampleInput Input;

			private void Execute(in MmdSampleCrouch crouch, ref MmdSampleCrouchProgress progress, ref MmdSampleCharacterControl control,
				in PhysicsVelocity velocity, ref MmdSampleMovement movement, ref MmdSearchSettings settings, ref MmdPlayback playback,
				ref MmdInertialization smoothing, in MmdDatabase data, ref MmdActionRequest request, DynamicBuffer<MmdSearchQueryElement> query) {
				progress.StandingControl = control;
				progress.StandingInertialization = smoothing;
				progress.StandingDirectionWeight = settings.TrajectoryDirectionWeight;
				float speed = math.length(velocity.Linear.xz);
				bool stationary = speed < control.WalkSpeed * 0.3f && math.lengthsq(Input.Movement) < 0.001f;
				control.WalkSpeed = crouch.Speed;
				control.RunSpeed = crouch.Speed;
				control.SprintSpeed = crouch.Speed;
				control.IdleTags = crouch.IdleTags;
				control.WalkTags = crouch.MoveTags;
				control.RunTags = crouch.MoveTags;
				control.SprintTags = crouch.MoveTags;
				control.TurnTags = crouch.TurnTags;
				control.StartTags = crouch.StartTags;
				movement = new MmdSampleMovement { CrouchCollider = true };
				int action = crouch.EnterAction;
				settings.RequiredTags = stationary ? crouch.IdleTags : crouch.MoveTags | crouch.StartTags;
				smoothing.PoseDuration = math.max(smoothing.PoseDuration, crouch.TransitionDuration);
				settings.ExcludedTags &= ~crouch.StartTags;
				settings.TrajectoryPositionWeight = 1f;
				playback.Speed = stationary ? 1f : math.saturate(crouch.Speed / crouch.AnimationSpeed);
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
