using System;
using UnityEditor;

namespace ColInt.MMD.Editor.Baking {
	public static class MotionBakeConfigurationEditing {
		public static void AddGroup(SerializedObject configuration) {
			if (EditorApplication.isPlayingOrWillChangePlaymode) {
				throw new InvalidOperationException("Edit animation groups in Edit Mode.");
			}
			configuration.Update();
			SerializedProperty groups = configuration.FindProperty("_groups");
			if (groups.arraySize >= 32) {
				throw new InvalidOperationException("A database supports up to 32 named groups.");
			}
			string name = "New Group";
			int suffix = 2;
			while (ContainsName(groups, name)) {
				name = "New Group " + suffix++;
			}
			groups.InsertArrayElementAtIndex(groups.arraySize);
			SerializedProperty group = groups.GetArrayElementAtIndex(groups.arraySize - 1);
			group.FindPropertyRelative("<Name>k__BackingField").stringValue = name;
			group.FindPropertyRelative("<Playback>k__BackingField").enumValueIndex = 0;
			group.FindPropertyRelative("<Clips>k__BackingField").arraySize = 0;
			group.isExpanded = true;
			configuration.ApplyModifiedProperties();
		}

		private static bool ContainsName(SerializedProperty groups, string name) {
			for (int index = 0; index < groups.arraySize; index++) {
				if (groups.GetArrayElementAtIndex(index).FindPropertyRelative("<Name>k__BackingField").stringValue == name) {
					return true;
				}
			}
			return false;
		}
	}
}
