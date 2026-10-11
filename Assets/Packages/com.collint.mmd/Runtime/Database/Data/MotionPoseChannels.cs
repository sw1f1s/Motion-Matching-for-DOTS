using System;

namespace ColInt.MMD.Database.Data {
	[Flags]
	public enum MotionPoseChannels : byte {
		None = 0,
		Position = 1,
		Rotation = 2,
		Scale = 4,
		All = Position | Rotation | Scale
	}
}
