using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleLandingConfiguration {
		[field: SerializeField, Min(0.01f)]
		public float HeavyHeight { get; set; } = 3f;

		[field: SerializeField, Min(0.01f)]
		public float LightRecoveryTime { get; set; } = 0.08f;

		[field: SerializeField, Min(0.01f)]
		public float HeavyRecoveryTime { get; set; } = 0.9f;

		[field: SerializeField]
		public MmdSampleGaitAnimationGroups LightGroups { get; set; } = new MmdSampleGaitAnimationGroups();

		[field: SerializeField]
		public MmdSampleGaitAnimationGroups HeavyGroups { get; set; } = new MmdSampleGaitAnimationGroups();
	}
}
