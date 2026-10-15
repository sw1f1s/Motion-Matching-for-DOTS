using ColInt.MMD.MotionMatching.Components;
using Unity.Collections;
using Unity.Entities;
using ColInt.MMD.Database.Data;

namespace ColInt.MMD.Playback.Data {
	public struct MmdAnimationLayerDefinition {
		public FixedString64Bytes Name;
		public MmdAnimationLayerSource Source;
		public int GroupId;
		public int ClipIndex;
		public int FirstPose;
		public float Time;
		public float Speed;
		public float Weight;
		public float FadeDuration;
		public float TransitionDuration;
		public MotionPoseChannels Channels;
		public MmdSearchSettings Search;
		public BlobArray<MmdAnimationMaskedBone> Bones;
		public BlobArray<ulong> ActiveGroups;
	}
}
