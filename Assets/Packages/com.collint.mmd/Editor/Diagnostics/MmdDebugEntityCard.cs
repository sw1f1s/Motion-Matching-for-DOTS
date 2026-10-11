using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugEntityCard {
		private readonly Dictionary<string, Label> _values = new Dictionary<string, Label>();
		private readonly VisualElement _view;
		private readonly VisualTreeAsset _detailsTemplate;
		private readonly Foldout _details;
		private MmdDebugEntitySnapshot _snapshot;
		private bool _detailsCreated;

		public VisualElement View => _view;

		public MmdDebugEntityCard(VisualTreeAsset cardTemplate, VisualTreeAsset detailsTemplate) {
			_view = cardTemplate.CloneTree();
			_detailsTemplate = detailsTemplate;
			_details = _view.Q<Foldout>("detailsFoldout");
			_details.RegisterValueChangedCallback(change => {
				if (change.target == _details && change.newValue) {
					EnsureDetails();
					UpdateDetails(_snapshot);
				}
			});
			_view.Q<Button>("pingClip").clicked += PingClip;
			_view.Q<Button>("frameEntity").clicked += FrameEntity;
			_view.Q<Button>("selectEntity").clicked += SelectEntity;
		}

		public void Update(MmdDebugEntitySnapshot snapshot) {
			_snapshot = snapshot;
			UpdateSummary(snapshot);
			if (_details.value) {
				EnsureDetails();
				UpdateDetails(snapshot);
			}
		}

		private void EnsureDetails() {
			if (_detailsCreated) {
				return;
			}
			_detailsTemplate.CloneTree(_details.contentContainer);
			var history = _view.Q("history");
			for (int index = 0; index < MmdDebugPlaybackHistory.Capacity; index++) {
				var entry = new VisualElement { name = $"history{index}" };
				entry.AddToClassList("clip-history-entry");
				AddLabel(entry, "clip", "history-clip");
				AddLabel(entry, "info", "muted");
				history.Add(entry);
			}
			AddDataRows("playbackRows", new[] { "Clip index", "Frame", "Playback speed", "Match revision", "Matched database frame",
				"Next search", "Search interval", "Required groups", "Excluded groups", "Action" });
			AddDataRows("entityRows", new[] { "World", "Entity", "Position", "Physics velocity", "GPU rendering", "Database",
				"Database clips", "Database frames", "Skeleton bones", "Feature dimensions" });
			_detailsCreated = true;
		}

		private void UpdateSummary(MmdDebugEntitySnapshot entity) {
			_view.Q<Label>("entityTitle").text = entity.Name;
			_view.Q<Label>("entityIdentity").text = $"{entity.WorldLabel} · Entity {entity.Entity.Index}:{entity.Entity.Version}";
			_view.Q<Label>("clipTitle").text = entity.ClipName;
			_view.Q<Label>("groupLabel").text = "Group · " + entity.Groups;
			UpdateBadge(_view.Q<Label>("status"), entity);
			var progress = _view.Q<ProgressBar>("progress");
			progress.value = entity.Progress * 100f;
			progress.title = entity.ValidClip ? $"{entity.Progress * 100f:0.0}%" : "Unavailable";
			_view.Q<Label>("timeLabel").text = entity.ValidClip ? $"{entity.Playback.Time:0.000} / {entity.Duration:0.000} s" : "—";
			_view.Q<Label>("loopLabel").text = entity.Loop ? "LOOP" : "ONE-SHOT";
			_view.Q<Button>("pingClip").SetEnabled(entity.HasNames && !string.IsNullOrEmpty(entity.ClipPath));
			_view.Q<Button>("frameEntity").SetEnabled(entity.HasPosition && entity.World.IsCreated && entity.World.EntityManager.Exists(entity.Entity));
			_view.Q<Button>("selectEntity").SetEnabled(MmdDebugEntityNavigation.CanSelect(entity));
		}

		private void UpdateDetails(MmdDebugEntitySnapshot entity) {
			SetVisible(_view.Q("problem"), entity.Problem.Length > 0);
			_view.Q<Label>("problemText").text = entity.Problem;
			SetVisible(_view.Q("namesWarning"), !entity.HasNames && entity.ClipCount > 0);
			SetValue("Clip index", entity.Playback.ClipIndex.ToString());
			SetValue("Frame", entity.ValidClip ? entity.Frame.ToString() : "—");
			SetValue("Playback speed", $"{entity.Playback.Speed:0.00}×");
			SetValue("Match revision", entity.Playback.Revision.ToString());
			SetValue("Matched database frame", entity.Playback.MatchedFrame.ToString());
			SetValue("Next search", $"{Math.Max(0f, entity.Playback.SearchCountdown):0.000} s");
			SetValue("Search interval", $"{entity.Search.Interval:0.000} s");
			SetValue("Required groups", entity.RequiredGroups);
			SetValue("Excluded groups", entity.ExcludedGroups);
			SetValue("Action", entity.Action.ActionId == 0 ? "None" : $"#{entity.Action.ActionId} · {(entity.Action.Completed ? "Completed" : "Active")}{(entity.Action.Rejected ? " · Request rejected" : "")}");
			SetValue("World", entity.WorldLabel);
			SetValue("Entity", $"{entity.Entity.Index}:{entity.Entity.Version}{(entity.Prefab ? " · Prefab" : "")}{(entity.Disabled ? " · Disabled" : "")}");
			SetValue("Position", entity.HasPosition ? $"{entity.Position.x:0.00}, {entity.Position.y:0.00}, {entity.Position.z:0.00} m" : "Not attached");
			SetValue("Physics velocity", entity.HasVelocity ? $"{entity.Velocity.x:0.00}, {entity.Velocity.y:0.00}, {entity.Velocity.z:0.00} m/s" : "Not attached");
			SetValue("GPU rendering", entity.RenderStatus);
			SetValue("Database", entity.HasNames ? entity.DatabasePath : "No editor catalog");
			SetValue("Database clips", entity.ClipCount.ToString());
			SetValue("Database frames", entity.FrameCount.ToString("N0"));
			SetValue("Skeleton bones", entity.BoneCount.ToString());
			SetValue("Feature dimensions", entity.FeatureCount.ToString());
			UpdateTransition(entity);
			UpdateHistory(entity);
		}

		private void UpdateTransition(MmdDebugEntitySnapshot entity) {
			bool known = entity.HasObservedTransition || entity.TransitionActive;
			string source = entity.HasObservedTransition ? entity.TransitionFrom : "Previous pose (not observed)";
			_view.Q<Label>("transitionRoute").text = known ? $"{source}\n→ {entity.ClipName}" : "No transition observed yet";
			var state = _view.Q<Label>("transitionStatus");
			state.text = !known ? "—" : !entity.HasSmoothing ? "Immediate" : !entity.SmoothingCurrent ? "Awaiting pose" : entity.TransitionActive ? "Smoothing" : "Complete";
			state.EnableInClassList("warning-state", entity.TransitionActive);
			bool available = entity.HasSmoothing && entity.SmoothingCurrent && known;
			var pose = _view.Q<ProgressBar>("poseTransition");
			pose.value = entity.PoseTransitionProgress * 100f;
			pose.title = !available ? "—" : entity.PoseTransitionDuration <= 0f ? "Immediate" : $"{pose.value:0}% · {Math.Min(entity.TransitionElapsed, entity.PoseTransitionDuration):0.000} / {entity.PoseTransitionDuration:0.000} s";
			var root = _view.Q<ProgressBar>("rootTransition");
			root.value = entity.RootTransitionProgress * 100f;
			root.title = !available ? "—" : entity.Action.ActionId != 0 ? "Authored action · no smoothing" : entity.RootTransitionDuration <= 0f ? "Immediate" : $"{root.value:0}% · {Math.Min(entity.TransitionElapsed, entity.RootTransitionDuration):0.000} / {entity.RootTransitionDuration:0.000} s";
			pose.SetEnabled(available);
			root.SetEnabled(available);
			string note = entity.SameClipTransition ? "Same clip reselected at a new time." : "Pose and root motion smoothing use independent durations.";
			if (!entity.HasSmoothing) {
				note = "Transition smoothing is not attached to this entity.";
			}
			if (entity.HasObservedTransition && entity.TransitionRevisionGap > 1) {
				note += $" {entity.TransitionRevisionGap} revisions occurred between observations; the source shown is the last observed clip.";
			}
			_view.Q<Label>("transitionNote").text = note;
		}

		private void UpdateHistory(MmdDebugEntitySnapshot entity) {
			var history = _view.Q("history");
			for (int index = 0; index < MmdDebugPlaybackHistory.Capacity; index++) {
				var row = history.Q($"history{index}");
				bool visible = index < entity.ClipHistory.Length;
				SetVisible(row, visible);
				if (!visible) {
					continue;
				}
				var clip = entity.ClipHistory[index];
				row.EnableInClassList("history-current", index == 0);
				row.Q<Label>("clip").text = $"{(index == 0 ? "CURRENT" : $"−{index}")} · {clip.ClipName}";
				row.Q<Label>("info").text = $"{clip.ObservedAt} · {clip.Groups} · entry {clip.EntryTime:0.000}s · r{clip.Revision}";
			}
		}

		private void AddDataRows(string parent, string[] names) {
			var container = _view.Q(parent);
			foreach (string name in names) {
				var row = new VisualElement();
				row.AddToClassList("data-row");
				var key = new Label(name);
				key.AddToClassList("data-key");
				var value = new Label("—");
				value.AddToClassList("data-value");
				row.Add(key);
				row.Add(value);
				container.Add(row);
				_values.Add(name, value);
			}
		}

		private void SetValue(string key, string value) {
			_values[key].text = value;
		}

		private void PingClip() {
			if (_snapshot == null || !_snapshot.HasNames) {
				return;
			}
			foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(_snapshot.ClipPath)) {
				if (asset is AnimationClip clip && clip.name == _snapshot.ClipName) {
					Selection.activeObject = clip;
					EditorGUIUtility.PingObject(clip);
					return;
				}
			}
		}

		private void FrameEntity() {
			if (_snapshot == null || !_snapshot.World.IsCreated || !_snapshot.World.EntityManager.Exists(_snapshot.Entity)) {
				return;
			}
			if (SceneView.lastActiveSceneView != null) {
				Unity.Mathematics.float3 position = _snapshot.Position;
				SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(position.x, position.y + 0.9f, position.z), Vector3.one * 3f), false);
			}
		}

		private void SelectEntity() {
			MmdDebugEntityNavigation.Select(_snapshot);
		}

		private static void AddLabel(VisualElement parent, string name, string className) {
			var label = new Label { name = name };
			label.AddToClassList(className);
			parent.Add(label);
		}

		private static void UpdateBadge(Label label, MmdDebugEntitySnapshot entity) {
			label.text = entity.Status;
			label.EnableInClassList("fault-state", entity.Problem.Length > 0);
			label.EnableInClassList("warning-state", entity.Disabled || entity.Paused || entity.Prefab);
		}

		private static void SetVisible(VisualElement element, bool visible) {
			element.EnableInClassList("hidden", !visible);
		}

	}
}
