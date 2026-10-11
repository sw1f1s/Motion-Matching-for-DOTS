using ColInt.MMD.Database.Data;

namespace ColInt.MMD.Editor.Baking {
	public sealed class MotionBakeSelection {
		public MotionGroup[] Groups { get; set; }
		public MotionBakeClip[] Clips { get; set; }
	}
}
