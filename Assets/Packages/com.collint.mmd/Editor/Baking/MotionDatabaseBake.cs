using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Helpers;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	public static class MotionDatabaseBake {
		private static CancellationTokenSource _activeBake;
		private static GameObject _activeInstance;
		private static PlayableGraph _activeGraph;
		private static MotionSourceRoot _activeSourceRoot;

		public static async void Bake(MotionBakeConfiguration configuration) {
			try {
				await BakeAsync(configuration);
			} catch (OperationCanceledException) {
				Debug.Log("MMD bake cancelled; previous database retained.");
			} catch (Exception exception) {
				Debug.LogException(exception);
			}
		}

		public static async Task BakeAsync(MotionBakeConfiguration configuration) {
			MotionBakeSelection selection = MotionBakeGroupResolver.Resolve(configuration.Groups);
			int[] normalizationGroups = MotionFeatureNormalization.ResolveGroups(selection.Groups, configuration.NormalizationGroups);
			if (EditorApplication.isPlayingOrWillChangePlaymode || configuration.Rig == null || selection.Clips.Length == 0 ||
				configuration.SampleRate <= 0f || string.IsNullOrWhiteSpace(configuration.Output) ||
				!IsWritableOutput(configuration.Output)) {
				throw new InvalidOperationException("A rig, clips, positive sample rate and a .bytes output inside Assets or an embedded package are required in Edit Mode.");
			}
			if (_activeBake != null) {
				throw new InvalidOperationException("A bake is already running.");
			}
			_activeBake = new CancellationTokenSource();
			CancellationToken cancellation = _activeBake.Token;
			string configurationSnapshot = EditorJsonUtility.ToJson(configuration);
			EditorApplication.playModeStateChanged += CancelForPlayMode;
			EditorSceneManager.activeSceneChangedInEditMode += CancelForScene;
			AssemblyReloadEvents.beforeAssemblyReload += AbortBeforeReload;
			GameObject instance = UnityEngine.Object.Instantiate(configuration.Rig);
			_activeInstance = instance;
			instance.hideFlags = HideFlags.HideAndDontSave;
			var frames = new List<MotionFrame>();
			var poses = new List<JointPose>();
			var clips = new List<MotionClipSource>();
			try {
				Transform[] bones = instance.GetComponentsInChildren<Transform>(true).Where(t => t != instance.transform).ToArray();
				string[] paths = bones.Select(t => AnimationUtility.CalculateTransformPath(t, instance.transform)).ToArray();
				int[] parents = bones.Select(t => Array.IndexOf(bones, t.parent)).ToArray();
				Transform left = instance.transform.Find(configuration.LeftFootPath);
				Transform right = instance.transform.Find(configuration.RightFootPath);
				int leftIndex = Array.IndexOf(bones, left);
				int rightIndex = Array.IndexOf(bones, right);
				if (leftIndex < 0 || rightIndex < 0) {
					throw new InvalidOperationException("Both foot paths must identify rig bones.");
				}
				var animator = instance.GetComponent<Animator>();
				JointPose[] rest = bones.Select(t => new JointPose { Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale }).ToArray();
				for (int clipIndex = 0; clipIndex < selection.Clips.Length; clipIndex++) {
					cancellation.ThrowIfCancellationRequested();
					MotionBakeClip source = selection.Clips[clipIndex];
					if (source.Clip == null || source.Clip.length <= 0f || (source.ActionId > 0 && source.Loop)) {
						throw new InvalidOperationException("Missing/empty animation or looping action.");
					}
					if (EditorUtility.DisplayCancelableProgressBar("MMD animation bake", source.Clip.name, (float)clipIndex / selection.Clips.Length)) {
						throw new OperationCanceledException("MMD bake cancelled. Existing database retained.");
					}
					for (int bone = 0; bone < bones.Length; bone++) {
						bones[bone].localPosition = rest[bone].Position;
						bones[bone].localRotation = rest[bone].Rotation;
						bones[bone].localScale = rest[bone].Scale;
					}
					instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
					instance.transform.localScale = Vector3.one;
					int samples = (int)math.ceil(source.Clip.length * configuration.SampleRate) + 1;
					int first = frames.Count;
					PlayableGraph graph = default;
					AnimationClipPlayable playable = default;
					Transform root = instance.transform.Find(configuration.RootMotionPath);
					if (source.Clip.isHumanMotion) {
						if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid) {
							throw new InvalidOperationException("Humanoid retargeting requires a valid target Avatar.");
						}
						// Enable only the hidden bake instance; the shared GPU prefab keeps it disabled.
						animator.enabled = true;
						animator.applyRootMotion = true;
						animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
						animator.Rebind();
						graph = PlayableGraph.Create("MMD Offline Retarget");
						_activeGraph = graph;
						graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
						playable = AnimationClipPlayable.Create(graph, source.Clip);
						playable.SetApplyFootIK(false);
						playable.SetApplyPlayableIK(false);
						playable.SetSpeed(0d);
						AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
						graph.Play();
					} else if (root == null) {
						throw new InvalidOperationException("Generic animation requires an explicit root-motion bone path.");
					}
					try {
						if (source.Clip.isHumanMotion && !string.IsNullOrEmpty(configuration.SourceRootMotionPath)) {
							_activeSourceRoot = MotionSourceRoot.Create(source.Clip, configuration.SourceRootMotionPath, animator);
						}
						RigidTransform origin = RigidTransform.identity;
						float3 poseOriginOffset = float3.zero;
						float previous = 0f;
						for (int sample = 0; sample < samples; sample++) {
							if (sample > 0 && (sample & 63) == 0) {
								await Task.Yield();
						cancellation.ThrowIfCancellationRequested();
							}
							float time = math.min(sample / configuration.SampleRate, source.Clip.length);
							if (graph.IsValid()) {
								playable.SetTime(time);
								graph.Evaluate(time - previous);
							} else {
								source.Clip.SampleAnimation(instance, time);
							}
							Transform rootTransform = graph.IsValid() ? instance.transform : root;
							RigidTransform absolute = new RigidTransform(rootTransform.rotation, rootTransform.position);
							if (sample == 0) {
								origin = absolute;
							}
							RigidTransform humanoidRoot = math.mul(math.inverse(origin), absolute);
							if (_activeSourceRoot != null) {
								// Pose, root translation and search features must share the clip's initial facing basis.
								humanoidRoot = math.mul(new RigidTransform(_activeSourceRoot.InverseInitialHeading, float3.zero), humanoidRoot);
							}
							RigidTransform extracted = _activeSourceRoot != null ? _activeSourceRoot.Sample(time) : humanoidRoot;
							RigidTransform correction = math.mul(math.inverse(extracted), humanoidRoot);
							if (sample == 0 && _activeSourceRoot != null) {
								poseOriginOffset = _activeSourceRoot.PoseOriginOffset(animator, correction);
							}
							correction.pos += poseOriginOffset;
							frames.Add(new MotionFrame { ClipIndex = clipIndex, Time = time, Root = extracted });
							// Generic root is stripped from pose; humanoid motion moves the model object only.
							Vector3 savedPosition = rootTransform.position;
							Quaternion savedRotation = rootTransform.rotation;
							rootTransform.SetPositionAndRotation(origin.pos, origin.rot);
							for (int bone = 0; bone < bones.Length; bone++) {
								var pose = new JointPose { Position = bones[bone].localPosition, Rotation = bones[bone].localRotation, Scale = bones[bone].localScale };
								if (_activeSourceRoot != null && parents[bone] < 0) {
									pose.Position = math.transform(correction, pose.Position);
									pose.Rotation = math.normalize(math.mul(correction.rot, pose.Rotation));
								}
								poses.Add(pose);
							}
							rootTransform.SetPositionAndRotation(savedPosition, savedRotation);
							previous = time;
						}
						await Task.Yield();
						cancellation.ThrowIfCancellationRequested();
						clips.Add(new MotionClipSource {
							FirstFrame = first, FrameCount = samples, Duration = source.Clip.length,
							SampleRate = configuration.SampleRate, Loop = source.Loop, Groups = source.Groups, ActionId = source.ActionId, Mode = source.Mode
						});
					} finally {
						if (_activeSourceRoot != null) {
							_activeSourceRoot.Dispose();
							_activeSourceRoot = null;
						}
						if (graph.IsValid()) {
							graph.Destroy();
							_activeGraph = default;
						}
					}
				}
				var data = new MotionDatabaseSource {
					Groups = selection.Groups, Clips = clips.ToArray(), Frames = frames.ToArray(), Poses = poses.ToArray(), Parents = parents,
					BoneNameHashes = paths.Select(BonePathHash.Calculate).ToArray(), FeatureCount = LocomotionSchema.Dimension,
					Features = new float[frames.Count * LocomotionSchema.Dimension],
					LeftFootBone = leftIndex, RightFootBone = rightIndex, MaximumRotationErrorDegrees = configuration.MaximumRotationErrorDegrees,
					FeatureScales = Enumerable.Repeat(1f, LocomotionSchema.Dimension).ToArray()
				};
				BakeFeatures(data, leftIndex, rightIndex);
				MotionFeatureNormalization.Normalize(data, normalizationGroups);
				byte[] bytes = MotionDatabaseCodec.Write(data);
				Directory.CreateDirectory(Path.GetDirectoryName(configuration.Output));
				string temporary = configuration.Output + ".tmp";
				File.WriteAllBytes(temporary, bytes);
				if (configurationSnapshot != EditorJsonUtility.ToJson(configuration) || selection.Clips.Any(c => c.Loop != AnimationUtility.GetAnimationClipSettings(c.Clip).loopTime)) {
					throw new InvalidOperationException("Bake configuration changed while baking; previous database retained.");
				}
				cancellation.ThrowIfCancellationRequested();
				if (File.Exists(configuration.Output)) {
					File.Replace(temporary, configuration.Output, null);
				} else {
					File.Move(temporary, configuration.Output);
				}
				AssetDatabase.ImportAsset(configuration.Output);
				try {
					ColInt.MMD.Editor.Diagnostics.MmdDebugCatalogStore.Write(configuration.Output, data, selection.Clips);
				} catch (IOException exception) {
					Debug.LogWarning($"MMD database saved, but its optional editor debug catalog could not be written: {exception.Message}");
				}
				Debug.Log($"MMD baked {clips.Count} clips, {frames.Count} frames, {bones.Length} bones to {configuration.Output}");
			} finally {
				EditorApplication.playModeStateChanged -= CancelForPlayMode;
				EditorSceneManager.activeSceneChangedInEditMode -= CancelForScene;
				AssemblyReloadEvents.beforeAssemblyReload -= AbortBeforeReload;
				_activeBake.Dispose();
				_activeBake = null;
				EditorUtility.ClearProgressBar();
				UnityEngine.Object.DestroyImmediate(instance);
				_activeInstance = null;
			}
		}

		public static void Cancel() {
			if (_activeBake != null) {
				_activeBake.Cancel();
			}
		}

		private static void AbortBeforeReload() {
			Cancel();
			if (_activeSourceRoot != null) {
				_activeSourceRoot.Dispose();
				_activeSourceRoot = null;
			}
			if (_activeGraph.IsValid()) {
				_activeGraph.Destroy();
			}
			if (_activeInstance != null) {
				UnityEngine.Object.DestroyImmediate(_activeInstance);
			}
			EditorUtility.ClearProgressBar();
		}

		private static void CancelForPlayMode(PlayModeStateChange change) {
			Cancel();
		}

		private static void CancelForScene(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene current) {
			Cancel();
		}

		private static void BakeFeatures(MotionDatabaseSource source, int left, int right) {
			var blob = MotionDatabaseBuilder.Build(source);
			try {
				ref var database = ref blob.Value;
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					MotionFrame sample = source.Frames[frame];
					ref var clip = ref database.Clips[sample.ClipIndex];
					RigidTransform inverse = math.inverse(sample.Root);
					int offset = frame * source.FeatureCount;
					for (int prediction = 0; prediction < 3; prediction++) {
						RigidTransform future = math.mul(inverse, MmdAnimationMath.RootAt(ref database, sample.ClipIndex,
							sample.Time + LocomotionSchema.PredictionTimes[prediction]));
						float3 forward = math.rotate(future.rot, new float3(0f, 0f, 1f));
						source.Features[offset + prediction * 4] = future.pos.x;
						source.Features[offset + prediction * 4 + 1] = future.pos.z;
						source.Features[offset + prediction * 4 + 2] = forward.x;
						source.Features[offset + prediction * 4 + 3] = forward.z;
					}
					float interval = 1f / clip.SampleRate;
					RigidTransform nextRoot = MmdAnimationMath.RootAt(ref database, sample.ClipIndex, sample.Time + interval);
					float3 velocity = math.mul(inverse, nextRoot).pos / interval;
					WriteVector(source.Features, offset + 12, velocity);
					WriteFoot(source, frame, left, offset + 15, interval);
					WriteFoot(source, frame, right, offset + 21, interval);
				}
			} finally {
				blob.Dispose();
			}
		}

		private static void WriteFoot(MotionDatabaseSource source, int frame, int bone, int offset, float interval) {
			MotionClipSource clip = source.Clips[source.Frames[frame].ClipIndex];
			int next = math.min(frame + 1, clip.FirstFrame + clip.FrameCount - 1);
			float3 position = BonePosition(source, frame, bone);
			float3 velocity = (BonePosition(source, next, bone) - position) / interval;
			WriteVector(source.Features, offset, position);
			WriteVector(source.Features, offset + 3, velocity);
		}

		private static float3 BonePosition(MotionDatabaseSource source, int frame, int bone) {
			float4x4 model = float4x4.identity;
			int current = bone;
			while (current >= 0) {
				JointPose pose = source.Poses[frame * source.Parents.Length + current];
				model = math.mul(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), model);
				current = source.Parents[current];
			}
			return model.c3.xyz;
		}

		private static void WriteVector(float[] target, int offset, float3 value) {
			target[offset] = value.x;
			target[offset + 1] = value.y;
			target[offset + 2] = value.z;
		}

		private static bool IsWritableOutput(string output) {
			if (!output.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			string root;
			if (output.StartsWith("Assets/", StringComparison.Ordinal)) {
				root = Path.GetFullPath("Assets");
			} else {
				var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(output);
				if (package == null || package.source != UnityEditor.PackageManager.PackageSource.Embedded) {
					return false;
				}
				root = Path.GetFullPath("Packages/" + package.name);
			}
			return Path.GetFullPath(output).StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
		}

	}

}
