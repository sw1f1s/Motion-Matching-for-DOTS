using System;
using ColInt.MMD.Playback.Data;
using UnityEngine;

namespace ColInt.MMD.Playback.Authoring {
	[CreateAssetMenu(fileName = "AnimationMask", menuName = "MMD/Animation Mask")]
	public sealed class MmdAnimationMask : ScriptableObject {
		[SerializeField] private GameObject _rig;
		[SerializeField] private MmdAnimationBoneWeight[] _bones = Array.Empty<MmdAnimationBoneWeight>();

		public GameObject Rig => _rig;
		public MmdAnimationBoneWeight[] Bones => _bones;

		public void Configure(GameObject rig, MmdAnimationBoneWeight[] bones) {
			if (rig == null || bones == null) {
				throw new ArgumentException("A target rig and bone weights are required.");
			}
			_rig = rig;
			_bones = (MmdAnimationBoneWeight[])bones.Clone();
		}
	}
}
