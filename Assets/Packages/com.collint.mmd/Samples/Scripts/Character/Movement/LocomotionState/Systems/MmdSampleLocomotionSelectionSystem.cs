using ColInt.MMD.Actions.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.Facing.Helpers;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.LocomotionState.Systems {
	[BurstCompile]
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSampleLocomotionSelectionGroup))]
	public partial struct MmdSampleLocomotionSelectionSystem : ISystem {
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<MmdSampleInput>();
			state.RequireForUpdate<MmdWorldState>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			if (SystemAPI.GetSingleton<MmdWorldState>().IsPaused) {
				return;
			}
			state.Dependency = new LocomotionIntentJob {
				Input = SystemAPI.GetSingleton<MmdSampleInput>()
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(MmdSampleTraversalActive), typeof(MmdSampleState<MmdSampleCrouchEnter>), typeof(MmdSampleState<MmdSampleCrouchExit>), typeof(MmdSampleState<MmdSampleJump>), typeof(MmdSampleState<MmdSampleFall>), typeof(MmdSampleState<MmdSampleLanding>))]
		private partial struct LocomotionIntentJob : IJobEntity {
			private const float TurnEntryAngle = 0.2617994f;
			private const float TurnExitAngle = 0.0523599f;
			private const float CrouchStoppingPositionWeight = 8f;

			public MmdSampleInput Input;

			private void Execute(in MmdSampleCharacterControl control, in MmdTrajectoryEnvironment environment, in MmdActionState action, in MmdActionRequest request,
				in LocalTransform transform, in MmdSampleMovement movement, ref MmdSampleTrajectoryState trajectory,
				ref MmdSearchSettings settings, ref MmdPlayback playback) {
				bool cancellingAction = action.ActionId != 0 && request.ActionId == 0 && request.Revision != action.RequestRevision;
				if (action.ActionId != 0 && !cancellingAction) {
					return;
				}
				float speed = Input.Run ? control.RunSpeed : control.WalkSpeed;
				float reachableSpeed = speed * math.saturate(environment.MovementFraction);
				float2 desiredMovement = math.normalizesafe(Input.Movement);
				bool hasMovement = math.lengthsq(desiredMovement) > 0f && speed > 0f;
				// Crouch stops contain several approach steps. Prioritize the requested
				// braking path over their matching moving foot phase when releasing input.
				settings.TrajectoryPositionWeight = movement.CrouchCollider && !hasMovement ? CrouchStoppingPositionWeight : 1f;
				bool wasTurning = control.TurnTags != 0 && settings.RequiredTags == control.TurnTags;
				float turnError = math.abs(MmdSampleFacing.YawError(transform.Rotation, desiredMovement));
				bool turnInPlace = hasMovement && control.TurnTags != 0 &&
					(wasTurning ? turnError > TurnExitAngle : math.isfinite(environment.MaximumSpeed) && turnError > TurnEntryAngle);
				uint tags;
				uint previousGait = settings.RequiredTags & ~control.StartTags;
				bool wasMoving = previousGait == control.WalkTags || previousGait == control.RunTags;
				float movementThreshold = control.WalkSpeed * (wasMoving ? 0.2f : 0.3f);
				if (turnInPlace) {
					// Wall contact selects a turn; unobstructed starts stay searchable in their gait.
					tags = control.TurnTags;
					playback.Speed = 1f;
				} else if (hasMovement && reachableSpeed > math.max(0.1f, movementThreshold)) {
					bool wasRunning = previousGait == control.RunTags;
					bool running = Input.Run && reachableSpeed >= control.WalkSpeed * (wasRunning ? 1.1f : 1.5f);
					tags = running ? control.RunTags : control.WalkTags;
					float clipSpeed = running ? control.RunSpeed : control.WalkSpeed;
					// Normalize only the collision loss, without applying acceleration/braking twice.
					playback.Speed = math.saturate(reachableSpeed / math.max(0.001f, clipSpeed));
				} else {
					tags = control.IdleTags;
					playback.Speed = 1f;
				}
				uint excluded = settings.ExcludedTags & ~control.StartTags;
				if (control.StartTags != 0 && (tags == control.WalkTags || tags == control.RunTags)) {
					bool wasStarting = (settings.RequiredTags & control.StartTags) != 0;
					bool starting = settings.RequiredTags == control.IdleTags || wasStarting &&
						(playback.TransitionElapsed < settings.MinimumTransitionTime || turnError > TurnEntryAngle ||
						math.lengthsq(trajectory.Velocity.xz) < control.WalkSpeed * control.WalkSpeed * 0.09f);
					if (starting) {
						tags |= control.StartTags;
					} else if (turnError <= TurnEntryAngle) {
						// Keep turning starts eligible during moving reversals.
						excluded |= playback.TransitionElapsed >= settings.MinimumTransitionTime
							? control.StartTags : settings.ExcludedTags & control.StartTags;
					}
				}
				// An aligned moving action exit needs a gait cycle, not another acceleration
				// or pivot chosen solely for its close outgoing foot pose.
				settings.PreferLoop = cancellingAction && (tags == control.WalkTags || tags == control.RunTags) &&
					turnError <= TurnEntryAngle && math.lengthsq(trajectory.Velocity.xz) > math.square(control.WalkSpeed * 0.3f);
				bool changedDirection = math.lengthsq(desiredMovement - trajectory.DesiredMovement) > 0.01f;
				trajectory.DesiredMovement = desiredMovement;
				if (changedDirection) {
					playback.TransitionElapsed = settings.MinimumTransitionTime;
				}
				if (settings.RequiredTags != tags || settings.ExcludedTags != excluded || changedDirection) {
					settings.RequiredTags = tags;
					settings.ExcludedTags = excluded;
					playback.SearchCountdown = 0f;
				}
			}
		}
	}
}
