using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Baking;
using ColInt.MMD.Playback.Helpers;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugCatalogStore {
		private readonly Dictionary<string, MmdDebugClipCatalog> _catalogs = new Dictionary<string, MmdDebugClipCatalog>();
		private readonly Dictionary<BlobAssetReference<MotionDatabase>, MmdDebugClipCatalog> _resolved = new Dictionary<BlobAssetReference<MotionDatabase>, MmdDebugClipCatalog>();

		public void Reload() {
			_catalogs.Clear();
			_resolved.Clear();
			foreach (string guid in AssetDatabase.FindAssets("t:TextAsset")) {
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (!path.EndsWith(".mmd-debug.json", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				try {
					var catalog = JsonConvert.DeserializeObject<MmdDebugClipCatalog>(AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(path).text);
					if (catalog != null && catalog.Version == 1 && !string.IsNullOrEmpty(catalog.Fingerprint) &&
						catalog.Names != null && catalog.AssetPaths != null && catalog.Names.Length == catalog.AssetPaths.Length) {
						RelocateAssetPaths(catalog, path);
						_catalogs[catalog.Fingerprint] = catalog;
					}
				} catch (Exception exception) when (exception is IOException || exception is JsonException) {
					UnityEngine.Debug.LogWarning($"MMD debug catalog '{path}' could not be read: {exception.Message}");
				}
			}
		}

		public MmdDebugClipCatalog Resolve(BlobAssetReference<MotionDatabase> database) {
			if (_resolved.TryGetValue(database, out var cached)) {
				return cached;
			}
			string fingerprint = Fingerprint(ref database.Value);
			_catalogs.TryGetValue(fingerprint, out var catalog);
			if (catalog != null && catalog.Names.Length != database.Value.Clips.Length) {
				catalog = null;
			}
			_resolved.Add(database, catalog);
			return catalog;
		}

		public void ClearResolved() {
			_resolved.Clear();
		}

		public static void Write(string databasePath, MotionDatabaseSource source, MotionBakeClip[] clips) {
			if (source.Clips.Length != clips.Length) {
				throw new ArgumentException("Debug catalog must use the baked clip order.");
			}
			using (var database = MotionDatabaseBuilder.Build(source)) {
				var catalog = new MmdDebugClipCatalog {
					Fingerprint = Fingerprint(ref database.Value), DatabasePath = databasePath,
					Names = new string[clips.Length], AssetPaths = new string[clips.Length]
				};
				for (int index = 0; index < clips.Length; index++) {
					catalog.Names[index] = clips[index].Clip.name;
					catalog.AssetPaths[index] = AssetDatabase.GetAssetPath(clips[index].Clip);
				}
				string path = Path.ChangeExtension(databasePath, ".mmd-debug.json");
				File.WriteAllText(path, JsonConvert.SerializeObject(catalog, Formatting.Indented));
				AssetDatabase.ImportAsset(path);
			}
		}

		public static string Fingerprint(ref MotionDatabase database) {
			// Bounded content signature: structure and three complete pose/feature samples per clip.
			// It is computed once per database, never during the regular window refresh.
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream)) {
					writer.Write(database.FeatureCount);
					writer.Write(database.Frames.Length);
					writer.Write(database.Parents.Length);
					for (int bone = 0; bone < database.Parents.Length; bone++) {
						writer.Write(database.Parents[bone]);
						writer.Write(database.BoneNameHashes[bone]);
					}
					for (int feature = 0; feature < database.FeatureScales.Length; feature++) {
						writer.Write(database.FeatureScales[feature]);
					}
					writer.Write(database.Groups.Length);
					for (int index = 0; index < database.Groups.Length; index++) {
						ref var group = ref database.Groups[index];
						writer.Write(group.Name.ToString());
						writer.Write(group.Tags);
						writer.Write(group.ActionId);
					}
					writer.Write(database.Clips.Length);
					for (int index = 0; index < database.Clips.Length; index++) {
						ref var clip = ref database.Clips[index];
						writer.Write(clip.FirstFrame);
						writer.Write(clip.FrameCount);
						writer.Write(clip.Duration);
						writer.Write(clip.SampleRate);
						writer.Write(clip.Tags);
						writer.Write(clip.ActionId);
						writer.Write(clip.Loop);
						WriteSample(writer, ref database, clip.FirstFrame);
						WriteSample(writer, ref database, clip.FirstFrame + clip.FrameCount / 2);
						WriteSample(writer, ref database, clip.FirstFrame + clip.FrameCount - 1);
					}
					writer.Flush();
					stream.Position = 0;
					using (var hash = SHA256.Create()) {
						return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
					}
				}
			}
		}


		private static void RelocateAssetPaths(MmdDebugClipCatalog catalog, string catalogPath) {
			string sourceDirectory = Path.GetDirectoryName(catalog.DatabasePath);
			string currentDirectory = Path.GetDirectoryName(catalogPath);
			catalog.DatabasePath = catalogPath.Substring(0, catalogPath.Length - ".mmd-debug.json".Length) + ".bytes";
			if (string.IsNullOrEmpty(sourceDirectory)) {
				return;
			}
			for (int index = 0; index < catalog.AssetPaths.Length; index++) {
				string sourcePath = catalog.AssetPaths[index];
				if (string.IsNullOrEmpty(sourcePath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sourcePath, AssetPathToGUIDOptions.OnlyExistingAssets))) {
					continue;
				}
				string relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
				// Resolve virtual asset paths lexically; local UPM packages can be filesystem symlinks.
				var directoryUri = new Uri("file:///" + currentDirectory.Replace('\\', '/') + "/");
				string relocatedPath = Uri.UnescapeDataString(new Uri(directoryUri, relativePath.Replace('\\', '/')).AbsolutePath).TrimStart('/');
				if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(relocatedPath, AssetPathToGUIDOptions.OnlyExistingAssets))) {
					catalog.AssetPaths[index] = relocatedPath;
				}
			}
		}

		private static void WriteSample(BinaryWriter writer, ref MotionDatabase database, int frame) {
			writer.Write(database.Frames[frame].Time);
			WriteVector(writer, database.Frames[frame].Root.pos);
			WriteRotation(writer, database.Frames[frame].Root.rot);
			for (int feature = 0; feature < database.FeatureCount; feature++) {
				writer.Write(database.Features[frame * database.FeatureCount + feature]);
			}
			for (int bone = 0; bone < database.Parents.Length; bone++) {
				JointPose pose = MmdAnimationMath.PoseSample(ref database, frame, bone);
				WriteVector(writer, pose.Position);
				WriteRotation(writer, pose.Rotation);
				WriteVector(writer, pose.Scale);
			}
		}

		private static void WriteVector(BinaryWriter writer, float3 value) {
			writer.Write(value.x);
			writer.Write(value.y);
			writer.Write(value.z);
		}

		private static void WriteRotation(BinaryWriter writer, quaternion value) {
			writer.Write(value.value.x);
			writer.Write(value.value.y);
			writer.Write(value.value.z);
			writer.Write(value.value.w);
		}
	}
}
