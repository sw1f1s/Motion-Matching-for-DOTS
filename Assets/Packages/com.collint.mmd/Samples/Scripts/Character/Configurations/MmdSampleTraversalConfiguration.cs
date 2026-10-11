using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleTraversalConfiguration {
		[field: SerializeField, Min(0.1f)]
		public float Reach { get; set; } = 1.35f;

		[field: SerializeField, Min(0.1f)]
		public float MinimumHeight { get; set; } = 0.45f;

		[field: SerializeField, Min(0.1f)]
		public float MaximumHeight { get; set; } = 2.8f;

		[field: SerializeField, Min(0.1f), Tooltip("Maximum height for the low ledge animation. With 1 m Mantle and 2.5 m Climb clips, 1.75 m selects the nearer authored height.")]
		public float MantleHeight { get; set; } = 1.75f;

		[field: SerializeField, Min(0.1f)]
		public float MaximumCrossingDepth { get; set; } = 1.2f;

		[field: SerializeField, Min(0f)]
		public float MaximumLandingDrop { get; set; } = 1.2f;

		[field: SerializeField, Min(0.1f), Tooltip("Maximum ledge height above the character during an airborne catch.")]
		public float CatchReach { get; set; } = 2.2f;

		[field: SerializeField, Min(0f), Tooltip("Maximum horizontal gap from the character capsule to the wall when catching from a jump or fall.")]
		public float CatchDistance { get; set; } = 0.25f;

		[field: SerializeField, Min(0.01f)]
		public float AlignmentDuration { get; set; } = 0.3f;

		[field: SerializeField]
		public MmdSampleTraversalAnimationGroups Groups { get; set; } = new MmdSampleTraversalAnimationGroups();
	}
}
