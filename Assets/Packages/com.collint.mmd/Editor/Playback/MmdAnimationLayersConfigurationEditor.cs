using System;
using System.Collections.Generic;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Diagnostics;
using ColInt.MMD.Playback.Authoring;
using ColInt.MMD.Playback.Data;
using ColInt.MMD.Playback.Helpers;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Playback {
	[CustomEditor(typeof(MmdAnimationLayersConfiguration))]
	public sealed class MmdAnimationLayersConfigurationEditor : UnityEditor.Editor {
		private readonly Dictionary<string, bool> _expanded = new Dictionary<string, bool>();
		private BlobAssetReference<MotionDatabase> _database;
		private TextAsset _source;
		private MmdDebugClipCatalog _catalog;
		private VisualElement _list;
		private HelpBox _validation;
		private int _count;

		public override VisualElement CreateInspectorGUI() {
			var root = MmdAnimationInspectorUi.CreateRoot(this);
			var databaseRow = Row();
			AddField(databaseRow, new ObjectField("Baked Database") {
				objectType = typeof(TextAsset), allowSceneObjects = false,
				tooltip = "Baked database that provides the layer clips and groups. Must match the character database."
			}, serializedObject.FindProperty("_database"));
			databaseRow.Bind(serializedObject);
			root.Add(databaseRow);
			root.Add(new HelpBox("Layers override masked bones. Layers lower in the list have higher priority.", HelpBoxMessageType.Info));
			databaseRow.Add(new Button(() => {
				ReleaseDatabase();
				Rebuild();
			}) { text = "Reload", tooltip = "Reload the database after baking and refresh the available groups and clips." });
			_validation = new HelpBox(string.Empty, HelpBoxMessageType.Error);
			root.Add(_validation);
			_list = new VisualElement();
			root.Add(_list);
			var add = new Button(AddLayer) { text = "Add Layer", tooltip = "Add a layer over the base animation. Select its source, bone mask, and base groups to activate it." };
			add.AddToClassList("mmd-animation-block");
			root.Add(add);
			root.TrackSerializedObjectValue(serializedObject, changed => {
				var configuration = (MmdAnimationLayersConfiguration)target;
				if (_source != configuration.Database || _count != configuration.Layers.Length) {
					Rebuild();
				} else {
					Validate();
				}
			});
			root.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
			Rebuild();
			return root;
		}

		private void OnDisable() {
			ReleaseDatabase();
		}

		private void ReleaseDatabase() {
			if (_database.IsCreated) {
				_database.Dispose();
			}
			_database = default;
			_source = null;
			_catalog = null;
		}

		private void Rebuild() {
			serializedObject.Update();
			_list.Clear();
			var configuration = (MmdAnimationLayersConfiguration)target;
			if (_source != configuration.Database) {
				ReleaseDatabase();
				_source = configuration.Database;
				if (_source != null) {
					try {
						_database = MotionDatabaseCodec.Load(_source.GetData<byte>());
						var store = new MmdDebugCatalogStore();
						store.Reload();
						_catalog = store.Resolve(_database);
					} catch (Exception exception) when (exception is ArgumentException || exception is System.IO.IOException) {
						_validation.text = exception.Message;
						_validation.style.display = DisplayStyle.Flex;
					}
				}
			}
			SerializedProperty layers = serializedObject.FindProperty("_layers");
			_count = layers.arraySize;
			for (int index = 0; index < layers.arraySize; index++) {
				AddRow(layers.GetArrayElementAtIndex(index), index);
			}
			_list.Bind(serializedObject);
			Validate();
		}

		private void AddRow(SerializedProperty layer, int index) {
			var name = Property(layer, "Name");
			var foldout = RememberFoldout(layer.propertyPath, $"Layer {index + 1}: {name.stringValue}", true);
			foldout.tooltip = "Animation layer settings. Layers apply from top to bottom; lower layers have higher priority on shared bones.";
			foldout.AddToClassList("mmd-animation-block");
			var header = Row();
			header.AddToClassList("mmd-animation-layer-header");
			header.Add(foldout.Q<Toggle>());
			foldout.hierarchy.Insert(0, header);
			var toolbar = Row();
			toolbar.AddToClassList("mmd-animation-actions");
			var up = new Button(() => Move(index, index - 1)) { text = "↑", tooltip = "Move the layer up so it applies earlier with lower priority." };
			up.SetEnabled(index > 0);
			var down = new Button(() => Move(index, index + 1)) { text = "↓", tooltip = "Move the layer down so it applies later with higher priority." };
			down.SetEnabled(index < _count - 1);
			toolbar.Add(up);
			toolbar.Add(down);
			toolbar.Add(new Button(() => Remove(index)) { text = "Remove", tooltip = "Remove this layer. Use Undo to restore it." });
			header.Add(toolbar);
			AddField(foldout, new TextField("Name") { tooltip = "Unique layer name used to identify and control it at runtime. The name itself does not affect animation." }, name);
			foldout.TrackPropertyValue(name, changed => foldout.text = $"Layer {index + 1}: {changed.stringValue}");
			var mask = Property(layer, "Mask");
			var maskRow = Row();
			AddField(maskRow, new ObjectField("Bone Mask") {
				objectType = typeof(MmdAnimationMask), allowSceneObjects = false,
				tooltip = "Controls layer influence on each bone: 0 keeps the base pose, 1 uses the layer pose. The skeleton must match the database."
			}, mask);
			var edit = new Button(() => {
				if (mask.objectReferenceValue != null) {
					Selection.activeObject = mask.objectReferenceValue;
					EditorGUIUtility.PingObject(mask.objectReferenceValue);
				}
			}) { text = "Edit", tooltip = "Open the mask editor to adjust bone weights or apply arm and upper body presets." };
			edit.SetEnabled(mask.objectReferenceValue != null);
			maskRow.Add(edit);
			maskRow.TrackPropertyValue(mask, changed => edit.SetEnabled(changed.objectReferenceValue != null));
			foldout.Add(maskRow);
			AddClipSelection(foldout, layer);
			bool groupSource = Property(layer, "Source").enumValueIndex == (int)MmdAnimationLayerSource.Group;
			if (!groupSource) {
				AddField(foldout, new FloatField("Time (s)") { isDelayed = true, tooltip = "Initial clip time in seconds. With Speed = 0, the layer holds the pose at this time. Looping clips wrap; other clips are limited to their duration." }, Property(layer, "Time"));
			}
			AddField(foldout, new FloatField("Speed") { isDelayed = true, tooltip = "Layer playback speed multiplier: 1 uses the original speed, 0 holds the selected clip pose. A Motion Matching group requires a value greater than 0." }, Property(layer, "Speed"));
			AddField(foldout, new Slider("Weight", 0f, 1f) { showInputField = true, tooltip = "Overall layer weight from 0 to 1, multiplied by each bone weight in the mask. 0 keeps the base animation; 1 fully applies the layer to enabled bones." }, Property(layer, "Weight"));
			AddField(foldout, new FloatField("Fade Duration (s)") { isDelayed = true, tooltip = "Time in seconds to fade across the full 0–1 weight range when enabling, disabling, or changing the layer weight. 0 switches instantly." }, Property(layer, "FadeDuration"));
			AddField(foldout, new EnumFlagsField("Channels", MotionPoseChannels.Rotation) { tooltip = "Local bone properties replaced by the layer: Position, Rotation, and Scale. Rotation is usually sufficient for holding a weapon. Root motion comes from the base animation." }, Property(layer, "Channels"));
			AddGroupMask(foldout, layer);
			AddField(foldout, new FloatField("Transition (s)") { isDelayed = true, tooltip = "Pose smoothing when Play, SetGroup or Motion Matching selects another clip/frame. Fade Duration controls the overall layer weight." }, Property(Property(layer, "Matching"), "TransitionDuration"));
			if (groupSource) {
				var matchingFoldout = RememberFoldout(layer.propertyPath + ".matching", "Motion Matching Settings", false);
				matchingFoldout.tooltip = "Frame selection settings for the source group. These control animation matching and smoothing for this layer only.";
				var matching = Property(layer, "Matching");
				AddField(matchingFoldout, new FloatField("Interval (s)") { isDelayed = true, tooltip = "Interval between frame searches in seconds. Smaller values search more often and cost more processing time. 0 allows a search every simulation step when no transition is active." }, Property(matching, "Interval"));
				AddField(matchingFoldout, new FloatField("Switch Penalty") { isDelayed = true, tooltip = "Additional cost for switching to another frame. Higher values favor continuing the current clip." }, Property(matching, "SwitchPenalty"));
				AddField(matchingFoldout, new FloatField("Min. Remaining (s)") { isDelayed = true, tooltip = "Minimum time remaining for a candidate in a non-looping clip. Rejects frames too close to the end. Does not affect looping clips." }, Property(matching, "MinimumRemainingTime"));
				AddField(matchingFoldout, new FloatField("Min. Switch (s)") { isDelayed = true, tooltip = "Minimum time difference when switching to another frame in the same clip. Prevents repeated jumps to nearby frames and accounts for the loop boundary." }, Property(matching, "MinimumSwitchTime"));
				foldout.Add(matchingFoldout);
			}
			_list.Add(foldout);
		}

		private void AddClipSelection(VisualElement row, SerializedProperty layer) {
			var source = Property(layer, "Source");
			var selectSource = new PopupField<string>("Source", new List<string> { "Clip", "Group (Motion Matching)" }, source.enumValueIndex);
			selectSource.tooltip = "Layer source: Clip uses one selected clip; Group uses Motion Matching to select a clip and frame from the entire group based on character movement.";
			MmdAnimationInspectorUi.StyleField(selectSource);
			selectSource.RegisterValueChangedCallback(evt => {
				source.enumValueIndex = selectSource.index;
				var speed = Property(layer, "Speed");
				if (source.enumValueIndex == (int)MmdAnimationLayerSource.Group && speed.floatValue <= 0f) {
					speed.floatValue = 1f;
				}
				serializedObject.ApplyModifiedProperties();
				Rebuild();
			});
			row.Add(selectSource);
			row.TrackPropertyValue(source, changed => {
				if (selectSource.index != changed.enumValueIndex) {
					Rebuild();
				}
			});
			bool groupSource = source.enumValueIndex == (int)MmdAnimationLayerSource.Group;
			var group = Property(layer, "Group");
			var ordinal = Property(layer, "ClipInGroup");
			if (!_database.IsCreated) {
				AddField(row, new TextField("Animation Group") { tooltip = "Name of the source group in the baked database. This selects the animation source; base groups that activate the layer are selected separately." }, group);
				if (!groupSource) {
					AddField(row, new IntegerField("Clip In Group") { tooltip = "Clip index within the source group, starting at 0. Check this index after reordering clips during baking." }, ordinal);
				}
				return;
			}
			var choices = new List<string> { string.Empty };
			for (int index = 0; index < _database.Value.Groups.Length; index++) {
				choices.Add(_database.Value.Groups[index].Name.ToString());
			}
			if (!choices.Contains(group.stringValue)) {
				choices.Add(group.stringValue);
			}
			var selectGroup = new PopupField<string>("Animation Group", choices, group.stringValue) { tooltip = "Source group in the baked database. Clip uses one of its clips; Group matches across all its clips. Select the base groups that activate the layer below." };
			selectGroup.RegisterValueChangedCallback(evt => {
				group.stringValue = evt.newValue;
				ordinal.intValue = 0;
				serializedObject.ApplyModifiedProperties();
				Rebuild();
			});
			MmdAnimationInspectorUi.StyleField(selectGroup);
			row.Add(selectGroup);
			row.TrackPropertyValue(group, changed => {
				if (selectGroup.value != changed.stringValue) {
					Rebuild();
				}
			});
			var clips = new List<string>();
			int groupId = 0;
			for (int candidate = 0; candidate < _database.Value.Groups.Length; candidate++) {
				if (_database.Value.Groups[candidate].Name.ToString() == group.stringValue) {
					groupId = _database.Value.Groups[candidate].Id;
					break;
				}
			}
			for (int index = 0; index < _database.Value.Clips.Length; index++) {
				if (MotionDatabaseGroups.Contains(ref _database.Value, _database.Value.Clips[index], groupId)) {
					string name = _catalog != null ? _catalog.Names[index] : $"Database clip {index}";
					clips.Add($"{clips.Count}: {name}");
				}
			}
			if (groupSource) {
				selectGroup.tooltip += $" This group contains {clips.Count} clips.";
				MmdAnimationInspectorUi.StyleField(selectGroup);
				return;
			}
			if (clips.Count == 0 || ordinal.intValue < 0 || ordinal.intValue >= clips.Count) {
				AddField(row, new IntegerField("Clip In Group") { tooltip = "Clip index within the source group, starting at 0. Check this index after reordering clips during baking." }, ordinal);
				return;
			}
			var selectClip = new PopupField<string>("Clip", clips, ordinal.intValue) { tooltip = "Specific clip from the source group. With Speed = 0, holds the selected pose. With Speed > 0, plays independently of the base animation." };
			selectClip.RegisterValueChangedCallback(evt => {
				ordinal.intValue = selectClip.index;
				serializedObject.ApplyModifiedProperties();
			});
			row.TrackPropertyValue(ordinal, changed => {
				if (changed.intValue >= 0 && changed.intValue < clips.Count) {
					selectClip.SetValueWithoutNotify(clips[changed.intValue]);
				}
			});
			MmdAnimationInspectorUi.StyleField(selectClip);
			row.Add(selectClip);
		}

		private void AddGroupMask(VisualElement row, SerializedProperty layer) {
			var groups = Property(layer, "ActiveGroups");
			if (!_database.IsCreated) {
				row.Add(new PropertyField(groups, "Base Groups") { tooltip = "Names of base animation groups that activate this layer. An empty list disables the layer." });
				return;
			}
			var choices = new List<string>();
			for (int index = 0; index < _database.Value.Groups.Length; index++) {
				if (_database.Value.Groups[index].Mode != MotionPlaybackMode.LayerOnly) {
					choices.Add(_database.Value.Groups[index].Name.ToString());
				}
			}
			for (int index = 0; index < groups.arraySize; index++) {
				string name = groups.GetArrayElementAtIndex(index).stringValue;
				if (!choices.Contains(name)) {
					choices.Add(name);
				}
			}
			for (int first = 0; first < choices.Count; first += 64) {
				var page = choices.GetRange(first, Math.Min(64, choices.Count - first));
				string label = choices.Count <= 64 ? "Base Groups" : $"Groups {first + 1}–{first + page.Count}";
				var field = new Mask64Field(label, page, GroupMask(groups, page)) {
					tooltip = "Base animation groups that activate this layer. Everything selects all groups in this field; Nothing clears them. The current base clip must belong to at least one selected group. New groups are not selected automatically."
				};
				MmdAnimationInspectorUi.StyleField(field);
				field.RegisterValueChangedCallback(evt => {
					var selected = new HashSet<string>(StringComparer.Ordinal);
					for (int index = 0; index < groups.arraySize; index++) {
						selected.Add(groups.GetArrayElementAtIndex(index).stringValue);
					}
					for (int index = 0; index < page.Count; index++) {
						if ((evt.newValue & (1UL << index)) != 0) {
							selected.Add(page[index]);
						} else {
							selected.Remove(page[index]);
						}
					}
					SetActiveGroups(groups, choices.FindAll(selected.Contains));
				});
				field.TrackPropertyValue(groups, changed => field.SetValueWithoutNotify(GroupMask(changed, page)));
				row.Add(field);
			}
		}

		private void SetActiveGroups(SerializedProperty property, IList<string> groups) {
			property.arraySize = groups.Count;
			for (int index = 0; index < groups.Count; index++) {
				property.GetArrayElementAtIndex(index).stringValue = groups[index];
			}
			serializedObject.ApplyModifiedProperties();
		}

		private void Validate() {
			try {
				if (!_database.IsCreated) {
					throw new ArgumentException("Choose a valid baked MMD database.");
				}
				using var definition = MmdAnimationLayerBuilder.Build(_database, ((MmdAnimationLayersConfiguration)target).Layers);
				_validation.style.display = DisplayStyle.None;
			} catch (ArgumentException exception) {
				_validation.text = exception.Message;
				_validation.style.display = DisplayStyle.Flex;
			}
		}

		private void AddLayer() {
			Undo.RecordObject(target, "Add animation layer");
			var configuration = (MmdAnimationLayersConfiguration)target;
			var layers = new MmdAnimationLayerSettings[configuration.Layers.Length + 1];
			Array.Copy(configuration.Layers, layers, configuration.Layers.Length);
			layers[layers.Length - 1] = new MmdAnimationLayerSettings { Name = "Layer " + layers.Length };
			if (configuration.Database == null) {
				_validation.text = "Choose a baked database before adding layers.";
				_validation.style.display = DisplayStyle.Flex;
				return;
			}
			configuration.Configure(configuration.Database, layers);
			EditorUtility.SetDirty(target);
			Rebuild();
		}

		private void Remove(int index) {
			serializedObject.FindProperty("_layers").DeleteArrayElementAtIndex(index);
			serializedObject.ApplyModifiedProperties();
			Rebuild();
		}

		private void Move(int from, int to) {
			serializedObject.FindProperty("_layers").MoveArrayElement(from, to);
			serializedObject.ApplyModifiedProperties();
			Rebuild();
		}

		private Foldout RememberFoldout(string key, string text, bool defaultValue) {
			bool value = _expanded.TryGetValue(key, out bool stored) ? stored : defaultValue;
			var foldout = new Foldout { text = text, value = value };
			foldout.RegisterValueChangedCallback(evt => {
				if (evt.target == foldout) {
					_expanded[key] = evt.newValue;
				}
			});
			return foldout;
		}

		private static void AddField<T>(VisualElement row, BaseField<T> field, SerializedProperty property) {
			field.bindingPath = property.propertyPath;
			MmdAnimationInspectorUi.StyleField(field);
			row.Add(field);
		}

		private static VisualElement Row() {
			return MmdAnimationInspectorUi.Row();
		}

		private static ulong GroupMask(SerializedProperty groups, IList<string> choices) {
			ulong mask = 0;
			for (int index = 0; index < groups.arraySize; index++) {
				int bit = choices.IndexOf(groups.GetArrayElementAtIndex(index).stringValue);
				if (bit >= 0) {
					mask |= 1UL << bit;
				}
			}
			return mask;
		}

		private static SerializedProperty Property(SerializedProperty layer, string name) {
			return layer.FindPropertyRelative("<" + name + ">k__BackingField");
		}
	}
}
