using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Playback.Authoring;
using UnityEngine;

namespace ColInt.MMD.Playback.Data {
	[Serializable]
	public sealed class MmdAnimationLayerSettings {
		[field: SerializeField] public string Name { get; set; } = "Weapon";
		[field: SerializeField] public MmdAnimationMask Mask { get; set; }
		[field: SerializeField] public string[] ActiveGroups { get; set; } = Array.Empty<string>();
		[field: SerializeField] public MmdAnimationLayerSource Source { get; set; } = MmdAnimationLayerSource.Clip;
		[field: SerializeField] public string Group { get; set; }
		[field: SerializeField, Min(0)] public int ClipInGroup { get; set; }
		[field: SerializeField] public MmdAnimationLayerMatchingSettings Matching { get; set; } = new MmdAnimationLayerMatchingSettings();
		[field: SerializeField, Min(0f)] public float Time { get; set; }
		[field: SerializeField, Min(0f), Tooltip("Zero holds the selected pose. Otherwise the clip advances independently of locomotion.")] public float Speed { get; set; }
		[field: SerializeField, Range(0f, 1f)] public float Weight { get; set; } = 1f;
		[field: SerializeField, Min(0f), Tooltip("Seconds to fade across the full 0–1 weight range.")] public float FadeDuration { get; set; } = 0.2f;
		[field: SerializeField] public MotionPoseChannels Channels { get; set; } = MotionPoseChannels.Rotation;
	}
}
