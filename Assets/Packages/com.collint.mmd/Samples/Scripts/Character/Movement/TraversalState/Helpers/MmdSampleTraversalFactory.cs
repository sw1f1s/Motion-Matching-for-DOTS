using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public static class MmdSampleTraversalFactory {
		public static void Add(EntityManager manager, Entity entity, MmdSampleCharacterConfiguration configuration, BlobAssetReference<MotionDatabase> database) {
			if (string.IsNullOrEmpty(configuration.Traversal.Groups.Mantle)) {
				return;
			}
			var settings = new MmdSampleTraversalSettings {
				Reach = configuration.Traversal.Reach, MinimumHeight = configuration.Traversal.MinimumHeight,
				MaximumHeight = configuration.Traversal.MaximumHeight, MantleHeight = configuration.Traversal.MantleHeight,
				MaximumCrossingDepth = configuration.Traversal.MaximumCrossingDepth, MaximumLandingDrop = configuration.Traversal.MaximumLandingDrop,
				CatchReach = configuration.Traversal.CatchReach, CatchDistance = configuration.Traversal.CatchDistance, Radius = 0.3f, Skin = 0.04f, AlignmentDuration = configuration.Traversal.AlignmentDuration
			};
			if (settings.Reach <= settings.Radius || settings.MinimumHeight <= 0f || settings.MaximumHeight <= settings.MantleHeight ||
				settings.MantleHeight <= settings.MinimumHeight || settings.CatchReach <= settings.MinimumHeight ||
				settings.CatchDistance < 0f || settings.MaximumCrossingDepth < settings.Radius * 2f || settings.MaximumLandingDrop < 0f || settings.AlignmentDuration <= 0f) {
				throw new ArgumentException("Sample traversal requires ordered height ranges, capsule clearance and positive reach/alignment settings.");
			}
			manager.AddComponentData(entity, new MmdSampleMantle { ActionId = Action(ref database.Value, configuration.Traversal.Groups.Mantle) });
			manager.AddComponentData(entity, new MmdSampleClimb { ActionId = Action(ref database.Value, configuration.Traversal.Groups.Climb) });
			manager.AddComponentData(entity, new MmdSampleVault { ActionId = Action(ref database.Value, configuration.Traversal.Groups.Vault) });
			manager.AddComponentData(entity, new MmdSampleHurdle { ActionId = Action(ref database.Value, configuration.Traversal.Groups.Hurdle) });
			manager.AddComponentData(entity, new MmdSampleCatchMantle { ActionId = Action(ref database.Value, configuration.Traversal.Groups.CatchMantle) });
			manager.AddComponentData(entity, new MmdSampleCatchCliff { ActionId = Action(ref database.Value, configuration.Traversal.Groups.CatchCliff) });
			manager.AddComponentData(entity, new MmdSampleCatchVault { ActionId = Action(ref database.Value, configuration.Traversal.Groups.CatchVault) });
			manager.AddComponentData(entity, new MmdSampleCatchHurdle { ActionId = Action(ref database.Value, configuration.Traversal.Groups.CatchHurdle) });
			manager.AddComponentData(entity, settings);
			manager.AddComponent<MmdSampleTraversalTarget>(entity);
			manager.AddComponent<MmdSampleTraversalProgress>(entity);
			manager.AddComponent<PhysicsMassOverride>(entity);
			manager.AddComponentData(entity, new PhysicsGravityFactor { Value = 1f });
			int leftHand = Bone(ref database.Value, configuration.Traversal.LeftHandPath);
			int rightHand = Bone(ref database.Value, configuration.Traversal.RightHandPath);

			var profiles = manager.AddBuffer<MmdSampleTraversalClip>(entity);
			int[] actions = { manager.GetComponentData<MmdSampleMantle>(entity).ActionId, manager.GetComponentData<MmdSampleClimb>(entity).ActionId, manager.GetComponentData<MmdSampleVault>(entity).ActionId, manager.GetComponentData<MmdSampleHurdle>(entity).ActionId, manager.GetComponentData<MmdSampleCatchMantle>(entity).ActionId, manager.GetComponentData<MmdSampleCatchCliff>(entity).ActionId, manager.GetComponentData<MmdSampleCatchVault>(entity).ActionId, manager.GetComponentData<MmdSampleCatchHurdle>(entity).ActionId };
			for (int clip = 0; clip < database.Value.Clips.Length; clip++) {
				for (int group = 0; group < actions.Length; group++) {
					if (database.Value.Clips[clip].ActionId == actions[group]) {
						var profile = MmdSampleTraversalAnimation.Profile(ref database.Value, clip, group >= 4);
						if (group == 1) {
							profile = MmdSampleTraversalAnimation.Contacts(ref database.Value, profile, leftHand, rightHand);
						}
						profiles.Add(profile);
						break;
					}
				}
			}
		}


		private static int Bone(ref MotionDatabase database, string path) {
			int hash = BonePathHash.Calculate(path);
			for (int bone = 0; bone < database.Parents.Length; bone++) {
				if (database.BoneNameHashes[bone] == hash) {
					return bone;
				}
			}
			throw new ArgumentException("Traversal hand path does not identify a database bone: " + path);
		}

		private static int Action(ref MotionDatabase database, string name) {
			int action = MotionDatabaseGroups.GetActionId(ref database, new FixedString64Bytes(name));
			if (action == 0) {
				throw new ArgumentException("Traversal groups must use Action playback.");
			}
			for (int clip = 0; clip < database.Clips.Length; clip++) {
				if (database.Clips[clip].ActionId == action) {
					if (database.Clips[clip].Loop || database.Clips[clip].Duration <= 0.1f) {
						throw new ArgumentException("Traversal clips must be non-looping and longer than 0.1 seconds.");
					}
					return action;
				}
			}
			throw new ArgumentException("Traversal groups must contain clips.");
		}
	}
}
