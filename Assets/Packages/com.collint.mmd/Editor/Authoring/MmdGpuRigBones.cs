using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColInt.MMD.Editor.Authoring {
	public static class MmdGpuRigBones {
		public static Transform[] Collect(Transform root, Transform[] hierarchy, SkinnedMeshRenderer[] renderers) {
			var required = new HashSet<Transform>();
			foreach (var renderer in renderers) {
				foreach (Transform bone in renderer.bones) {
					if (bone == null || bone == root) {
						throw new InvalidOperationException("Mesh bones must be non-null descendants of the GPU rig root.");
					}
					for (Transform parent = bone; parent != root; parent = parent.parent) {
						if (parent == null) {
							throw new InvalidOperationException("All mesh bones must belong to the authored GPU rig.");
						}
						required.Add(parent);
					}
				}
			}
			var bones = new List<Transform>(required.Count);
			foreach (Transform transform in hierarchy) {
				if (required.Contains(transform)) {
					bones.Add(transform);
				}
			}
			if (bones.Count != required.Count) {
				throw new InvalidOperationException("The GPU rig hierarchy does not contain all required mesh bones.");
			}
			return bones.ToArray();
		}
	}
}
