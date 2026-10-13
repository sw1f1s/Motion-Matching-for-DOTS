using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleSlideConfiguration {
		[field: SerializeField, Min(0.01f)] public float MinimumEntrySpeed { get; set; } = 4.5f;
		[field: SerializeField, Min(0.01f)] public float Deceleration { get; set; } = 3f;
		[field: SerializeField, Min(0.01f)] public float StopSpeed { get; set; } = 0.35f;
		[field: SerializeField, Min(0.01f)] public float MaximumSpeed { get; set; } = 14f;
		[field: SerializeField, Min(0f), Tooltip("Slide steering speed in radians per second.")] public float SteeringSpeed { get; set; } = 2f;
		[field: SerializeField, Range(1f, 45f)] public float MinimumDownhillAngle { get; set; } = 5f;
		[field: SerializeField] public string EnterGroup { get; set; } = string.Empty;
		[field: SerializeField, Tooltip("Looping slide variants and their non-looping exits into crouch.")] public string MotionGroup { get; set; } = string.Empty;
	}
}
