using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Graphics.Authoring;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Presentation.Components;
using Unity.Entities;
using UnityEngine;

namespace ColInt.MMD.Editor.Authoring {
	public sealed class MmdGpuCharacterBaker : Baker<MmdGpuCharacterAuthoring> {
		public override void Bake(MmdGpuCharacterAuthoring authoring) {
			Entity root = GetEntity(TransformUsageFlags.Dynamic);
			var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (var renderer in renderers) {
				DependsOn(renderer);
			}
			Transform[] bones = MmdGpuRigBones.Collect(authoring.transform, GetComponentsInChildren<Transform>(), renderers);
			var skeleton = AddBuffer<MmdSkeletonBone>(root);
			var poses = AddBuffer<MmdLocalPose>(root);
			AddBuffer<MmdModelMatrix>(root);
			AddComponent<MmdPlayback>(root);
			foreach (Transform bone in bones) {
				DependsOn(bone);
				skeleton.Add(new MmdSkeletonBone {
					ParentIndex = Array.IndexOf(bones, bone.parent), PathHash = BonePathHash.Calculate(PathFrom(bone, authoring.transform))
				});
				poses.Add(new MmdLocalPose { Value = new JointPose {
					Position = bone.localPosition, Rotation = bone.localRotation, Scale = bone.localScale
				} });
			}
		}

		private static string PathFrom(Transform bone, Transform root) {
			string path = bone.name;
			while (bone.parent != root) {
				bone = bone.parent;
				path = bone.name + "/" + path;
			}
			return path;
		}
	}
}
