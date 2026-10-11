using System;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	[Serializable]
	public sealed class MotionBakeGroup {
		[field: SerializeField] public string Name { get; set; } = "New Group";
		[field: SerializeField] public MotionBakeGroupPlayback Playback { get; set; }
		[field: SerializeField] public AnimationClip[] Clips { get; set; } = Array.Empty<AnimationClip>();
	}
}
