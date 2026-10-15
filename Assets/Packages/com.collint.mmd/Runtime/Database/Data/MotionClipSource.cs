namespace ColInt.MMD.Database.Data {
	// Managed bake boundary; runtime clips use ranges into the shared group array.
	public struct MotionClipSource {
		public int FirstFrame;
		public int FrameCount;
		public float Duration;
		public float SampleRate;
		public int[] Groups;
		public int ActionId;
		public MotionPlaybackMode Mode;
		public bool Loop;
	}
}
