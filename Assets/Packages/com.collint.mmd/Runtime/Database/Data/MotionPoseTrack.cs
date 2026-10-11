namespace ColInt.MMD.Database.Data {
	// One track per clip/bone. Constant channels have one sample; animated channels have FrameCount samples.
	public struct MotionPoseTrack {
		public int PositionOffset;
		public int RotationOffset;
		public int ScaleOffset;
		public MotionPoseChannels AnimatedChannels;
	}
}
