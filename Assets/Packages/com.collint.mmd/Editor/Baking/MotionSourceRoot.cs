using System.Linq;
using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	// Read authored FBX motion separately from Humanoid's projected body motion.
	public sealed class MotionSourceRoot : IDisposable {
		private readonly string _temporaryFolder;
		private GameObject _instance;
		private AnimationClip _clip;
		private Transform _root;
		private Transform _leftFoot;
		private Transform _rightFoot;
		private Vector3 _originPosition;
		private Quaternion _originRotation;
		private quaternion _initialHeading;
		private float _scale;
		private bool _disposed;

		public quaternion InverseInitialHeading => math.inverse(_initialHeading);

		private MotionSourceRoot(string temporaryFolder) {
			_temporaryFolder = temporaryFolder;
		}

		public RigidTransform Sample(float time) {
			_clip.SampleAnimation(_instance, time);
			// Remove only authored initial yaw, retaining Unity's model axes rather than the FBX bone axes.
			quaternion rotation = _root.rotation * Quaternion.Inverse(_originRotation);
			return new RigidTransform(math.mul(math.mul(InverseInitialHeading, rotation), _initialHeading),
				math.rotate(InverseInitialHeading, (float3)(_root.position - _originPosition) * _scale));
		}

		public float3 PoseOriginOffset(Animator target, RigidTransform correction) {
			float3 sourceFeet = ((float3)_leftFoot.position + (float3)_rightFoot.position) * 0.5f;
			sourceFeet = math.rotate(InverseInitialHeading, (sourceFeet - (float3)_originPosition) * _scale);
			float3 targetFeet = ((float3)target.transform.InverseTransformPoint(target.GetBoneTransform(HumanBodyBones.LeftFoot).position) +
				(float3)target.transform.InverseTransformPoint(target.GetBoneTransform(HumanBodyBones.RightFoot).position)) * 0.5f;
			float3 offset = sourceFeet - math.transform(correction, targetFeet);
			// Trimmed Humanoid clips can retain metres of body-origin displacement inside their pose.
			// Small differences are retargeting proportions and must not rebase ordinary locomotion.
			return math.lengthsq(offset) > 0.25f ? offset : float3.zero;
		}

		public void Dispose() {
			if (_disposed) {
				return;
			}
			_disposed = true;
			if (_instance != null) {
				UnityEngine.Object.DestroyImmediate(_instance);
			}
			AssetDatabase.DeleteAsset(_temporaryFolder);
		}

		public static MotionSourceRoot Create(AnimationClip clip, string rootPath, Animator target) {
			string sourcePath = AssetDatabase.GetAssetPath(clip);
			var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
			if (!sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || sourceModel == null) {
				throw new InvalidOperationException("Authored source root motion requires a source FBX model.");
			}
			var sourceImporter = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
			var sourceRig = sourceImporter.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
				? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(sourceImporter.sourceAvatar)) : sourceModel;
			var sourceAnimator = sourceRig.GetComponent<Animator>();
			if (sourceAnimator == null || sourceAnimator.avatar == null || !sourceAnimator.avatar.isHuman) {
				throw new InvalidOperationException("Authored Humanoid root motion requires the source Avatar.");
			}
			string name = "MmdRootBake_" + Guid.NewGuid().ToString("N");
			const string parent = "Assets/MmdBakeTemporary";
			if (!AssetDatabase.IsValidFolder(parent)) {
				AssetDatabase.CreateFolder("Assets", "MmdBakeTemporary");
			}
			AssetDatabase.CreateFolder(parent, name);
			var owner = new MotionSourceRoot(parent + "/" + name);
			try {
				string temporary = owner._temporaryFolder + "/Source.fbx";
				if (!AssetDatabase.CopyAsset(sourcePath, temporary)) {
					throw new InvalidOperationException("Could not create the temporary source-root FBX.");
				}
				var importer = (ModelImporter)AssetImporter.GetAtPath(temporary);
				importer.animationType = ModelImporterAnimationType.Generic;
				importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
				importer.sourceAvatar = null;
				importer.motionNodeName = string.Empty;
				importer.bakeAxisConversion = false;
				var clips = importer.clipAnimations;
				foreach (var settings in clips) {
					settings.keepOriginalPositionXZ = true;
					settings.keepOriginalPositionY = true;
					settings.keepOriginalOrientation = true;
					settings.lockRootPositionXZ = false;
					settings.lockRootHeightY = false;
					settings.lockRootRotation = false;
				}
				importer.clipAnimations = clips;
				importer.SaveAndReimport();
				owner._clip = AssetDatabase.LoadAllAssetsAtPath(temporary).OfType<AnimationClip>().Single(c => c.name == clip.name);
				owner._instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(temporary));
				owner._instance.hideFlags = HideFlags.HideAndDontSave;
				owner._instance.GetComponent<Animator>().enabled = false;
				owner._root = owner._instance.transform.Find(rootPath);
				if (owner._root == null) {
					throw new InvalidOperationException("Source root path does not identify an FBX transform: " + rootPath);
				}
				string leftPath = AnimationUtility.CalculateTransformPath(sourceAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), sourceRig.transform);
				string rightPath = AnimationUtility.CalculateTransformPath(sourceAnimator.GetBoneTransform(HumanBodyBones.RightFoot), sourceRig.transform);
				owner._leftFoot = owner._instance.transform.Find(leftPath);
				owner._rightFoot = owner._instance.transform.Find(rightPath);
				if (owner._leftFoot == null || owner._rightFoot == null) {
					throw new InvalidOperationException("Source Avatar foot bindings must exist in the authored FBX skeleton.");
				}
				owner._scale = target.humanScale / sourceAnimator.humanScale;
				owner._clip.SampleAnimation(owner._instance, 0f);
				owner._originPosition = owner._root.position;
				owner._originRotation = owner._root.rotation;
				Transform referenceRoot = sourceRig.transform.Find(rootPath);
				if (referenceRoot == null) {
					throw new InvalidOperationException("Source Avatar rig does not contain the root-motion bone: " + rootPath);
				}
				Vector3 forward = (owner._originRotation * Quaternion.Inverse(referenceRoot.rotation)) * Vector3.forward;
				owner._initialHeading = quaternion.RotateY(math.atan2(forward.x, forward.z));
				return owner;
			} catch {
				owner.Dispose();
				throw;
			}
		}
	}
}
