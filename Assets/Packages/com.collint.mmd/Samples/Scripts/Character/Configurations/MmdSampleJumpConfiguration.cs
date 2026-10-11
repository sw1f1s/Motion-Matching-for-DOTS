using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleJumpConfiguration {
		[field: SerializeField, Min(0.01f)]
		public float Height { get; set; } = 1.2f;

		[field: SerializeField, Min(0f), Tooltip("Fallback animation entry time when the clip has no authored lift. The physical jump starts immediately.")]
		public float TakeoffTime { get; set; } = 0.2f;

		[field: SerializeField, Min(0f)]
		public float AirAcceleration { get; set; } = 4f;

		[field: SerializeField]
		public MmdSampleGaitAnimationGroups Groups { get; set; } = new MmdSampleGaitAnimationGroups();
	}
}
