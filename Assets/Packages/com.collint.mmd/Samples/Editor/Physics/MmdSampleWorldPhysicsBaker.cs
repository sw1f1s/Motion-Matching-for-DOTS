using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System;
using ColInt.MMD.Samples.Physics.Helpers;
using ColInt.MMD.Samples.Physics;
using System.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Editor.Physics {
	public static class MmdSampleWorldPhysicsBaker {
		public static bool IsBaking { get; private set; }

		public static async Task BakeAsync(MmdSampleWorldPhysicsGeometry source, CancellationToken token) {
			if (IsBaking || EditorApplication.isPlayingOrWillChangePlaymode) {
				throw new InvalidOperationException("Bake sample world physics in Edit Mode with no bake already running.");
			}
			token.ThrowIfCancellationRequested();
			IsBaking = true;
			try {
				string fingerprint = Fingerprint(source);
				var bodies = new List<MmdSampleWorldPhysicsCacheBody>();
				var scene = source.gameObject.scene;
				foreach (var body in MmdSampleWorldPhysicsBuilder.BuildBodies(source)) {
					bodies.Add(MmdSampleWorldPhysicsCacheBody.Pack(body));
					token.ThrowIfCancellationRequested();
					if (EditorUtility.DisplayCancelableProgressBar("MMD sample world physics", "Baked " + bodies.Count + " bodies", 0.5f)) {
						throw new OperationCanceledException();
					}
					await Task.Yield();
					token.ThrowIfCancellationRequested();
					if (source == null || !scene.isLoaded || source.gameObject.scene != scene || EditorApplication.isPlayingOrWillChangePlaymode) {
						throw new OperationCanceledException();
					}
				}
				token.ThrowIfCancellationRequested();
				if (fingerprint != Fingerprint(source)) {
					throw new InvalidOperationException("Geometry changed during baking; the previous cache was preserved.");
				}
				Undo.IncrementCurrentGroup();
				int undo = Undo.GetCurrentGroup();
				Undo.SetCurrentGroupName("Bake MMD sample world physics");
				try {
					Undo.RegisterCompleteObjectUndo(source, "Bake MMD sample world physics");
					source.SetCache(fingerprint, bodies.ToArray());
					EditorUtility.SetDirty(source);
					PrefabUtility.RecordPrefabInstancePropertyModifications(source);
					EditorSceneManager.MarkSceneDirty(scene);
				} finally {
					Undo.CollapseUndoOperations(undo);
				}
			} finally {
				EditorUtility.ClearProgressBar();
				IsBaking = false;
			}
		}

		public static bool IsCurrent(MmdSampleWorldPhysicsGeometry source) {
			return source.IsCacheCompatible && source.SourceFingerprint == Fingerprint(source);
		}

		// Editor-only traversal. Include transforms, collider settings and mesh/terrain/prefab dependencies.
		// Do not hash the scene itself: its reference to the generated cache would create a hash cycle.
		public static string Fingerprint(MmdSampleWorldPhysicsGeometry source) {
			var text = new StringBuilder();
			text.Append(MmdSampleWorldPhysicsGeometry.CacheVersion).Append(Application.unityVersion);
			text.Append(File.ReadAllText("Packages/packages-lock.json"));
			text.Append(JsonUtility.ToJson(new MmdSampleWorldPhysicsFingerprintSettings { Matrix = source.transform.localToWorldMatrix, Friction = source.Friction }));
			var assets = new SortedSet<string>(StringComparer.Ordinal);
			foreach (var collider in source.GetComponentsInChildren<Collider>(true)) {
				if (collider.GetComponentInParent<MmdSampleWorldPhysicsGeometry>() != source) {
					continue;
				}
				if (collider.enabled && collider.gameObject.activeInHierarchy) {
					MmdSampleWorldPhysicsBuilder.ValidateCollider(collider);
				}

				text.Append(collider.GetType().FullName).Append(collider.enabled).Append(collider.isTrigger)
					.Append(collider.gameObject.activeInHierarchy);
				text.Append(JsonUtility.ToJson(new MmdSampleWorldPhysicsFingerprintSettings { Matrix = collider.transform.localToWorldMatrix }));
				// Serialize scalar geometry without Unity instance IDs (which change after reload).
				if (collider is BoxCollider box) {
					text.Append(JsonUtility.ToJson(new MmdSampleWorldPhysicsFingerprintShape { Center = box.center, Size = box.size }));
				}

				if (collider is SphereCollider sphere) {
					text.Append(JsonUtility.ToJson(new MmdSampleWorldPhysicsFingerprintShape { Center = sphere.center, Radius = sphere.radius }));
				}

				if (collider is CapsuleCollider capsule) {
					text.Append(JsonUtility.ToJson(new MmdSampleWorldPhysicsFingerprintShape {
						Center = capsule.center,
						Radius = capsule.radius,
						Height = capsule.height,
						Direction = capsule.direction
					}));
				}

				if (collider is MeshCollider mesh) {
					text.Append(mesh.convex);
					AddAsset(mesh.sharedMesh, text, assets);
				}
				if (collider is TerrainCollider terrain) {
					AddAsset(terrain.terrainData, text, assets);
				}
			}
			foreach (string path in assets) {
				text.Append(path).Append(AssetDatabase.GetAssetDependencyHash(path));
			}

			return Hash128.Compute(text.ToString()).ToString();
		}

		private static void AddAsset(UnityEngine.Object asset, StringBuilder text, SortedSet<string> assets) {
			if (asset == null) {
				text.Append("null");
				return;
			}
			if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)) {
				throw new InvalidOperationException($"Save '{asset.name}' as an asset before baking location physics.");
			}

			text.Append(guid).Append(':').Append(localId);
			assets.Add(AssetDatabase.GetAssetPath(asset));
		}
	}
}
