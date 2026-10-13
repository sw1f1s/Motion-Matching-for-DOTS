using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleFallConfiguration {
		[field: SerializeField, Min(0.01f)]
		public float MaximumSpeed { get; set; } = 25f;

		[field: SerializeField, Min(0.001f)]
		public float GroundContactDistance { get; set; } = 0.05f;

		[field: SerializeField, Min(0.01f)]
		public float EntrySpeed { get; set; } = 2f;

		[field: SerializeField, Tooltip("Empty disables the sample movement states. Otherwise all jump/landing groups must be assigned.")]
		public string Group { get; set; } = string.Empty;
	}
}
