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
			var root = new Foldout { text = $"{name.stringValue} ({clips.arraySize} clips)", value = property.isExpanded };
			root.AddToClassList("mmd-animation-group");
			root.RegisterValueChangedCallback(change => property.isExpanded = change.newValue);
			root.Add(new PropertyField(name, "Group Name"));
			root.Add(new PropertyField(property.FindPropertyRelative("<Playback>k__BackingField"), "Playback"));
			string path = clips.propertyPath;
			var list = new ListView {
				bindingPath = path, fixedItemHeight = 24f, reorderable = true,
				showAddRemoveFooter = true, showBoundCollectionSize = true, showBorder = true,
				makeItem = () => new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false },
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
