using System;
using System.Collections.Generic;
using ColInt.MMD.Playback.Authoring;
using ColInt.MMD.Playback.Data;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Editor.Playback {
	public static class MmdAnimationMaskEditing {
		public static void Synchronize(MmdAnimationMask mask) {
			if (mask.Rig == null) {
				throw new ArgumentException("Choose the same target rig used to bake the database.");
			}
			var previous = new Dictionary<string, float>(StringComparer.Ordinal);
			foreach (var bone in mask.Bones) {
				previous[bone.Path] = bone.Weight;
			}
			var weights = new List<MmdAnimationBoneWeight>();
			foreach (Transform bone in mask.Rig.GetComponentsInChildren<Transform>(true)) {
				if (bone == mask.Rig.transform) {
					continue;
				}
				string path = AnimationUtility.CalculateTransformPath(bone, mask.Rig.transform);
				weights.Add(new MmdAnimationBoneWeight { Path = path, Weight = previous.TryGetValue(path, out float weight) ? weight : 0f });
			}
			Undo.RecordObject(mask, "Synchronize animation mask");
			mask.Configure(mask.Rig, weights.ToArray());
			EditorUtility.SetDirty(mask);
		}

		public static void SetWeight(MmdAnimationMask mask, string path, float weight, bool descendants) {
			Undo.RecordObject(mask, "Change animation mask weights");
			var bones = (MmdAnimationBoneWeight[])mask.Bones.Clone();
			for (int index = 0; index < bones.Length; index++) {
				var bone = bones[index];
				if (path == null || bone.Path == path || descendants && bone.Path.StartsWith(path + "/", StringComparison.Ordinal)) {
					bone.Weight = Mathf.Clamp01(weight);
					bones[index] = bone;
				}
			}
			mask.Configure(mask.Rig, bones);
			EditorUtility.SetDirty(mask);
		}

		public static void SetHumanoidPreset(MmdAnimationMask mask, HumanBodyBones start) {
			if (mask.Rig == null) {
				throw new ArgumentException("Choose a target rig before applying a preset.");
			}
			var animator = mask.Rig.GetComponent<Animator>();
			if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid) {
				throw new ArgumentException("Arm presets require a valid Humanoid avatar. For a Generic rig, select a shoulder branch manually.");
			}
			Transform bone = animator.GetBoneTransform(start);
			if (bone == null && (start == HumanBodyBones.RightShoulder || start == HumanBodyBones.LeftShoulder)) {
				bone = animator.GetBoneTransform(start == HumanBodyBones.RightShoulder ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
			}
			if (bone == null) {
				throw new ArgumentException("The Humanoid avatar does not contain this bone.");
			}
			Undo.IncrementCurrentGroup();
			int group = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Set animation mask preset");
			Synchronize(mask);
			SetWeight(mask, null, 0f, false);
			SetWeight(mask, AnimationUtility.CalculateTransformPath(bone, mask.Rig.transform), 1f, true);
			Undo.CollapseUndoOperations(group);
		}
	}
}
