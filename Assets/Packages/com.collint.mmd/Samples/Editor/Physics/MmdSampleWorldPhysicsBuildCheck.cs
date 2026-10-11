using System;
using ColInt.MMD.Samples.Physics;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace ColInt.MMD.Editor.Physics {
	public sealed class MmdSampleWorldPhysicsBuildCheck : IProcessSceneWithReport {
		public int callbackOrder => 0;

		public void OnProcessScene(Scene scene, BuildReport report) {
			// A null report is Unity's Play Mode scene processing, not a player build.
			if (report == null) {
				return;
			}
			foreach (GameObject root in scene.GetRootGameObjects()) {
				foreach (var source in root.GetComponentsInChildren<MmdSampleWorldPhysicsGeometry>(true)) {
					if (!MmdSampleWorldPhysicsBaker.IsCurrent(source)) {
						throw new BuildFailedException("Rebake the MMD sample world physics cache on '" + source.name + "'.");
					}
				}
			}
		}

		[InitializeOnLoadMethod]
		private static void Initialize() {
			EditorApplication.playModeStateChanged -= ValidateBeforePlay;
			EditorApplication.playModeStateChanged += ValidateBeforePlay;
		}

		private static void ValidateBeforePlay(PlayModeStateChange state) {
			if (state != PlayModeStateChange.ExitingEditMode) {
				return;
			}
			try {
				foreach (var source in UnityEngine.Object.FindObjectsByType<MmdSampleWorldPhysicsGeometry>(FindObjectsSortMode.None)) {
					if (!MmdSampleWorldPhysicsBaker.IsCurrent(source)) {
						throw new InvalidOperationException("Rebake the MMD sample world physics cache on '" + source.name + "' before Play Mode.");
					}
				}
			} catch (Exception exception) {
				EditorApplication.isPlaying = false;
				Debug.LogException(exception);
			}
		}
	}
}
