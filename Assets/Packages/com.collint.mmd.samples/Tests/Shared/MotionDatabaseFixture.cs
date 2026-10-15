using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Helpers;
using Unity.Mathematics;

namespace ColInt.MMD.Tests {
	public static class MotionDatabaseFixture {
		public static MotionDatabaseSource Create() {
			const int samples = 11;
			var source = new MotionDatabaseSource {
				FeatureCount = LocomotionSchema.Dimension,
				Groups = new[] {
					new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Idle"), Id = 1 },
					new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Walk"), Id = 2 },
					new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Action"), Id = 4, ActionId = 7, Mode = MotionPlaybackMode.Action },
					new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Other"), Id = 8 }
				},
				Parents = new[] { -1, 0 }, BoneNameHashes = new[] { 1, 2 },
				Clips = new MotionClipSource[3], Frames = new MotionFrame[samples * 3],
				Poses = new JointPose[samples * 3 * 2], Features = new float[samples * 3 * LocomotionSchema.Dimension],
				FeatureScales = new float[LocomotionSchema.Dimension]
			};
			for (int feature = 0; feature < source.FeatureScales.Length; feature++) {
				source.FeatureScales[feature] = 1f;
			}
			for (int clip = 0; clip < 3; clip++) {
				source.Clips[clip] = new MotionClipSource {
					FirstFrame = clip * samples, FrameCount = samples, SampleRate = 10f, Duration = 1f,
					Loop = clip != 2, ActionId = clip == 2 ? 7 : 0, Mode = clip == 2 ? MotionPlaybackMode.Action : MotionPlaybackMode.MotionMatching, Groups = new[] { 1 << clip }
				};
				for (int sample = 0; sample < samples; sample++) {
					int frame = clip * samples + sample;
					float time = sample / 10f;
					source.Frames[frame] = new MotionFrame {
						ClipIndex = clip, Time = time, Root = new RigidTransform(quaternion.identity, new float3(0f, 0f, clip * time))
					};
					source.Poses[frame * 2] = new JointPose { Rotation = quaternion.identity, Scale = new float3(1f) };
					source.Poses[frame * 2 + 1] = new JointPose {
						Position = new float3(0f, 1f + time, 0f), Rotation = quaternion.identity, Scale = new float3(1f)
					};
					for (int i = 0; i < 3; i++) {
						source.Features[frame * source.FeatureCount + i * 4 + 1] = clip * LocomotionSchema.PredictionTimes[i];
						source.Features[frame * source.FeatureCount + i * 4 + 3] = 1f;
					}
					source.Features[frame * source.FeatureCount + 14] = clip;
				}
			}
			return source;
		}
	}

}
