using System;
using ColInt.MMD.Playback.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Playback {
	[CustomEditor(typeof(MmdAnimationMask))]
	public sealed class MmdAnimationMaskEditor : UnityEditor.Editor {
		private const string BoneWeightTooltip = "Layer influence on this bone: 0 keeps the base animation, 1 uses the layer pose, and intermediate values blend them. Multiplied by the layer Weight. When Apply weight to children is enabled, changes also affect child bones.";

		private MultiColumnTreeView _tree;
		private Label _summary;
		private HelpBox _message;
		private ToolbarSearchField _search;
		private bool _branch;
		private string[] _paths = Array.Empty<string>();
		public override VisualElement CreateInspectorGUI() {
			var root = MmdAnimationInspectorUi.CreateRoot(this);
			var rig = new ObjectField("Target Rig") {
				objectType = typeof(GameObject), allowSceneObjects = false, bindingPath = "_rig",
				tooltip = "Model or prefab whose skeleton provides the mask bone list. Use the same target skeleton as the baked animation database."
			};
			MmdAnimationInspectorUi.StyleField(rig);
			rig.RegisterValueChangedCallback(evt => {
				serializedObject.Update();
				serializedObject.FindProperty("_rig").objectReferenceValue = evt.newValue;
				serializedObject.ApplyModifiedProperties();
				if (evt.newValue != null) {
					Run(() => MmdAnimationMaskEditing.Synchronize((MmdAnimationMask)target));
				}
			});
			root.Add(rig);
			root.Bind(serializedObject);
			root.Add(new HelpBox("0 keeps locomotion; 1 uses the layer pose. Start with the weapon arm, including its fingers. Keep root, pelvis and legs at 0. Use the same target rig as the database.", HelpBoxMessageType.Info));
			var toolbar = MmdAnimationInspectorUi.Row();
			toolbar.Add(new Button(() => Run(() => MmdAnimationMaskEditing.Synchronize((MmdAnimationMask)target))) { text = "Sync Rig", tooltip = "Update the bone list from Target Rig. Existing bone paths keep their weights; new bones receive a weight of 0." });
			toolbar.Add(new Button(() => Run(() => MmdAnimationMaskEditing.SetWeight((MmdAnimationMask)target, null, 0f, false))) { text = "Clear", tooltip = "Set all bone weights to 0 so the mask keeps the base pose." });
			toolbar.Add(new Button(() => Run(() => MmdAnimationMaskEditing.SetWeight((MmdAnimationMask)target, null, 1f, false))) { text = "All", tooltip = "Set all bone weights to 1 so the layer affects the entire skeleton within the selected Channels." });
			toolbar.Add(new Button(() => Run(() => MmdAnimationMaskEditing.SetHumanoidPreset((MmdAnimationMask)target, HumanBodyBones.RightShoulder))) { text = "Right Arm", tooltip = "Set the right shoulder, arm, hand, and fingers to a weight of 1. Set all other bones to 0. Requires a Humanoid Avatar." });
			toolbar.Add(new Button(() => Run(() => MmdAnimationMaskEditing.SetHumanoidPreset((MmdAnimationMask)target, HumanBodyBones.LeftShoulder))) { text = "Left Arm", tooltip = "Set the left shoulder, arm, hand, and fingers to a weight of 1. Set all other bones to 0. Requires a Humanoid Avatar." });
			toolbar.Add(new Button(() => Run(() => MmdAnimationMaskEditing.SetHumanoidPreset((MmdAnimationMask)target, HumanBodyBones.Spine))) { text = "Upper Body", tooltip = "Enable the upper body from the spine, including the arms and head. Keep the pelvis, legs, and root at a weight of 0. Requires a Humanoid Avatar." });
			root.Add(toolbar);
			var branch = new Toggle { text = "Apply weight to children", value = true, tooltip = "Apply bone weight changes to all descendants. When disabled, only the selected bone changes." };
			_branch = true;
			branch.RegisterValueChangedCallback(evt => _branch = evt.newValue);
			root.Add(branch);
			_search = new ToolbarSearchField();
			_search.tooltip = "Find bones by name or full path. Filtering only changes the display; hidden bones remain in the mask.";
			_search.RegisterValueChangedCallback(evt => Rebuild());
			_search.style.flexGrow = 1;
			_search.style.minWidth = 0;
			_search.style.flexBasis = 0;
			var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			searchRow.Add(_search);
			searchRow.Add(new Button(() => _tree.ExpandAll()) { text = "Expand", tooltip = "Expand all branches of the displayed bone hierarchy." });
			searchRow.Add(new Button(() => _tree.CollapseAll()) { text = "Collapse", tooltip = "Collapse all branches of the bone hierarchy. Bone weights are preserved." });
			root.Add(searchRow);
			_message = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
			root.Add(_message);
			var columns = new Columns { stretchMode = Columns.StretchMode.GrowAndFill, reorderable = false, primaryColumnName = "bone" };
			columns.Add(new Column {
				name = "bone", title = "Bone", width = 270, minWidth = 130, stretchable = true,
				makeCell = MakeBone,
				bindCell = BindBone
			});
			columns.Add(new Column {
				name = "weight", title = "Weight", width = 140, minWidth = 55, stretchable = true,
				makeCell = MakeSlider, bindCell = BindSlider
			});
			columns.Add(new Column {
				name = "value", title = "Value", width = 62, minWidth = 62, maxWidth = 62, stretchable = false, resizable = false,
				makeCell = MakeNumber, bindCell = BindNumber
			});
			_tree = new MultiColumnTreeView(columns) {
				fixedItemHeight = 24, autoExpand = true, selectionType = SelectionType.Single,
				showBorder = true, showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
				horizontalScrollingEnabled = false
			};
			_tree.tooltip = "Bone shows the bone and its place in the hierarchy. Weight controls layer influence with a slider. Value sets the exact weight from 0 to 1. Bones with a nonzero weight are shown in green.";
			_tree.style.height = 480;
			_tree.style.minWidth = 0;
			_tree.style.marginTop = 6;
			_tree.style.marginRight = 4;
			root.Add(_tree);
			_summary = new Label { tooltip = "Number of bones with a weight above 0 in the entire mask, including bones hidden by the filter. Final influence also depends on the layer Weight.", style = { marginTop = 4 } };
			root.Add(_summary);
			root.TrackSerializedObjectValue(serializedObject, changed => RefreshWeights());
			root.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
			Rebuild();
			return root;
		}

		private void Run(Action action) {
			try {
				action();
				serializedObject.Update();
				Rebuild();
			} catch (ArgumentException exception) {
				_message.text = exception.Message;
				_message.style.display = DisplayStyle.Flex;
			}
		}

		private void Rebuild() {
			var mask = (MmdAnimationMask)target;
			_paths = new string[mask.Bones.Length];
			for (int index = 0; index < _paths.Length; index++) {
				_paths[index] = mask.Bones[index].Path;
			}
			bool ready = mask.Rig != null && mask.Bones.Length > 0;
			_message.text = ready ? string.Empty : "Choose a target rig and press Sync Rig to build the bone tree.";
			_message.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
			_tree.SetEnabled(ready);
			_tree.SetRootItems(MmdAnimationMaskTree.Build(mask.Bones, _search.value));
			_tree.Rebuild();
			UpdateSummary();
		}

		private VisualElement MakeBone() {
			var label = new Label { style = {
				minWidth = 0, flexGrow = 1, flexShrink = 1, height = Length.Percent(100), unityTextAlign = TextAnchor.MiddleLeft,
				overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap
			} };
			label.RegisterCallback<GeometryChangedEvent>(evt => UpdateIndent(label));
			return label;
		}

		private void UpdateIndent(Label label) {
			if (label.parent == null || label.parent.parent == null || !(label.userData is int depth)) {
				return;
			}
			label.parent.style.minWidth = 0;
			label.parent.style.flexShrink = 1;
			var indent = label.parent.parent.Q<VisualElement>("unity-tree-view__item-indent");
			if (indent != null) {
				indent.style.width = depth * 8;
			}
		}

		private VisualElement MakeSlider() {
			var slider = new Slider(0f, 1f) { tooltip = BoneWeightTooltip };
			slider.style.flexGrow = 1;
			slider.style.minWidth = 0;
			slider.style.marginLeft = 6;
			slider.style.marginRight = 8;
			slider.RegisterValueChangedCallback(evt => ChangeWeight((string)slider.userData, evt.newValue));
			return slider;
		}

		private VisualElement MakeNumber() {
			var number = new FloatField { isDelayed = true };
			number.style.minWidth = 0;
			number.style.marginLeft = 2;
			number.style.marginRight = 6;
			number.RegisterValueChangedCallback(evt => ChangeWeight((string)number.userData, evt.newValue));
			return number;
		}

		private void BindBone(VisualElement element, int index) {
			var item = _tree.GetItemDataForIndex<MmdAnimationMaskTreeItem>(index);
			var value = ((MmdAnimationMask)target).Bones[item.BoneIndex];
			var label = (Label)element;
			label.userData = item.Depth;
			int slash = value.Path.LastIndexOf('/');
			label.text = slash >= 0 ? value.Path.Substring(slash + 1) : value.Path;
			label.tooltip = "Target rig bone: " + value.Path + "\nThis bone's weight determines how much the layer replaces its base pose.";
			label.style.color = value.Weight > 0f ? new StyleColor(new Color(0.56f, 0.78f, 0.61f)) : new StyleColor(StyleKeyword.Null);
			UpdateIndent(label);
		}

		private void BindSlider(VisualElement element, int index) {
			int bone = _tree.GetItemDataForIndex<MmdAnimationMaskTreeItem>(index).BoneIndex;
			var value = ((MmdAnimationMask)target).Bones[bone];
			var slider = (Slider)element;
			slider.userData = value.Path;
			slider.tooltip = value.Path + "\n" + BoneWeightTooltip;
			slider.SetValueWithoutNotify(value.Weight);
		}

		private void BindNumber(VisualElement element, int index) {
			int bone = _tree.GetItemDataForIndex<MmdAnimationMaskTreeItem>(index).BoneIndex;
			var value = ((MmdAnimationMask)target).Bones[bone];
			var number = (FloatField)element;
			number.userData = value.Path;
			number.tooltip = value.Path + "\n" + BoneWeightTooltip + " The value is applied when you press Enter or leave the field.";
			number.SetValueWithoutNotify(value.Weight);
		}

		private void UpdateSummary() {
			int affected = 0;
			var bones = ((MmdAnimationMask)target).Bones;
			foreach (var bone in bones) {
				if (bone.Weight > 0f) {
					affected++;
				}
			}
			_summary.text = $"{affected} of {bones.Length} bones affected";
		}

		private void ChangeWeight(string path, float weight) {
			if (string.IsNullOrEmpty(path) || float.IsNaN(weight) || float.IsInfinity(weight)) {
				RefreshWeights();
				return;
			}
			MmdAnimationMaskEditing.SetWeight((MmdAnimationMask)target, path, weight, _branch);
			serializedObject.Update();
			RefreshWeights();
		}

		private void RefreshWeights() {
			var mask = (MmdAnimationMask)target;
			if (mask.Bones.Length != _paths.Length) {
				Rebuild();
				return;
			}
			for (int index = 0; index < _paths.Length; index++) {
				if (mask.Bones[index].Path != _paths[index]) {
					Rebuild();
					return;
				}
			}
			_tree.RefreshItems();
			UpdateSummary();
		}
	}
}
