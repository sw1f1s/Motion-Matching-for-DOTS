using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.MotionMatching.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Graphics.Components;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugSnapshotReader : IDisposable {
		private static readonly ProfilerMarker ReadMarker = new ProfilerMarker("MMD.Debug.ReadSnapshots");

		private readonly Dictionary<ulong, MmdDebugWorldQuery> _queries = new Dictionary<ulong, MmdDebugWorldQuery>();
		private readonly List<ulong> _removed = new List<ulong>();
		private readonly MmdDebugCatalogStore _catalogs = new MmdDebugCatalogStore();
		private readonly Dictionary<(ulong World, Entity Entity), MmdDebugPlaybackHistory> _histories = new Dictionary<(ulong, Entity), MmdDebugPlaybackHistory>();
		private readonly HashSet<(ulong World, Entity Entity)> _live = new HashSet<(ulong, Entity)>();
		private readonly HashSet<ulong> _busyWorlds = new HashSet<ulong>();

		public List<MmdDebugEntitySnapshot> Entities { get; } = new List<MmdDebugEntitySnapshot>();
		public double ReadMilliseconds { get; private set; }

		public void ReloadCatalogs() {
			_catalogs.Reload();
		}

		public void Read() {
			using (ReadMarker.Auto()) {
				var timer = Stopwatch.StartNew();
				Entities.Clear();
				_live.Clear();
				_busyWorlds.Clear();
				string observedAt = DateTime.Now.ToString("HH:mm:ss.fff");
				_removed.Clear();
				foreach (var pair in _queries) {
					if (!pair.Value.World.IsCreated) {
						_removed.Add(pair.Key);
					}
				}
				foreach (ulong key in _removed) {
					_queries[key].Dispose();
					_queries.Remove(key);
				}
				if (_removed.Count > 0) {
					_catalogs.ClearResolved();
				}
				foreach (World world in World.All) {
					if (!world.IsCreated) {
						continue;
					}
					var manager = world.EntityManager;
					// Scene streaming owns some worlds exclusively while its asynchronous job runs.
					if (!manager.CanBeginExclusiveEntityTransaction()) {
						_busyWorlds.Add(world.SequenceNumber);
						continue;
					}
					if (!_queries.TryGetValue(world.SequenceNumber, out var query)) {
						query = new MmdDebugWorldQuery(world);
						_queries.Add(world.SequenceNumber, query);
					}
					bool paused = query.State.CalculateEntityCount() == 1 && query.State.GetSingleton<MmdWorldState>().IsPaused;
					using (var entities = query.Characters.ToEntityArray(Allocator.Temp)) {
						foreach (Entity entity in entities) {
							var snapshot = ReadEntity(world, manager, entity, paused);
							var key = (world.SequenceNumber, entity);
							_live.Add(key);
							if (!_histories.TryGetValue(key, out var history)) {
								history = new MmdDebugPlaybackHistory();
								_histories.Add(key, history);
							}
							history.Observe(snapshot, observedAt);
							Entities.Add(snapshot);
						}
					}
				}
				foreach (var key in _histories.Keys.ToArray()) {
					if (!_live.Contains(key) && !_busyWorlds.Contains(key.World)) {
						_histories.Remove(key);
					}
				}
				timer.Stop();
				ReadMilliseconds = timer.Elapsed.TotalMilliseconds;
			}
		}

		public void Dispose() {
			foreach (var query in _queries.Values) {
				query.Dispose();
			}
			_queries.Clear();
			Entities.Clear();
			_histories.Clear();
			_live.Clear();
			_busyWorlds.Clear();
			_catalogs.ClearResolved();
		}

		private MmdDebugEntitySnapshot ReadEntity(World world, EntityManager manager, Entity entity, bool paused) {
			string name = manager.GetName(entity);
			var result = new MmdDebugEntitySnapshot {
				World = world, Entity = entity, WorldLabel = $"{world.Name} · #{world.SequenceNumber}",
				Name = string.IsNullOrEmpty(name) ? $"Entity {entity.Index}:{entity.Version}" : name,
				Playback = manager.GetComponentData<MmdPlayback>(entity), Paused = paused,
				Disabled = manager.HasComponent<Disabled>(entity), Prefab = manager.HasComponent<Prefab>(entity),
				RenderStatus = manager.HasComponent<MmdGpuCharacterRender>(entity) ? manager.GetComponentData<MmdGpuCharacterRender>(entity).Status.ToString() : "Not attached",
				ClipName = "No clip", Groups = "—", RequiredGroups = "Unrestricted", ExcludedGroups = "None", Problem = ""
			};
			result.Status = result.Prefab ? "Prefab" : result.Disabled ? "Disabled" : paused ? "Paused" : "Playing";
			if (manager.HasComponent<MmdGpuCharacterRenderDisabled>(entity)) {
				result.RenderStatus = "Disabled";
			}
			if (manager.HasComponent<MmdFault>(entity)) {
				var fault = manager.GetComponentData<MmdFault>(entity).Code;
				if (fault != MmdFaultCode.None) {
					result.Problem = fault.ToString();
					result.Status = "Fault";
				}
			}
			if (manager.HasComponent<MmdSearchSettings>(entity)) {
				result.Search = manager.GetComponentData<MmdSearchSettings>(entity);
			}
			if (manager.HasComponent<MmdActionState>(entity)) {
				result.Action = manager.GetComponentData<MmdActionState>(entity);
			}
			ReadTransition(manager, entity, result);
			result.HasPosition = manager.HasComponent<LocalTransform>(entity);
			if (result.HasPosition) {
				result.Position = manager.GetComponentData<LocalTransform>(entity).Position;
			}
			result.HasVelocity = manager.HasComponent<PhysicsVelocity>(entity);
			if (result.HasVelocity) {
				result.Velocity = manager.GetComponentData<PhysicsVelocity>(entity).Linear;
			}
			var blob = manager.GetComponentData<MmdDatabase>(entity).Value;
			if (!blob.IsCreated) {
				result.Problem = "Database is not loaded";
				result.Status = "Fault";
				return result;
			}
			ref var database = ref blob.Value;
			result.ClipCount = database.Clips.Length;
			result.FrameCount = database.Frames.Length;
			result.BoneCount = database.Parents.Length;
			result.FeatureCount = database.FeatureCount;
			var catalog = _catalogs.Resolve(blob);
			result.HasNames = catalog != null;
			if (result.HasNames) {
				result.DatabasePath = catalog.DatabasePath;
			}
			result.RequiredGroups = manager.HasComponent<MmdMotionGroup>(entity)
				? FilterGroupNames(ref database, manager.GetBuffer<MmdMotionGroup>(entity, true), MmdMotionGroupUsage.Required, "Unrestricted") : "Unrestricted";
			result.ExcludedGroups = manager.HasComponent<MmdMotionGroup>(entity)
				? FilterGroupNames(ref database, manager.GetBuffer<MmdMotionGroup>(entity, true), MmdMotionGroupUsage.Excluded, "None") : "None";
			int index = result.Playback.ClipIndex;
			if (index < 0 || index >= database.Clips.Length || !math.isfinite(result.Playback.Time) || !math.isfinite(result.Playback.Speed)) {
				result.Problem = "Invalid playback clip or time";
				result.Status = "Fault";
				return result;
			}
			ref var clip = ref database.Clips[index];
			result.ValidClip = true;
			result.ClipName = result.HasNames ? catalog.Names[index] : $"Clip #{index}";
			result.ClipPath = result.HasNames ? catalog.AssetPaths[index] : "";
			result.Duration = clip.Duration;
			result.Loop = clip.Loop;
			result.Progress = math.saturate(result.Playback.Time / clip.Duration);
			result.Frame = MmdAnimationMath.FrameAt(ref database, index, result.Playback.Time) - clip.FirstFrame;
			result.Groups = ClipGroupNames(ref database, clip);
			if (!result.Disabled && !result.Prefab && !paused && result.Problem.Length == 0) {
				result.Status = result.Playback.Speed == 0f ? "Stopped" : result.Action.ActionId != 0 && !result.Action.Completed ? "Action" : "Playing";
			}
			return result;
		}

		private static void ReadTransition(EntityManager manager, Entity entity, MmdDebugEntitySnapshot snapshot) {
			snapshot.HasSmoothing = manager.HasComponent<MmdInertialization>(entity) && manager.HasComponent<MmdInertializationState>(entity);
			if (!snapshot.HasSmoothing) {
				return;
			}
			var settings = manager.GetComponentData<MmdInertialization>(entity);
			var state = manager.GetComponentData<MmdInertializationState>(entity);
			snapshot.SmoothingCurrent = state.Revision == snapshot.Playback.Revision;
			snapshot.TransitionElapsed = math.max(0f, state.Elapsed);
			snapshot.PoseTransitionDuration = math.max(0f, state.PoseDuration);
			// Actions keep exact authored root deltas and bypass root smoothing.
			snapshot.RootTransitionDuration = snapshot.Action.ActionId == 0 ? math.max(0f, settings.RootDuration) : 0f;
			snapshot.PoseTransitionProgress = Progress(snapshot.TransitionElapsed, snapshot.PoseTransitionDuration);
			snapshot.RootTransitionProgress = Progress(snapshot.TransitionElapsed, snapshot.RootTransitionDuration);
			snapshot.TransitionActive = snapshot.SmoothingCurrent && snapshot.Playback.Revision != 0 &&
				(snapshot.PoseTransitionProgress < 1f || snapshot.RootTransitionProgress < 1f);
		}

		private static float Progress(float elapsed, float duration) {
			return duration <= 0f ? 1f : math.saturate(elapsed / duration);
		}

		private static string FilterGroupNames(ref MotionDatabase database, in DynamicBuffer<MmdMotionGroup> groups, MmdMotionGroupUsage usage, string empty) {
			var names = new List<string>();
			for (int index = 0; index < database.Groups.Length; index++) {
				int id = database.Groups[index].Id;
				if (MmdMotionGroups.Contains(groups, id, usage)) {
					names.Add(GroupName(ref database, id));
				}
			}
			return names.Count == 0 ? empty : string.Join(" / ", names);
		}

		private static string ClipGroupNames(ref MotionDatabase database, in MotionClip clip) {
			var names = new List<string>();
			foreach (int id in MotionDatabaseGroups.GetIds(ref database, clip)) {
				names.Add(GroupName(ref database, id));
			}
			return names.Count == 0 ? "Ungrouped" : string.Join(" / ", names);
		}

		private static string GroupName(ref MotionDatabase database, int id) {
			for (int index = 0; index < database.Groups.Length; index++) {
				if (database.Groups[index].Id == id) {
					return database.Groups[index].Name.ToString();
				}
			}
			return "Unmapped #" + id;
		}
	}
}
