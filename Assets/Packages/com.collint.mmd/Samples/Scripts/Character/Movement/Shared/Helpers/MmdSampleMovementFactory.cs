using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.FallState.Components;
using ColInt.MMD.Samples.Character.Movement.JumpState.Components;
using ColInt.MMD.Samples.Character.Movement.LandingState.Components;
using ColInt.MMD.Samples.Character.Movement.LocomotionState.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers;
using Unity.Collections;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Helpers {
	public static class MmdSampleMovementFactory {
		public static void Add(EntityManager manager, Entity entity, MmdSampleCharacterConfiguration configuration, BlobAssetReference<MotionDatabase> database) {
			manager.AddComponent<MmdSampleMovement>(entity);
			if (string.IsNullOrEmpty(configuration.Fall.Group)) {
				return;
			}
			var settings = new MmdSampleMovementSettings {
				JumpHeight = configuration.Jump.Height, TakeoffTime = configuration.Jump.TakeoffTime, AirAcceleration = configuration.Jump.AirAcceleration,
				MaximumFallSpeed = configuration.Fall.MaximumSpeed, GroundContactDistance = configuration.Fall.GroundContactDistance,
				FallEntrySpeed = configuration.Fall.EntrySpeed, HeavyLandingHeight = configuration.Landing.HeavyHeight,
				LightRecoveryTime = configuration.Landing.LightRecoveryTime, HeavyRecoveryTime = configuration.Landing.HeavyRecoveryTime,
				WalkSpeed = configuration.Locomotion.WalkSpeed, RunSpeed = configuration.Locomotion.RunSpeed, Ground = MmdGroundMovement.Default
			};
			if (settings.JumpHeight <= 0f || settings.TakeoffTime < 0f || settings.AirAcceleration < 0f || settings.MaximumFallSpeed <= 0f ||
				settings.GroundContactDistance <= 0f || settings.FallEntrySpeed <= 0f || settings.HeavyLandingHeight <= 0f ||
				settings.LightRecoveryTime <= 0f || settings.HeavyRecoveryTime <= 0f) {
				throw new ArgumentException("Sample movement needs positive height, fall/contact/recovery settings and nonnegative takeoff/air acceleration.");
			}
			manager.AddComponentData(entity, new MmdSampleJump {
				StandAction = Action(ref database.Value, configuration.Jump.Groups.Stand), WalkAction = Action(ref database.Value, configuration.Jump.Groups.Walk),
				RunAction = Action(ref database.Value, configuration.Jump.Groups.Run)
			});
			var fall = MotionDatabaseGroups.GetGroup(ref database.Value, new FixedString64Bytes(configuration.Fall.Group));
			if (fall.ActionId != 0) {
				throw new ArgumentException("The Fall group must use Motion Matching playback.");
			}
			manager.AddComponentData(entity, new MmdSampleFall { Tags = fall.Tags });
			manager.AddComponentData(entity, new MmdSampleLanding {
				StandLightAction = Action(ref database.Value, configuration.Landing.LightGroups.Stand), WalkLightAction = Action(ref database.Value, configuration.Landing.LightGroups.Walk),
				RunLightAction = Action(ref database.Value, configuration.Landing.LightGroups.Run), StandHeavyAction = Action(ref database.Value, configuration.Landing.HeavyGroups.Stand),
				WalkHeavyAction = Action(ref database.Value, configuration.Landing.HeavyGroups.Walk), RunHeavyAction = Action(ref database.Value, configuration.Landing.HeavyGroups.Run)
			});
			if (!string.IsNullOrEmpty(configuration.Crouch.IdleGroup)) {
				if (configuration.Crouch.Speed <= 0f || configuration.Crouch.TransitionDuration <= 0f) {
					throw new ArgumentException("Sample crouch needs positive speed and transition duration.");
				}
				manager.AddComponentData(entity, new MmdSampleCrouch {
					Speed = configuration.Crouch.Speed, TransitionDuration = configuration.Crouch.TransitionDuration,
					IdleTags = Matching(ref database.Value, configuration.Crouch.IdleGroup), MoveTags = Matching(ref database.Value, configuration.Crouch.MoveGroup),
					TurnTags = Matching(ref database.Value, configuration.Crouch.TurnGroup), StartTags = Matching(ref database.Value, configuration.Crouch.StartGroup),
					EnterAction = Action(ref database.Value, configuration.Crouch.EnterGroup), ExitAction = Action(ref database.Value, configuration.Crouch.ExitGroup)
				});
				manager.AddComponent<MmdSampleCrouchProgress>(entity);
			}
			MmdSampleTraversalFactory.Add(manager, entity, configuration, database);
			manager.AddComponent<MmdSampleJumpProgress>(entity);
			manager.AddComponent<MmdSampleJumpIntent>(entity);
			manager.AddComponent<MmdSampleLocomotion>(entity);
			manager.AddComponentData(entity, settings);
			manager.AddComponent<MmdSampleGroundState>(entity);
			manager.AddComponent<MmdSampleStateMachine>(entity);
			var transition = new MmdSampleStateTransition();
			transition.Request<MmdSampleLocomotion>(MmdSampleStatePriority.Completion);
			manager.AddComponentData(entity, transition);
		}

		private static uint Matching(ref MotionDatabase database, string name) {
			var group = MotionDatabaseGroups.GetGroup(ref database, new FixedString64Bytes(name));
			if (group.ActionId != 0) {
				throw new ArgumentException("Sample crouch locomotion groups must use Motion Matching playback.");
			}
			return group.Tags;
		}

		private static int Action(ref MotionDatabase database, string name) {
			int action = MotionDatabaseGroups.GetActionId(ref database, new FixedString64Bytes(name));
			if (action == 0) {
				throw new ArgumentException("Sample transition groups must use Action playback.");
			}
			for (int clip = 0; clip < database.Clips.Length; clip++) {
				if (database.Clips[clip].ActionId == action) {
					return action;
				}
			}
			throw new ArgumentException("Sample action groups must contain a clip.");
		}
	}
}
