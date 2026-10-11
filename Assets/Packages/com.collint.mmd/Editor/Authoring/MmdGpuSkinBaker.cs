using System.Linq;
using System;
using ColInt.MMD.Graphics.Authoring;
using ColInt.MMD.Graphics.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ColInt.MMD.Editor.Authoring {
	public sealed class MmdGpuSkinBaker : Baker<MmdGpuSkinAuthoring> {
		public override void Bake(MmdGpuSkinAuthoring authoring) {
			var rig = GetComponentInParent<MmdGpuCharacterAuthoring>();
			var renderer = GetComponent<SkinnedMeshRenderer>();
			if (rig == null || renderer.rootBone == null || renderer.sharedMesh == null || renderer.bones.Length != renderer.sharedMesh.bindposeCount) {
				throw new InvalidOperationException("A GPU rig root, mesh and matching bind-pose array are required.");
			}
			DependsOn(renderer.sharedMesh);
			DependsOn(rig.transform);
			DependsOn(renderer.rootBone);
			Transform[] bones = GetComponentsInChildren<Transform>(rig).Where(b => b != rig.transform).ToArray();
			Entity skin = GetEntity(TransformUsageFlags.Dynamic);
			float4x4 renderRoot = math.mul((float4x4)rig.transform.worldToLocalMatrix, (float4x4)renderer.rootBone.localToWorldMatrix);
			AddComponent(skin, new MmdSkinSource { PoseEntity = GetEntity(rig, TransformUsageFlags.Dynamic), ModelToMesh = math.inverse(renderRoot) });
			var bindings = AddBuffer<MmdSkinBinding>(skin);
			Matrix4x4[] bindPoses = renderer.sharedMesh.bindposes;
			for (int i = 0; i < renderer.bones.Length; i++) {
				int boneIndex = Array.IndexOf(bones, renderer.bones[i]);
				if (boneIndex < 0) {
					throw new InvalidOperationException("All mesh bones must belong to the authored rig.");
				}
				bindings.Add(new MmdSkinBinding { BoneIndex = boneIndex, BindPose = bindPoses[i] });
			}
		}
	}
}
