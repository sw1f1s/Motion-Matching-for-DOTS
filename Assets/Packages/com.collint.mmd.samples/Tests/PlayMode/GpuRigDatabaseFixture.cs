using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
#if UNITY_EDITOR
using System.Linq;
using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public static class GpuRigDatabaseFixture {
		public static BlobAssetReference<MotionDatabase> Build(GameObject prefab) {
			Transform[] bones = prefab.GetComponentsInChildren<Transform>(true).Where(t => t != prefab.transform).ToArray();
			var source = MotionDatabaseFixture.Create();
			source.Parents = bones.Select(b => Array.IndexOf(bones, b.parent)).ToArray();
			source.BoneNameHashes = bones.Select(b => BonePathHash.Calculate(AnimationUtility.CalculateTransformPath(b, prefab.transform))).ToArray();
			source.Poses = new JointPose[source.Frames.Length * bones.Length];
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int bone = 0; bone < bones.Length; bone++) {
					source.Poses[frame * bones.Length + bone] = new JointPose {
						Position = bones[bone].localPosition, Rotation = bones[bone].localRotation, Scale = bones[bone].localScale
					};
				}
			}
			return MotionDatabaseBuilder.Build(source);
		}
	}
}
#endif
