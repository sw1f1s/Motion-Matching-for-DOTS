using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Playback {
	public static class MmdAnimationInspectorUi {
		public static VisualElement CreateRoot(UnityEditor.Editor editor) {
			var root = new VisualElement();
			root.AddToClassList("mmd-animation-inspector");
			string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(editor))).Replace('\\', '/');
			root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(directory + "/MmdAnimationInspector.uss"));
			return root;
		}

		public static VisualElement Row() {
			var row = new VisualElement();
			row.AddToClassList("mmd-animation-row");
			return row;
		}

		public static void StyleField<T>(BaseField<T> field) {
			field.AddToClassList("mmd-animation-field");
			field.labelElement.tooltip = field.tooltip;
			var input = field.Q<VisualElement>(className: BaseField<T>.inputUssClassName);
			if (input != null) {
				input.tooltip = field.tooltip;
			}
		}
	}
}
