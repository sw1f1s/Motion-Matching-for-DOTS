using UnityEditor;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Baking {
	public static class MotionBakeInspectorUi {
		public static void AddField<T>(VisualElement parent, BaseField<T> field, SerializedProperty property) {
			field.bindingPath = property.propertyPath;
			field.AddToClassList("mmd-bake-field");
			field.labelElement.tooltip = field.tooltip;
			var input = field.Q<VisualElement>(className: BaseField<T>.inputUssClassName);
			if (input != null) {
				input.tooltip = field.tooltip;
			}
			parent.Add(field);
		}
	}
}
