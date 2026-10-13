using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Movement.CrouchState.Components;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Helpers {
	public static class MmdSampleSlideFactory {
		public static void Add(EntityManager manager, Entity entity, MmdSampleCharacterConfiguration configuration, BlobAssetReference<MotionDatabase> database) {
			var source = configuration.Slide;
			if (string.IsNullOrEmpty(source.EnterGroup)) {
				return;
			}
			if (!manager.HasComponent<MmdSampleCrouch>(entity) || !math.isfinite(source.MinimumEntrySpeed) || source.MinimumEntrySpeed <= 0f ||
				!math.isfinite(source.Deceleration) || source.Deceleration <= 0f || !math.isfinite(source.StopSpeed) || source.StopSpeed <= 0f ||
				!math.isfinite(source.MaximumSpeed) || source.MaximumSpeed < source.MinimumEntrySpeed || source.StopSpeed >= source.MinimumEntrySpeed ||
				!math.isfinite(source.SteeringSpeed) || source.SteeringSpeed < 0f || !math.isfinite(source.MinimumDownhillAngle) || source.MinimumDownhillAngle <= 0f || source.MinimumDownhillAngle >= 45f) {
				throw new ArgumentException("Slide requires crouch, positive entry/braking/stop speeds, a larger maximum speed, nonnegative steering speed and a downhill angle between zero and 45 degrees.");
			}
			var enter = MotionDatabaseGroups.GetGroup(ref database.Value, new FixedString64Bytes(source.EnterGroup));
			var motion = MotionDatabaseGroups.GetGroup(ref database.Value, new FixedString64Bytes(source.MotionGroup));
			if (enter.ActionId == 0 || motion.ActionId != 0) {
				throw new ArgumentException("Slide entry must be an Action; Slide motion must be Motion Matching.");
			}
			int exits = 0;
			int loops = 0;
			for (int clip = 0; clip < database.Value.Clips.Length; clip++) {
				ref var candidate = ref database.Value.Clips[clip];
				if (!MotionDatabaseGroups.Contains(ref database.Value, candidate, motion.Id)) {
					continue;
				}
				if (candidate.Loop) {
					loops++;
				} else {
					exits++;
				}
			}
			if (loops == 0 || exits == 0) {
				throw new ArgumentException("Slide motion requires a looping slide and a non-looping exit into crouch.");
			}
			manager.AddComponentData(entity, new MmdSampleSlide {
				MinimumEntrySpeed = source.MinimumEntrySpeed, Deceleration = source.Deceleration, StopSpeed = source.StopSpeed, MaximumSpeed = source.MaximumSpeed,
				SteeringSpeed = source.SteeringSpeed, MinimumDownhillNormalY = math.cos(math.radians(source.MinimumDownhillAngle)), EnterAction = enter.ActionId, MotionGroup = motion.Id
			});
			manager.AddComponent<MmdSampleSlideProgress>(entity);
		}
	}
}
