using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	// Resolved bake input. Authoring is stored in named groups and the clips' own import settings.
	public sealed class MotionBakeClip {
		public AnimationClip Clip { get; set; }
		public bool Loop { get; set; }
		public uint Tags { get; set; }
		public int ActionId { get; set; }
	}
}
