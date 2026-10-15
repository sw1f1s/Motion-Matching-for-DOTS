using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Components;
using Unity.Collections;

namespace ColInt.MMD.Playback.Data {
	internal struct MmdAnimationLayerInitialization {
		public FixedString64Bytes Name;
		public int FirstGroupWord;
		public int GroupWordCount;
		public int ClipIndex;
		// Zero selects a fixed clip; a positive id enables matching within that group.
		public int GroupId;
		public MmdSearchSettings SearchSettings;
		public float Time;
		public float Speed;
		public float TargetWeight;
		public float FadeDuration;
		public MotionPoseChannels Channels;
	}
}
