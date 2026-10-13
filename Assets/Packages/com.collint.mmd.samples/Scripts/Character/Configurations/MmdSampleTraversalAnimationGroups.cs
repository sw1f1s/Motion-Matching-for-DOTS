using System;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleTraversalAnimationGroups {
		[field: SerializeField]
		public string Mantle { get; set; } = string.Empty;

		[field: SerializeField]
		public string Climb { get; set; } = string.Empty;

		[field: SerializeField]
		public string Vault { get; set; } = string.Empty;

		[field: SerializeField]
		public string Hurdle { get; set; } = string.Empty;

		[field: SerializeField]
		public string CatchMantle { get; set; } = string.Empty;

		[field: SerializeField]
		public string CatchCliff { get; set; } = string.Empty;

		[field: SerializeField]
		public string CatchVault { get; set; } = string.Empty;

		[field: SerializeField]
		public string CatchHurdle { get; set; } = string.Empty;
	}
}
