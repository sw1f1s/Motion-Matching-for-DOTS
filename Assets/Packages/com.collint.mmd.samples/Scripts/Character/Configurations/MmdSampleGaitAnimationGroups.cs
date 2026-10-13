using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleGaitAnimationGroups {
		[field: SerializeField]
		public string Stand { get; set; } = string.Empty;

		[field: SerializeField]
		public string Walk { get; set; } = string.Empty;

		[field: SerializeField]
		public string Run { get; set; } = string.Empty;
	}
}
