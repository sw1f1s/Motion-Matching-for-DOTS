using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Database.Data {
	public struct MotionDatabase {
		public BlobArray<MotionGroup> Groups;
		public BlobArray<MotionClip> Clips;
		public BlobArray<MotionFrame> Frames;
		public BlobArray<MotionPoseTrack> PoseTracks;
		public BlobArray<float3> Positions;
		public BlobArray<MotionPackedRotation> Rotations;
		public BlobArray<float3> Scales;
		public BlobArray<float> Features;
		public BlobArray<float> FeatureScales;
		public BlobArray<int> Parents;
		public BlobArray<int> BoneNameHashes;
		public int FeatureCount;
		public int LeftFootBone;
		public int RightFootBone;
	}

}
