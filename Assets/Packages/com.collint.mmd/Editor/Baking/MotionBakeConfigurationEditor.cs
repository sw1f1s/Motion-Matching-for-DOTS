using System.IO;
using ColInt.MMD.Database.Helpers;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Baking {
	[CustomEditor(typeof(MotionBakeConfiguration))]
	public sealed class MotionBakeConfigurationEditor : UnityEditor.Editor {
		public override VisualElement CreateInspectorGUI() {
			string stylePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\', '/') + "/MotionBakeConfiguration.uss";
			var root = new VisualElement();
			root.AddToClassList("mmd-bake-inspector");
			root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath));
			MotionBakeInspectorUi.AddField(root, new ObjectField("Rig") { objectType = typeof(GameObject), allowSceneObjects = false, tooltip = "Target model or prefab used to sample animations. Its skeleton must match the character that uses the baked database." }, serializedObject.FindProperty("_rig"));
			root.Add(new HelpBox("Create named animation groups. Loop Time is read from each animation's import settings. Action groups play on request. Layer Only groups can loop and are used by masked layers, without participating in locomotion matching.", HelpBoxMessageType.Info));
			SerializedProperty groups = serializedObject.FindProperty("_groups");
			groups.isExpanded = true;
			var groupList = new PropertyField(groups, "Animation Groups") { tooltip = "Named clip groups in the database. Expand a group to edit its name, playback mode, and clips. Drag the handles to reorder groups." };
			groupList.AddToClassList("mmd-bake-groups");
			root.Add(groupList);
			var addGroup = new Button(() => MotionBakeConfigurationEditing.AddGroup(serializedObject)) { text = "Add Group", tooltip = "Add an empty animation group with a unique name." };
			addGroup.AddToClassList("mmd-add-group-button");
			root.Add(addGroup);
			var settings = new Foldout { text = "Bake Settings", value = false, tooltip = "Sampling, compression, feature normalization, bone paths, and database output settings." };
			settings.AddToClassList("mmd-bake-settings");
			MotionBakeInspectorUi.AddField(settings, new Slider("Sample Rate", 15f, 120f) { showInputField = true, tooltip = "Animation samples per second. Higher values capture more detail and increase database size and baking time." }, serializedObject.FindProperty("_sampleRate"));
			MotionBakeInspectorUi.AddField(settings, new Slider("Max. Rotation Error", 0.0001f, MmdRotationPacking.DefaultMaximumErrorDegrees) { showInputField = true, tooltip = "Maximum allowed local bone rotation error in degrees. Baking fails before replacing the database if compression exceeds this limit." }, serializedObject.FindProperty("_maximumRotationErrorDegrees"));
			settings.Add(new PropertyField(serializedObject.FindProperty("_normalizationGroups"), "Normalization Groups") { tooltip = "Groups used to calculate search feature scales. An empty list uses every clip. Use a fixed reference set to keep matching stable when adding new stances." });
			MotionBakeInspectorUi.AddField(settings, new TextField("Root Motion Path") { tooltip = "Path to the target rig bone used to extract root motion, relative to the rig root." }, serializedObject.FindProperty("_rootMotionPath"));
			MotionBakeInspectorUi.AddField(settings, new TextField("Source Root Path") { tooltip = "Optional source skeleton bone path for extracting root motion from Humanoid clips. Leave empty to use the standard sampling path." }, serializedObject.FindProperty("_sourceRootMotionPath"));
			MotionBakeInspectorUi.AddField(settings, new TextField("Left Foot Path") { tooltip = "Path to the left foot bone, relative to the rig root. Used to generate motion matching features." }, serializedObject.FindProperty("_leftFootPath"));
			MotionBakeInspectorUi.AddField(settings, new TextField("Right Foot Path") { tooltip = "Path to the right foot bone, relative to the rig root. Used to generate motion matching features." }, serializedObject.FindProperty("_rightFootPath"));
			MotionBakeInspectorUi.AddField(settings, new TextField("Output") { tooltip = "Project asset path for the baked database, including the .bytes file name." }, serializedObject.FindProperty("_output"));
			root.Add(settings);
			var bake = new Button(() => MotionDatabaseBake.Bake((MotionBakeConfiguration)target)) { text = "Bake Database", tooltip = "Sample the configured clips and write the animation database to Output. Validation errors disable this button." };
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
			addGroup.SetEnabled(editable);
			try {
				MotionBakeSelection selection = MotionBakeGroupResolver.Resolve(((MotionBakeConfiguration)target).Groups);
				MotionFeatureNormalization.ResolveGroups(selection.Groups, ((MotionBakeConfiguration)target).NormalizationGroups);
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
