using System.Reflection;
using System;
using Unity.Entities;
using UnityEditor;

namespace ColInt.MMD.Editor.Diagnostics {
	public static class MmdDebugEntityNavigation {
		private static readonly Type HierarchyType = Type.GetType("Unity.Entities.Editor.HierarchyWindow, Unity.Entities.Editor", true);
		private static readonly Type SelectionType = Type.GetType("Unity.Entities.Editor.EntitySelectionProxy, Unity.Entities.Editor", true);
		private static readonly MethodInfo SelectMethod = SelectionType.GetMethod("SelectEntity", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(World), typeof(Entity) }, null);
		private static readonly MethodInfo SearchMethod = HierarchyType.GetMethod("SetSearchQuery", BindingFlags.Public | BindingFlags.Instance);

		public static bool CanSelect(MmdDebugEntitySnapshot snapshot) {
			return snapshot.World.IsCreated && snapshot.World.EntityManager.CanBeginExclusiveEntityTransaction() &&
				snapshot.World.EntityManager.Exists(snapshot.Entity);
		}

		public static void Select(MmdDebugEntitySnapshot snapshot) {
			if (!CanSelect(snapshot)) {
				return;
			}
			var hierarchy = EditorWindow.GetWindow(HierarchyType);
			hierarchy.Show();
			hierarchy.Focus();
			hierarchy.rootVisualElement.schedule.Execute(() => {
				if (hierarchy != null && CanSelect(snapshot)) {
					SearchMethod.Invoke(hierarchy, new object[] { string.Empty });
					SelectMethod.Invoke(null, new object[] { snapshot.World, snapshot.Entity });
				}
			});
		}
	}
}
