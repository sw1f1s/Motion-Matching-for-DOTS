using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleCrouchConfiguration {
		[field: SerializeField, Min(0.01f)]
		public float Speed { get; set; } = 1.2f;

		[field: SerializeField, Min(0.01f), Tooltip("Authored forward speed in the crouch movement clips, in metres per second. Playback tempo scales it to Speed.")]
		public float AnimationSpeed { get; set; } = 2.25f;

		[field: SerializeField, Range(0.6f, 1.79f)]
		public float Height { get; set; } = 1.2f;

		[field: SerializeField, Min(0.01f)]
		public float TransitionDuration { get; set; } = 0.35f;

		[field: SerializeField]
		public string IdleGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string MoveGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string TurnGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string StartGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string EnterGroup { get; set; } = string.Empty;

		[field: SerializeField]
		public string ExitGroup { get; set; } = string.Empty;
	}
}
