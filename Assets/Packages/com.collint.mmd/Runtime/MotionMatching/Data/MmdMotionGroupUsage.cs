using System;

namespace ColInt.MMD.MotionMatching.Data {
	[Flags]
	public enum MmdMotionGroupUsage : byte {
		None = 0,
		Required = 1,
		Excluded = 2,
		StationaryPose = 4
	}
}
