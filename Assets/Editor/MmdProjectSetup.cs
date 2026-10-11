using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColInt.MMD.Project.Editor {
	[InitializeOnLoad]
	public static class MmdProjectSetup {
		private const string SampleScene = "Assets/Packages/com.collint.mmd/Samples/Scenes/MMDSampleEnvironment.unity";
		private static readonly string[] Dependencies = {
			"com.unity.entities@1.4.3",
			"com.unity.mathematics@1.3.3",
			"com.unity.entities.graphics@1.4.3",
			"com.unity.physics@1.4.3",
			"com.unity.collections@2.6.6",
			"com.unity.burst@1.8.29",
			"com.unity.nuget.newtonsoft-json@3.2.2",
			"com.unity.render-pipelines.universal@17.3.0",
			"com.unity.inputsystem@1.19.0"
		};
		private static AddAndRemoveRequest _request;
		private static double _deadline;

		static MmdProjectSetup() {
			EditorApplication.delayCall += InstallDependencies;
		}

		public static void InstallDependencies() {
			if (EditorApplication.isPlayingOrWillChangePlaymode || _request != null) {
				return;
			}
			var installed = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
			string[] missing = Dependencies.Where(id => !installed.Any(p => p.name + "@" + p.version == id)).ToArray();
			if (missing.Length == 0) {
				ConfigureProject();
				return;
			}
			_request = Client.AddAndRemove(missing);
			_deadline = EditorApplication.timeSinceStartup + 600d;
			EditorApplication.update += Poll;
		}

		private static void Poll() {
			if (!_request.IsCompleted) {
				if (EditorApplication.timeSinceStartup > _deadline) {
					EditorApplication.update -= Poll;
					Debug.LogError("MMD dependency installation timed out.");
				}
				return;
			}
			EditorApplication.update -= Poll;
			if (_request.Status != StatusCode.Success) {
				Debug.LogError("MMD dependency installation failed: " + _request.Error.message);
				return;
			}
			_request = null;
			EditorApplication.delayCall += ConfigureProject;
		}

		public static void ConfigureProject() {
			if (!File.Exists(SampleScene)) {
				Debug.LogError("MMD sample scene is missing: " + SampleScene);
				return;
			}
			EditorSettings.serializationMode = SerializationMode.ForceText;
			PlayerSettings.companyName = "ColInt";
			PlayerSettings.productName = "Motion Matching for DOTS";
			if (!EditorBuildSettings.scenes.Any(scene => scene.path == SampleScene)) {
				EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(scene => !scene.path.StartsWith("Assets/Samples/Motion Matching for DOTS/", StringComparison.Ordinal)).Concat(new[] { new EditorBuildSettingsScene(SampleScene, true) }).ToArray();
			}
			if (!AssetDatabase.IsValidFolder("Assets/MmdBakeTemporary")) {
				AssetDatabase.CreateFolder("Assets", "MmdBakeTemporary");
			}
			AssetDatabase.SaveAssets();
			Scene activeScene = SceneManager.GetActiveScene();
			if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode && string.IsNullOrEmpty(activeScene.path) && !activeScene.isDirty) {
				EditorSceneManager.OpenScene(SampleScene);
			}
			Directory.CreateDirectory("Temp");
			File.WriteAllText("Temp/MmdDependencies.ready", "Dependencies, bundled sample and build scene configured.");
			Debug.Log("MMD project ready.");
		}
	}
}
