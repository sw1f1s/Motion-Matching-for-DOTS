using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleLocomotionConfiguration {
		[field: SerializeField, Min(0f)]
		public float WalkSpeed { get; set; } = 1.5f;

		[field: SerializeField, Min(0f)]
		public float RunSpeed { get; set; } = 4f;

		[field: SerializeField, Min(0f)]
		public float TurnSpeed { get; set; } = 1.5f;

		[field: SerializeField]
		public string IdleGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string WalkGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string RunGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string TurnGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string StartGroup { get; set; } = string.Empty;
	}
}
