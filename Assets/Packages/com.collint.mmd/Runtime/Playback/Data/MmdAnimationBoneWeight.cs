using System;
using UnityEngine;

namespace ColInt.MMD.Playback.Data {
	[Serializable]
	public struct MmdAnimationBoneWeight {
		[field: SerializeField] public string Path { get; set; }
		[field: SerializeField, Range(0f, 1f)] public float Weight { get; set; }
	}
}
