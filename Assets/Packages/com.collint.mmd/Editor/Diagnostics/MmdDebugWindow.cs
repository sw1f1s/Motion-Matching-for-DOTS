using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Unity.Entities;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugWindow : EditorWindow {
		private const string AllWorlds = "All worlds";
		private const double RefreshInterval = 0.2d;

		private readonly Dictionary<(ulong World, Entity Entity), MmdDebugEntityCard> _cards = new Dictionary<(ulong World, Entity Entity), MmdDebugEntityCard>();
		private readonly HashSet<(ulong World, Entity Entity)> _live = new HashSet<(ulong World, Entity Entity)>();
		private MmdDebugSnapshotReader _reader;
		private ScrollView _list;
		private DropdownField _worldFilter;
		private ToolbarSearchField _search;
		private ToolbarToggle _freeze;
		private VisualElement _view;
		private VisualTreeAsset _cardTemplate;
		private VisualTreeAsset _detailsTemplate;
		private double _nextRefresh;

		public void CreateGUI() {
			string assetDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\', '/') + "/";
			rootVisualElement.Clear();
			AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetDirectory + "MmdDebugWindow.uxml").CloneTree(rootVisualElement);
			_view = rootVisualElement;
			_list = _view.Q<ScrollView>("entities");
			_cardTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetDirectory + "MmdDebugEntityCard.uxml");
			_detailsTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetDirectory + "MmdDebugEntityDetails.uxml");
			_cards.Clear();
			_worldFilter = _view.Q<DropdownField>("worldFilter");
			_worldFilter.choices = new List<string> { AllWorlds };
			_worldFilter.SetValueWithoutNotify(AllWorlds);
			_search = _view.Q<ToolbarSearchField>("search");
			_freeze = _view.Q<ToolbarToggle>("freeze");
			_worldFilter.RegisterValueChangedCallback(change => ApplyFilters());
			_search.RegisterValueChangedCallback(change => ApplyFilters());
			_view.Q<ToolbarButton>("refresh").clicked += RefreshCatalogs;
			RefreshData();
		}

		private void OnEnable() {
			_reader = new MmdDebugSnapshotReader();
			_reader.ReloadCatalogs();
			EditorApplication.update += UpdateWindow;
			EditorApplication.projectChanged += RefreshCatalogs;
			EditorApplication.playModeStateChanged += PlayModeChanged;
		}

		private void OnDisable() {
			EditorApplication.update -= UpdateWindow;
			EditorApplication.projectChanged -= RefreshCatalogs;
			EditorApplication.playModeStateChanged -= PlayModeChanged;
			_reader.Dispose();
			_reader = null;
			_cards.Clear();
		}

		private void UpdateWindow() {
			if (_view == null || _freeze.value || EditorApplication.isCompiling || EditorApplication.isUpdating ||
				EditorApplication.timeSinceStartup < _nextRefresh) {
				return;
			}
			RefreshData();
		}

		private void PlayModeChanged(PlayModeStateChange state) {
			_nextRefresh = 0d;
		}

		private void RefreshCatalogs() {
			_reader.ReloadCatalogs();
			if (_view != null && !EditorApplication.isCompiling) {
				RefreshData();
			}
		}

		private void RefreshData() {
			_nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
			_reader.Read();
			var choices = new List<string> { AllWorlds };
			foreach (var entity in _reader.Entities) {
				if (!choices.Contains(entity.WorldLabel)) {
					choices.Add(entity.WorldLabel);
				}
			}
			if (!_worldFilter.choices.SequenceEqual(choices)) {
				string selected = _worldFilter.value;
				_worldFilter.choices = choices;
				_worldFilter.SetValueWithoutNotify(choices.Contains(selected) ? selected : AllWorlds);
			}
			ApplyFilters();
		}

		private void ApplyFilters() {
			string text = _search.value;
			_live.Clear();
			int visible = 0;
			foreach (var entity in _reader.Entities) {
				var key = (entity.World.SequenceNumber, entity.Entity);
				_live.Add(key);
				bool matches = (_worldFilter.value == AllWorlds || _worldFilter.value == entity.WorldLabel) &&
					(string.IsNullOrWhiteSpace(text) || Matches(entity, text));
				if (!_cards.TryGetValue(key, out var card)) {
					if (!matches) {
						continue;
					}
					card = new MmdDebugEntityCard(_cardTemplate, _detailsTemplate);
					_cards.Add(key, card);
					_list.Add(card.View);
				}
				card.View.EnableInClassList("hidden", !matches);
				if (matches) {
					card.Update(entity);
					visible++;
				}
			}
			foreach (var key in _cards.Keys.Where(key => !_live.Contains(key)).ToArray()) {
				_cards[key].View.RemoveFromHierarchy();
				_cards.Remove(key);
			}
			_view.Q("emptyList").EnableInClassList("hidden", visible > 0);
		}

		[MenuItem("Window/Motion Matching for DOTS/Debugger")]
		public static void Open() {
			var window = GetWindow<MmdDebugWindow>();
			window.titleContent = new GUIContent("MMD Debugger");
			window.minSize = new Vector2(720f, 420f);
			window.Show();
		}

		private static bool Matches(MmdDebugEntitySnapshot entity, string text) {
			return entity.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
				entity.Entity.ToString().IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
				entity.ClipName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
				entity.Groups.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
				entity.Status.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
