using System.Collections.Generic;
using System;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Debugging.Components;
using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Physics.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.Presentation.Components;
using ColInt.MMD.RootMotion.Components;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Helpers;
using ColInt.MMD.Samples.Character.Movement.Trajectory.Components;
using ColInt.MMD.Samples.Physics.Helpers;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Helpers {
	// World-scoped sample factory. Owns database blobs until its world has disposed.
	public sealed class MmdSampleCharacterFactory {
		private readonly World _world;
		private readonly Dictionary<TextAsset, BlobAssetReference<MotionDatabase>> _assetDatabases = new Dictionary<TextAsset, BlobAssetReference<MotionDatabase>>();
		private readonly List<BlobAssetReference<MotionDatabase>> _ownedDatabases = new List<BlobAssetReference<MotionDatabase>>();
		private bool _disposed;

		internal MmdSampleCharacterFactory(World world) {
			_world = world;
		}

		public Entity Create(MmdSampleCharacterConfiguration configuration, in LocalTransform spawnTransform, bool playerControlled, bool drawTrajectory) {
			ThrowIfDisposed();
			if (configuration == null) {
				throw new ArgumentNullException(nameof(configuration));
			}
			var database = LoadDatabase(configuration.Assets.Database);
			Entity entity = Create(database, false, spawnTransform);
			var manager = _world.EntityManager;
			try {
				MmdSampleCharacterPhysicsBuilder.Add(manager, entity, string.IsNullOrEmpty(configuration.Crouch.IdleGroup) ? 0f : configuration.Crouch.Height);
				if (database.IsCreated) {
					manager.AddComponentData(entity, new MmdInertialization {
						PoseDuration = configuration.Transitions.PoseDuration, StationaryPoseDuration = configuration.Transitions.IdlePoseDuration, RootDuration = configuration.Transitions.RootDuration
					});
					manager.AddComponent<MmdInertializationState>(entity);
					manager.AddBuffer<MmdInertialPose>(entity);
				}
				if (playerControlled) {
					manager.AddComponentData(entity, new MmdSampleCharacterControl {
						WalkSpeed = configuration.Locomotion.WalkSpeed, RunSpeed = configuration.Locomotion.RunSpeed, TurnSpeed = configuration.Locomotion.TurnSpeed,
						IdleTags = ResolveTags(database, configuration.Locomotion.IdleGroup), WalkTags = ResolveTags(database, configuration.Locomotion.WalkGroup),
						RunTags = ResolveTags(database, configuration.Locomotion.RunGroup), TurnTags = ResolveTags(database, configuration.Locomotion.TurnGroup),
						StartTags = ResolveTags(database, configuration.Locomotion.StartGroup)
					});
				}
				if (database.IsCreated) {
					MmdSampleMovementFactory.Add(manager, entity, configuration, database);
				}
				if (configuration.Assets.RenderPrefab.IsReferenceValid) {
					manager.AddComponentData(entity, new MmdGpuCharacterRender { Prefab = configuration.Assets.RenderPrefab });
				}
#if UNITY_EDITOR
				if (drawTrajectory) {
					manager.AddComponentData(entity, MmdTrajectoryDebug.Default);
				}
#endif
				return entity;
			} catch {
				manager.DestroyEntity(entity);
				throw;
			}
		}

		public Entity CreateAutonomous(MmdSampleCharacterConfiguration configuration, in LocalTransform spawnTransform) {
			Entity entity = Create(configuration, spawnTransform, true, false);
			try {
				_world.EntityManager.AddComponent<MmdSampleCharacterInputOverride>(entity);
				return entity;
			} catch {
				_world.EntityManager.DestroyEntity(entity);
				throw;
			}
		}

		// Takes ownership of a runtime-allocated blob. Default creates a bind-pose preview.
		// Never pass a baking/scene-owned blob or share the owned blob across worlds.
		public Entity Create(BlobAssetReference<MotionDatabase> database, bool kinematicRoot, in LocalTransform spawnTransform) {
			ThrowIfDisposed();
			if (database.IsCreated && !_ownedDatabases.Contains(database)) {
				_ownedDatabases.Add(database);
			}
			var manager = _world.EntityManager;
			Entity entity = manager.CreateEntity(typeof(LocalTransform));
			try {
				manager.SetComponentData(entity, spawnTransform);
				manager.AddComponent<MmdSampleTrajectoryState>(entity);
				manager.AddComponent<MmdActionState>(entity);
				manager.AddComponentData(entity, new MmdTrajectoryEnvironment { MaximumSpeed = float.PositiveInfinity, MovementFraction = 1f });
				manager.AddBuffer<MmdTrajectoryPoint>(entity);
				manager.AddComponentData(entity, new MmdSearchQuerySpace { Position = spawnTransform.Position, Rotation = spawnTransform.Rotation });
				if (!database.IsCreated) {
					manager.AddComponent<MmdBindPoseInitialization>(entity);
					var trajectory = manager.AddBuffer<MmdSearchQueryElement>(entity);
					trajectory.ResizeUninitialized(LocomotionSchema.Dimension);
					for (int feature = 0; feature < trajectory.Length; feature++) {
						trajectory[feature] = default;
					}
					LocomotionSchema.WriteTrajectory(trajectory, float3.zero, 0f);
					return entity;
				}
				manager.AddComponentData(entity, new MmdDatabase { Value = database });
				manager.AddComponentData(entity, new MmdPlayback { Speed = 1f, MatchedFrame = -1 });
				manager.AddComponentData(entity, new MmdSearchSettings {
					Interval = 0.1f, SwitchPenalty = 1f, MinimumRemainingTime = 0.2f, MinimumSwitchTime = 0.2f, MinimumTransitionTime = 0.2f, TrajectoryDirectionWeight = 4f
				});
				manager.AddComponentData(entity, new MmdActionRequest { PreferredClip = -1 });
				manager.AddComponent<MmdActionWarp>(entity);
				manager.AddComponent<MmdFault>(entity);
				manager.AddComponent<MmdRootMotion>(entity);
				var query = manager.AddBuffer<MmdSearchQueryElement>(entity);
				query.ResizeUninitialized(database.Value.FeatureCount);
				for (int feature = 0; feature < query.Length; feature++) {
					query[feature] = new MmdSearchQueryElement { Value = database.Value.Features[feature] };
				}
				manager.AddBuffer<MmdPoseFeatureHistory>(entity);
				manager.AddBuffer<MmdSkeletonBone>(entity);
				manager.AddBuffer<MmdModelMatrix>(entity);
				var poses = manager.AddBuffer<MmdLocalPose>(entity);
				var skeleton = manager.GetBuffer<MmdSkeletonBone>(entity);
				for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
					skeleton.Add(new MmdSkeletonBone { ParentIndex = database.Value.Parents[bone], PathHash = database.Value.BoneNameHashes[bone] });
					poses.Add(new MmdLocalPose { Value = MmdAnimationMath.PoseAt(ref database.Value, 0, 0f, bone) });
				}
				if (kinematicRoot) {
					manager.AddComponent<MmdKinematicRoot>(entity);
				}
				return entity;
			} catch {
				manager.DestroyEntity(entity);
				throw;
			}
		}

		internal void Dispose() {
			if (_disposed) {
				return;
			}
			_disposed = true;
			foreach (var database in _ownedDatabases) {
				var owned = database;
				owned.Dispose();
			}
			_ownedDatabases.Clear();
			_assetDatabases.Clear();
		}

		private BlobAssetReference<MotionDatabase> LoadDatabase(TextAsset asset) {
			if (asset == null) {
				return default;
			}
			if (_assetDatabases.TryGetValue(asset, out var database)) {
				return database;
			}
			database = MotionDatabaseCodec.Load(asset.GetData<byte>());
			_ownedDatabases.Add(database);
			_assetDatabases.Add(asset, database);
			return database;
		}

		private void ThrowIfDisposed() {
			if (_disposed) {
				throw new ObjectDisposedException(nameof(MmdSampleCharacterFactory));
			}
		}

		private static uint ResolveTags(BlobAssetReference<MotionDatabase> database, string name) {
			if (string.IsNullOrEmpty(name)) {
				return 0;
			}
			MotionGroup group = MotionDatabaseGroups.GetGroup(ref database.Value, new Unity.Collections.FixedString64Bytes(name));
			if (group.ActionId != 0) {
				throw new ArgumentException("Sample locomotion requires a Motion Matching group.");
			}
			return group.Tags;
		}

	}
}
