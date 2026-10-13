using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Helpers;
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
using Unity.Collections;
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
				CrouchSettings = SystemAPI.GetComponentLookup<MmdSampleCrouch>(true), CrouchProgress = SystemAPI.GetComponentLookup<MmdSampleCrouchProgress>(true)
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(MmdSampleState<MmdSampleSlide>), typeof(MmdSampleState<MmdSampleSlideExit>), typeof(MmdSampleTraversalActive), typeof(MmdSampleState<MmdSampleCrouchEnter>), typeof(MmdSampleState<MmdSampleCrouchExit>), typeof(MmdSampleState<MmdSampleJump>), typeof(MmdSampleState<MmdSampleFall>), typeof(MmdSampleState<MmdSampleLanding>))]
		private partial struct LocomotionIntentJob : IJobEntity {
			private const float TurnEntryAngle = 0.2617994f;
			private const float TurnExitAngle = 0.0523599f;
			private const float CrouchStoppingPositionWeight = 8f;
			private const float CrouchDirectionWeight = 256f;

			[ReadOnly] public ComponentLookup<MmdSampleCrouch> CrouchSettings;
			[ReadOnly] public ComponentLookup<MmdSampleCrouchProgress> CrouchProgress;

			private void Execute(in MmdSampleInput input, Entity entity, in MmdSampleCharacterControl control, in MmdTrajectoryEnvironment environment, in MmdActionState action, in MmdActionRequest request,
				in LocalTransform transform, in MmdSampleMovement movement, ref MmdSampleTrajectoryState trajectory,
				ref MmdSearchSettings settings, ref MmdPlayback playback, DynamicBuffer<MmdMotionGroup> groups) {
				bool cancellingAction = action.ActionId != 0 && request.ActionId == 0 && request.Revision != action.RequestRevision;
				if (action.ActionId != 0 && !cancellingAction) {
					return;
				}
				float speed = MmdSampleGait.Speed(control, input);
				float reachableSpeed = speed * math.saturate(environment.MovementFraction);
				float2 desiredMovement = math.normalizesafe(input.Movement);
				bool hasMovement = math.lengthsq(desiredMovement) > 0f && speed > 0f;
				// Crouch stops contain several approach steps. Prioritize the requested
				// braking path over their matching moving foot phase when releasing input.
				settings.TrajectoryPositionWeight = movement.CrouchCollider && !hasMovement ? CrouchStoppingPositionWeight : 1f;
				float directionChange = math.lengthsq(desiredMovement - trajectory.DesiredMovement);
				bool wasTurning = control.TurnGroup != 0 && MmdMotionGroups.Is(groups, MmdMotionGroupUsage.Required, control.TurnGroup);
				float turnError = math.abs(MmdSampleFacing.YawError(transform.Rotation, desiredMovement));
				bool turnInPlace = hasMovement && control.TurnGroup != 0 &&
					(wasTurning ? turnError > TurnExitAngle : math.isfinite(environment.MaximumSpeed) && turnError > TurnEntryAngle);
				int group;
				bool requireStart = false;
				int previousGait = MmdMotionGroups.Contains(groups, control.SprintGroup, MmdMotionGroupUsage.Required) ? control.SprintGroup :
					MmdMotionGroups.Contains(groups, control.RunGroup, MmdMotionGroupUsage.Required) ? control.RunGroup :
					MmdMotionGroups.Contains(groups, control.WalkGroup, MmdMotionGroupUsage.Required) ? control.WalkGroup :
					MmdMotionGroups.First(groups, MmdMotionGroupUsage.Required, control.StartGroup);
				bool wasMoving = previousGait == control.WalkGroup || previousGait == control.RunGroup || previousGait == control.SprintGroup;
				float movementThreshold = control.WalkSpeed * (wasMoving ? 0.2f : 0.3f);
				if (turnInPlace) {
					// Wall contact selects a turn; unobstructed starts stay searchable in their gait.
					group = control.TurnGroup;
					playback.Speed = 1f;
				} else if (hasMovement && reachableSpeed > math.max(0.1f, movementThreshold)) {
					bool wasRunning = previousGait == control.RunGroup || previousGait == control.SprintGroup;
					bool running = (input.Run || input.Sprint) && reachableSpeed >= control.WalkSpeed * (wasRunning ? 1.1f : 1.5f);
					bool wasSprinting = previousGait == control.SprintGroup;
					bool sprinting = input.Sprint && reachableSpeed >= control.RunSpeed * (wasSprinting ? 1.05f : 1.25f);
					group = sprinting ? control.SprintGroup : running ? control.RunGroup : control.WalkGroup;
					float clipSpeed = movement.CrouchCollider ? CrouchSettings[entity].AnimationSpeed : sprinting ? control.SprintSpeed : running ? control.RunSpeed : control.WalkSpeed;
					// Normalize only the collision loss, without applying acceleration/braking twice.
					playback.Speed = math.saturate(reachableSpeed / math.max(0.001f, clipSpeed));
				} else {
					group = control.IdleGroup;
					playback.Speed = 1f;
				}
				bool excludeStart = false;
				if (control.StartGroup != 0 && (group == control.WalkGroup || group == control.RunGroup || group == control.SprintGroup)) {
					bool wasStarting = MmdMotionGroups.Contains(groups, control.StartGroup, MmdMotionGroupUsage.Required);
					bool starting = wasTurning || MmdMotionGroups.Is(groups, MmdMotionGroupUsage.Required, control.IdleGroup) || wasStarting &&
						(playback.TransitionElapsed < settings.MinimumTransitionTime || !movement.CrouchCollider && turnError > TurnEntryAngle ||
						math.lengthsq(trajectory.Velocity.xz) < control.WalkSpeed * control.WalkSpeed * 0.09f);
					if (starting) {
						requireStart = true;
					} else if (turnError <= TurnEntryAngle || group == control.SprintGroup) {
						// Sprint reversals use moving pivots, not stationary reface starts.
						// Walk/run retain their turning starts during reversals.
						excludeStart = playback.TransitionElapsed >= settings.MinimumTransitionTime || MmdMotionGroups.Contains(groups, control.StartGroup, MmdMotionGroupUsage.Excluded);
					}
				}
				bool wasReversing = MmdMotionGroups.Contains(groups, control.ReversalGroup, MmdMotionGroupUsage.Required);
				bool requireReversal = control.ReversalGroup != 0 && !movement.CrouchCollider && !requireStart &&
					(group == control.RunGroup || group == control.SprintGroup) &&
					(wasReversing ? turnError > TurnExitAngle && (directionChange <= 1f || turnError > 2.0943951f) :
						wasMoving && turnError > 2.0943951f && directionChange > 1f);
				// Crouch keeps its stride through moving turns. Stationary starts remain
				// eligible until acceleration settles, without restarting pivot tails.
				// Sprint and standing action exits return to cycles after aligning.
				settings.PreferLoop = !requireStart && !requireReversal && (cancellingAction || movement.CrouchCollider || group == control.SprintGroup || wasReversing) &&
					(group == control.WalkGroup || group == control.RunGroup || group == control.SprintGroup) && (movement.CrouchCollider || turnError <= TurnEntryAngle) &&
					math.lengthsq(trajectory.Velocity.xz) > math.square(control.WalkSpeed * 0.3f);
				if (movement.CrouchCollider) {
					// Moving turns and aligned strides must both follow the requested path.
					settings.TrajectoryDirectionWeight = hasMovement ? CrouchDirectionWeight : CrouchProgress[entity].StandingDirectionWeight;
				}
				bool changedDirection = directionChange > 0.01f;
				trajectory.DesiredMovement = desiredMovement;
				// Continuous mouse rotation must not restart a sprint pose blend every tick.
				// A discrete turn of more than 60 degrees still interrupts immediately.
				if (changedDirection && (requireStart || group != control.SprintGroup || directionChange > 1f || !wasMoving)) {
					playback.TransitionElapsed = settings.MinimumTransitionTime;
				}
				if (!MmdMotionGroups.Is(groups, MmdMotionGroupUsage.Required, group, requireStart ? control.StartGroup : requireReversal ? control.ReversalGroup : 0) ||
					MmdMotionGroups.Contains(groups, control.StartGroup, MmdMotionGroupUsage.Excluded) != excludeStart || changedDirection) {
					MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, group, requireStart ? control.StartGroup : requireReversal ? control.ReversalGroup : 0);
					MmdMotionGroups.Remove(groups, control.StartGroup, MmdMotionGroupUsage.Excluded);
					if (excludeStart) {
						MmdMotionGroups.Add(groups, control.StartGroup, MmdMotionGroupUsage.Excluded);
					}
					playback.SearchCountdown = 0f;
				}
			}
		}
	}
}
