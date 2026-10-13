using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleTransitionConfiguration {
		[field: SerializeField, Min(0f)]
		public float PoseDuration { get; set; } = 0.2f;

		[field: SerializeField, Min(0f)]
		public float IdlePoseDuration { get; set; } = 0.35f;

		[field: SerializeField, Min(0f)]
		public float RootDuration { get; set; } = 0.1f;
	}
}
