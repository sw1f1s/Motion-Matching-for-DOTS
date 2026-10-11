
namespace ColInt.MMD.Database.Data {
	// Managed loading/baking boundary; never stored in ECS.
	public sealed class MotionDatabaseSource {
		public MotionGroup[] Groups { get; set; } = System.Array.Empty<MotionGroup>();
		public MotionClip[] Clips { get; set; }
		public MotionFrame[] Frames { get; set; }
		public JointPose[] Poses { get; set; }
		public float[] Features { get; set; }
		public float[] FeatureScales { get; set; }
		public int[] Parents { get; set; }
		public int[] BoneNameHashes { get; set; }
		public int FeatureCount { get; set; }
		public float MaximumRotationErrorDegrees { get; set; } = 0.05f;
		public int LeftFootBone { get; set; } = -1;
		public int RightFootBone { get; set; } = -1;
	}

}
