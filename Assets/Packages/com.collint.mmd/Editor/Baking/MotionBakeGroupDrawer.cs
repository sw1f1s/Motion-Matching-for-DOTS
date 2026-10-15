using ColInt.MMD.Database.Data;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	[CustomPropertyDrawer(typeof(MotionBakeGroup))]
	public sealed class MotionBakeGroupDrawer : PropertyDrawer {
		public override VisualElement CreatePropertyGUI(SerializedProperty property) {
			SerializedProperty name = property.FindPropertyRelative("<Name>k__BackingField");
			SerializedProperty clips = property.FindPropertyRelative("<Clips>k__BackingField");
			var root = new Foldout { text = $"{name.stringValue} ({clips.arraySize} clips)", value = property.isExpanded, tooltip = "Animation group name and clip count. Expand to edit the group." };
			root.AddToClassList("mmd-animation-group");
			root.RegisterValueChangedCallback(change => property.isExpanded = change.newValue);
			MotionBakeInspectorUi.AddField(root, new TextField("Group Name") { tooltip = "Unique name used to identify this group in the database, character configuration, and animation layers." }, name);
			MotionBakeInspectorUi.AddField(root, new EnumField("Playback", MotionPlaybackMode.MotionMatching) { tooltip = "Motion Matching selects clips for locomotion. Action plays on request. Layer Only provides clips for masked layers and is excluded from base locomotion matching." }, property.FindPropertyRelative("<Playback>k__BackingField"));
			MotionBakeInspectorUi.AddField(root, new IntegerField("Clips") { isDelayed = true, tooltip = "Number of clips in this group. Changing the count resizes the list. Use the list buttons to add or remove clips." }, clips.FindPropertyRelative("Array.size"));
			string path = clips.propertyPath;
			var list = new ListView {
				bindingPath = path, fixedItemHeight = 24f, reorderable = true,
				showAddRemoveFooter = true, showBoundCollectionSize = false, showBorder = true,
				tooltip = "Clips in this group. Drag to reorder, assign animation assets, or use + and - to add and remove entries. Loop Time comes from each clip's import settings.",
				makeItem = () => new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false, tooltip = "Animation clip included in this group. Loop Time is read from its import settings." },
				bindItem = (element, index) => ((ObjectField)element).BindProperty(property.serializedObject.FindProperty(path).GetArrayElementAtIndex(index))
			};
			list.AddToClassList("mmd-group-clips");
			root.Add(list);
			root.TrackPropertyValue(name, changed => root.text = $"{changed.stringValue} ({property.FindPropertyRelative("<Clips>k__BackingField").arraySize} clips)");
			root.TrackPropertyValue(clips, changed => root.text = $"{property.FindPropertyRelative("<Name>k__BackingField").stringValue} ({changed.arraySize} clips)");
			return root;
		}
	}
}
