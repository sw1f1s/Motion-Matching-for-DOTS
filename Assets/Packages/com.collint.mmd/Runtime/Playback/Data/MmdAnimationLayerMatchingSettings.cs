using System;
using UnityEngine;

namespace ColInt.MMD.Playback.Data {
	[Serializable]
	public sealed class MmdAnimationLayerMatchingSettings {
		[field: SerializeField, Min(0f)] public float Interval { get; set; } = 0.1f;
		[field: SerializeField, Min(0f)] public float SwitchPenalty { get; set; } = 1f;
		[field: SerializeField, Min(0f)] public float MinimumRemainingTime { get; set; } = 0.1f;
		[field: SerializeField, Min(0f)] public float MinimumSwitchTime { get; set; } = 0.2f;
		[field: SerializeField, Min(0f)] public float TransitionDuration { get; set; } = 0.2f;
	}
}
