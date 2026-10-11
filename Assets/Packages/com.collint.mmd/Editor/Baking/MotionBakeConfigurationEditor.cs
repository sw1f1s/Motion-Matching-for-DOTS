using System.IO;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Baking {
	[CustomEditor(typeof(MotionBakeConfiguration))]
	public sealed class MotionBakeConfigurationEditor : UnityEditor.Editor {
		public override VisualElement CreateInspectorGUI() {
			string stylePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\', '/') + "/MotionBakeConfiguration.uss";
			var root = new VisualElement();
			root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath));
			root.Add(new PropertyField(serializedObject.FindProperty("_rig"), "Rig"));
			root.Add(new HelpBox("Create named animation groups. Loop Time is read from each animation's import settings. Action groups play on request instead of participating in locomotion matching.", HelpBoxMessageType.Info));
			SerializedProperty groups = serializedObject.FindProperty("_groups");
			groups.isExpanded = true;
			root.Add(new PropertyField(groups, "Animation Groups"));
			var addGroup = new Button(() => MotionBakeConfigurationEditing.AddGroup(serializedObject)) { text = "Add Group" };
			root.Add(addGroup);
			var settings = new Foldout { text = "Bake Settings", value = false };
			foreach (string name in new[] { "_sampleRate", "_maximumRotationErrorDegrees", "_normalizationGroups", "_rootMotionPath", "_sourceRootMotionPath", "_leftFootPath", "_rightFootPath", "_output" }) {
				settings.Add(new PropertyField(serializedObject.FindProperty(name)));
			}
			root.Add(settings);
			var bake = new Button(() => MotionDatabaseBake.Bake((MotionBakeConfiguration)target)) { text = "Bake Database" };
			bake.AddToClassList("mmd-bake-button");
			var validation = new HelpBox(string.Empty, HelpBoxMessageType.Error);
			root.Add(validation);
			root.TrackSerializedObjectValue(serializedObject, changed => UpdateValidation(validation, bake, addGroup));
			UpdateValidation(validation, bake, addGroup);
			root.Add(bake);
			return root;
		}

		private void UpdateValidation(HelpBox message, Button bake, Button addGroup) {
			bool editable = !EditorApplication.isPlayingOrWillChangePlaymode;
			addGroup.SetEnabled(editable && ((MotionBakeConfiguration)target).Groups.Length < 32);
			try {
				MotionBakeSelection selection = MotionBakeGroupResolver.Resolve(((MotionBakeConfiguration)target).Groups);
				MotionFeatureNormalization.ResolveTags(selection.Groups, ((MotionBakeConfiguration)target).NormalizationGroups);
				message.AddToClassList("mmd-hidden");
				bake.SetEnabled(editable && selection.Clips.Length > 0);
			} catch (System.ArgumentException exception) {
				message.text = exception.Message;
				message.RemoveFromClassList("mmd-hidden");
				bake.SetEnabled(false);
			}
		}
	}
}
