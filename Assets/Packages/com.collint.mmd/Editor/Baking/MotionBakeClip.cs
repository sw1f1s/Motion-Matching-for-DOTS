using ColInt.MMD.Database.Data;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	// Resolved bake input. Authoring is stored in named groups and the clips' own import settings.
	public sealed class MotionBakeClip {
		public AnimationClip Clip { get; set; }
		public bool Loop { get; set; }
		public int[] Groups { get; set; } = System.Array.Empty<int>();
		public int ActionId { get; set; }
		public MotionPlaybackMode Mode { get; set; }
	}
}
