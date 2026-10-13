using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Database.Helpers {
	public static class MotionDatabaseBuilder {
		public static BlobAssetReference<MotionDatabase> Build(MotionDatabaseSource source) {
			Validate(source);
			MotionPoseTrack[] tracks = CreatePoseTracks(source, out int positions, out int rotations, out int scales);
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var database = ref builder.ConstructRoot<MotionDatabase>();
				database.FeatureCount = source.FeatureCount;
				database.LeftFootBone = source.LeftFootBone;
				database.RightFootBone = source.RightFootBone;
				Copy(builder, ref database.Groups, source.Groups);
				CopyClips(builder, ref database, source.Clips);
				Copy(builder, ref database.Frames, source.Frames);
				Copy(builder, ref database.PoseTracks, tracks);
				PackPoses(builder, ref database, source, tracks, positions, rotations, scales);
				Copy(builder, ref database.Features, source.Features);
				Copy(builder, ref database.FeatureScales, source.FeatureScales);
				Copy(builder, ref database.Parents, source.Parents);
				Copy(builder, ref database.BoneNameHashes, source.BoneNameHashes);
				return builder.CreateBlobAssetReference<MotionDatabase>(Allocator.Persistent);
			}
		}

		public static void Validate(MotionDatabaseSource source) {
			if (source == null || source.Groups == null || source.Clips == null || source.Frames == null || source.Parents == null ||
				source.Poses == null || source.Features == null || source.FeatureScales == null || source.BoneNameHashes == null) {
				throw new ArgumentException("All database arrays are required.");
			}
			if (source.Clips.Length == 0 || source.Parents.Length == 0 || source.FeatureCount <= 0 ||
				source.FeatureScales.Length != source.FeatureCount || source.BoneNameHashes.Length != source.Parents.Length ||
				(long)source.Poses.Length != (long)source.Frames.Length * source.Parents.Length ||
				(long)source.Features.Length != (long)source.Frames.Length * source.FeatureCount) {
				throw new ArgumentException("Database dimensions are inconsistent.");
			}
			if (source.LeftFootBone < -1 || source.RightFootBone < -1 || source.LeftFootBone >= source.Parents.Length ||
				source.RightFootBone >= source.Parents.Length || (source.LeftFootBone < 0) != (source.RightFootBone < 0) ||
				(source.LeftFootBone >= 0 && (source.LeftFootBone == source.RightFootBone || source.FeatureCount != 27))) {
				throw new ArgumentException("Locomotion foot bindings must identify two different bones in the 27-feature schema.");
			}
			var ids = new System.Collections.Generic.HashSet<int>();
			var names = new System.Collections.Generic.HashSet<FixedString64Bytes>();
			var actions = new System.Collections.Generic.HashSet<int>();
			foreach (MotionGroup group in source.Groups) {
				if (group.Name.IsEmpty || group.Id <= 0 || !ids.Add(group.Id) || !names.Add(group.Name) || group.ActionId < 0 ||
					(group.ActionId != 0 && !actions.Add(group.ActionId))) {
					throw new ArgumentException("Invalid or duplicate animation group metadata.");
				}
			}
			for (int bone = 0; bone < source.Parents.Length; bone++) {
				if (source.Parents[bone] < -1 || source.Parents[bone] >= bone) {
					throw new ArgumentException("Bones must be in parent-before-child order.");
				}
			}
			int first = 0;
			for (int clipIndex = 0; clipIndex < source.Clips.Length; clipIndex++) {
				MotionClipSource clip = source.Clips[clipIndex];
				if (clip.Groups == null) {
					throw new ArgumentException("Clip group membership is required.");
				}
				int previousGroup = 0;
				foreach (int group in clip.Groups) {
					if (group <= previousGroup || !ids.Contains(group)) {
						throw new ArgumentException("Clip groups must be sorted, unique catalog ids.");
					}
					previousGroup = group;
				}

				if (clip.FirstFrame != first || clip.FrameCount < 2 || !(clip.Duration > 0f) || !math.isfinite(clip.Duration) ||
					!(clip.SampleRate > 0f) || !math.isfinite(clip.SampleRate) || (clip.ActionId != 0 && clip.Loop) ||
					(long)first + clip.FrameCount > source.Frames.Length) {
					throw new ArgumentException("Clip ranges, timing or action loop mode are invalid.");
				}
				for (int i = 0; i < clip.FrameCount; i++) {
					MotionFrame frame = source.Frames[first + i];
					float expected = math.min(i / clip.SampleRate, clip.Duration);
					if (frame.ClipIndex != clipIndex || !math.isfinite(frame.Time) || math.abs(frame.Time - expected) > 0.0001f ||
						(i > 0 && frame.Time <= source.Frames[first + i - 1].Time) ||
						!math.all(math.isfinite(frame.Root.pos)) || !UnitQuaternion(frame.Root.rot)) {
						throw new ArgumentException("Invalid root samples or clip frame timing.");
					}
				}
				if (math.abs(source.Frames[first + clip.FrameCount - 1].Time - clip.Duration) > 0.0001f) {
					throw new ArgumentException("Every clip needs its exact endpoint sample.");
				}
				first += clip.FrameCount;
			}
			if (first != source.Frames.Length) {
				throw new ArgumentException("Unreferenced frames in database.");
			}
			if (!math.isfinite(source.MaximumRotationErrorDegrees) || source.MaximumRotationErrorDegrees <= 0f || source.MaximumRotationErrorDegrees > MmdRotationPacking.DefaultMaximumErrorDegrees) {
				throw new ArgumentException("Maximum rotation error must be positive and at most 0.05 degrees.");
			}
			int poseIndex = 0;
			foreach (JointPose pose in source.Poses) {
				if (!math.all(math.isfinite(pose.Position)) || !UnitQuaternion(pose.Rotation) ||
					!math.all(math.isfinite(pose.Scale)) || math.any(pose.Scale <= 0f)) {
					throw new ArgumentException("Invalid joint pose.");
				}
				float error = MmdRotationPacking.ErrorDegrees(pose.Rotation, MmdRotationPacking.Unpack(MmdRotationPacking.Pack(pose.Rotation)));
				if (error > source.MaximumRotationErrorDegrees) {
					throw new ArgumentException($"Rotation packing error {error:F6} degrees exceeds {source.MaximumRotationErrorDegrees:F6} at frame {poseIndex / source.Parents.Length}, bone {poseIndex % source.Parents.Length}.");
				}
				poseIndex++;
			}
			foreach (float value in source.Features) {
				if (!math.isfinite(value)) {
					throw new ArgumentException("Features must be finite.");
				}
			}
			foreach (float scale in source.FeatureScales) {
				if (!math.isfinite(scale) || scale <= 0f) {
					throw new ArgumentException("Feature scales must be finite and positive.");
				}
			}
		}

		public static void Validate(ref MotionDatabase database) {
			if (database.Clips.Length == 0 || database.Parents.Length == 0 || database.FeatureCount <= 0 ||
				database.FeatureScales.Length != database.FeatureCount || database.BoneNameHashes.Length != database.Parents.Length ||
				(long)database.PoseTracks.Length != (long)database.Clips.Length * database.Parents.Length ||
				(long)database.Features.Length != (long)database.Frames.Length * database.FeatureCount) {
				throw new ArgumentException("Database dimensions are inconsistent.");
			}
			if (database.LeftFootBone < -1 || database.RightFootBone < -1 || database.LeftFootBone >= database.Parents.Length ||
				database.RightFootBone >= database.Parents.Length || (database.LeftFootBone < 0) != (database.RightFootBone < 0) ||
				(database.LeftFootBone >= 0 && (database.LeftFootBone == database.RightFootBone || database.FeatureCount != 27))) {
				throw new ArgumentException("Locomotion foot bindings must identify two different bones in the 27-feature schema.");
			}
			var ids = new System.Collections.Generic.HashSet<int>();
			var names = new System.Collections.Generic.HashSet<FixedString64Bytes>();
			var actions = new System.Collections.Generic.HashSet<int>();
			for (int index = 0; index < database.Groups.Length; index++) {
				MotionGroup group = database.Groups[index];
				if (group.Name.IsEmpty || group.Id <= 0 || !ids.Add(group.Id) || !names.Add(group.Name) || group.ActionId < 0 ||
					(group.ActionId != 0 && !actions.Add(group.ActionId))) {
					throw new ArgumentException("Invalid or duplicate animation group metadata.");
				}
			}
			for (int bone = 0; bone < database.Parents.Length; bone++) {
				if (database.Parents[bone] < -1 || database.Parents[bone] >= bone) {
					throw new ArgumentException("Bones must be in parent-before-child order.");
				}
			}
			int first = 0;
			for (int clipIndex = 0; clipIndex < database.Clips.Length; clipIndex++) {
				MotionClip clip = database.Clips[clipIndex];
				if (clip.FirstGroupWord < 0 || clip.GroupWordCount < 0 || (long)clip.FirstGroupWord + clip.GroupWordCount > database.ClipGroupWords.Length) {
					throw new ArgumentException("Invalid clip group mask range.");
				}
				foreach (int group in MotionDatabaseGroups.GetIds(ref database, clip)) {
					if (!ids.Contains(group)) {
						throw new ArgumentException("Clip mask contains an undeclared group.");
					}
				}

				if (clip.FirstFrame != first || clip.FrameCount < 2 || !(clip.Duration > 0f) || !math.isfinite(clip.Duration) ||
					!(clip.SampleRate > 0f) || !math.isfinite(clip.SampleRate) || (clip.ActionId != 0 && clip.Loop) ||
					(long)first + clip.FrameCount > database.Frames.Length) {
					throw new ArgumentException("Clip ranges, timing or action loop mode are invalid.");
				}
				for (int i = 0; i < clip.FrameCount; i++) {
					MotionFrame frame = database.Frames[first + i];
					float expected = math.min(i / clip.SampleRate, clip.Duration);
					if (frame.ClipIndex != clipIndex || !math.isfinite(frame.Time) || math.abs(frame.Time - expected) > 0.0001f ||
						(i > 0 && frame.Time <= database.Frames[first + i - 1].Time) ||
						!math.all(math.isfinite(frame.Root.pos)) || !UnitQuaternion(frame.Root.rot)) {
						throw new ArgumentException("Invalid root samples or clip frame timing.");
					}
				}
				if (math.abs(database.Frames[first + clip.FrameCount - 1].Time - clip.Duration) > 0.0001f) {
					throw new ArgumentException("Every clip needs its exact endpoint sample.");
				}
				first += clip.FrameCount;
			}
			if (first != database.Frames.Length) {
				throw new ArgumentException("Unreferenced frames in database.");
			}
			ValidatePoseTracks(ref database);
			for (int index = 0; index < database.Positions.Length; index++) {
				if (!math.all(math.isfinite(database.Positions[index]))) {
					throw new ArgumentException("Position channels must be finite.");
				}
			}
			for (int index = 0; index < database.Rotations.Length; index++) {
				if (!MmdRotationPacking.IsValid(database.Rotations[index])) {
					throw new ArgumentException("Rotation channels must contain valid packed unit quaternions.");
				}
			}
			for (int index = 0; index < database.Scales.Length; index++) {
				float3 scale = database.Scales[index];
				if (!math.all(math.isfinite(scale)) || math.any(scale <= 0f)) {
					throw new ArgumentException("Scale channels must be finite and positive.");
				}
			}
			for (int index = 0; index < database.Features.Length; index++) {
				float value = database.Features[index];
				if (!math.isfinite(value)) {
					throw new ArgumentException("Features must be finite.");
				}
			}
			for (int index = 0; index < database.FeatureScales.Length; index++) {
				float scale = database.FeatureScales[index];
				if (!math.isfinite(scale) || scale <= 0f) {
					throw new ArgumentException("Feature scales must be finite and positive.");
				}
			}
		}

		internal static MotionPoseTrack[] CreatePoseTracks(MotionDatabaseSource source, out int positions, out int rotations, out int scales) {
			int bones = source.Parents.Length;
			var tracks = new MotionPoseTrack[source.Clips.Length * bones];
			positions = 0;
			rotations = 0;
			scales = 0;
			for (int clipIndex = 0; clipIndex < source.Clips.Length; clipIndex++) {
				MotionClipSource clip = source.Clips[clipIndex];
				for (int bone = 0; bone < bones; bone++) {
					JointPose first = source.Poses[clip.FirstFrame * bones + bone];
					MotionPoseChannels channels = MotionPoseChannels.None;
					for (int sample = 1; sample < clip.FrameCount && channels != MotionPoseChannels.All; sample++) {
						JointPose pose = source.Poses[(clip.FirstFrame + sample) * bones + bone];
						if (!math.all(math.asint(first.Position) == math.asint(pose.Position))) {
							channels |= MotionPoseChannels.Position;
						}
						if (!math.all(math.asint(first.Rotation.value) == math.asint(pose.Rotation.value))) {
							channels |= MotionPoseChannels.Rotation;
						}
						if (!math.all(math.asint(first.Scale) == math.asint(pose.Scale))) {
							channels |= MotionPoseChannels.Scale;
						}
					}
					tracks[clipIndex * bones + bone] = new MotionPoseTrack {
						PositionOffset = positions, RotationOffset = rotations, ScaleOffset = scales, AnimatedChannels = channels
					};
					positions += (channels & MotionPoseChannels.Position) != 0 ? clip.FrameCount : 1;
					rotations += (channels & MotionPoseChannels.Rotation) != 0 ? clip.FrameCount : 1;
					scales += (channels & MotionPoseChannels.Scale) != 0 ? clip.FrameCount : 1;
				}
			}
			return tracks;
		}

		private static void PackPoses(BlobBuilder builder, ref MotionDatabase database, MotionDatabaseSource source,
			MotionPoseTrack[] tracks, int positions, int rotations, int scales) {
			var positionSamples = builder.Allocate(ref database.Positions, positions);
			var rotationSamples = builder.Allocate(ref database.Rotations, rotations);
			var scaleSamples = builder.Allocate(ref database.Scales, scales);
			int bones = source.Parents.Length;
			for (int clipIndex = 0; clipIndex < source.Clips.Length; clipIndex++) {
				MotionClipSource clip = source.Clips[clipIndex];
				for (int bone = 0; bone < bones; bone++) {
					MotionPoseTrack track = tracks[clipIndex * bones + bone];
					for (int sample = 0; sample < clip.FrameCount; sample++) {
						JointPose pose = source.Poses[(clip.FirstFrame + sample) * bones + bone];
						if (sample == 0 || (track.AnimatedChannels & MotionPoseChannels.Position) != 0) {
							positionSamples[track.PositionOffset + sample] = pose.Position;
						}
						if (sample == 0 || (track.AnimatedChannels & MotionPoseChannels.Rotation) != 0) {
							rotationSamples[track.RotationOffset + sample] = MmdRotationPacking.Pack(pose.Rotation);
						}
						if (sample == 0 || (track.AnimatedChannels & MotionPoseChannels.Scale) != 0) {
							scaleSamples[track.ScaleOffset + sample] = pose.Scale;
						}
					}
				}
			}
		}

		private static void ValidatePoseTracks(ref MotionDatabase database) {
			long positions = 0;
			long rotations = 0;
			long scales = 0;
			for (int clipIndex = 0; clipIndex < database.Clips.Length; clipIndex++) {
				int samples = database.Clips[clipIndex].FrameCount;
				for (int bone = 0; bone < database.Parents.Length; bone++) {
					MotionPoseTrack track = database.PoseTracks[clipIndex * database.Parents.Length + bone];
					if ((track.AnimatedChannels & ~MotionPoseChannels.All) != 0 ||
						track.PositionOffset != positions || track.RotationOffset != rotations || track.ScaleOffset != scales) {
						throw new ArgumentException("Invalid pose channel flags or offsets.");
					}
					positions += (track.AnimatedChannels & MotionPoseChannels.Position) != 0 ? samples : 1;
					rotations += (track.AnimatedChannels & MotionPoseChannels.Rotation) != 0 ? samples : 1;
					scales += (track.AnimatedChannels & MotionPoseChannels.Scale) != 0 ? samples : 1;
				}
			}
			if (positions != database.Positions.Length || rotations != database.Rotations.Length || scales != database.Scales.Length) {
				throw new ArgumentException("Pose channel sample counts are inconsistent.");
			}
		}

		private static bool UnitQuaternion(quaternion value) {
			return math.all(math.isfinite(value.value)) && math.abs(math.lengthsq(value.value) - 1f) < 0.001f;
		}

		internal static void CopyClips(BlobBuilder builder, ref MotionDatabase database, MotionClipSource[] source) {
			int count = 0;
			foreach (var clip in source) {
				int previous = 0;
				foreach (int group in clip.Groups) {
					if (group <= previous) {
						throw new ArgumentException("Clip groups must be positive, sorted and unique.");
					}
					previous = group;
				}
				count = checked(count + BitMask.WordsForBits(previous));
			}
			var clips = builder.Allocate(ref database.Clips, source.Length);
			var words = builder.Allocate(ref database.ClipGroupWords, count);
			int offset = 0;
			for (int index = 0; index < source.Length; index++) {
				var clip = source[index];
				int wordCount = clip.Groups.Length == 0 ? 0 : BitMask.WordsForBits(clip.Groups[clip.Groups.Length - 1]);
				clips[index] = new MotionClip {
					FirstFrame = clip.FirstFrame, FrameCount = clip.FrameCount, Duration = clip.Duration, SampleRate = clip.SampleRate,
					ActionId = clip.ActionId, Loop = clip.Loop, FirstGroupWord = offset, GroupWordCount = wordCount
				};
				for (int word = 0; word < wordCount; word++) {
					words[offset + word] = 0;
				}
				foreach (int group in clip.Groups) {
					int bit = group - 1;
					words[offset + (bit >> 6)] |= 1UL << (bit & 63);
				}
				offset += wordCount;
			}
		}

		private static void Copy<T>(BlobBuilder builder, ref BlobArray<T> destination, T[] source) where T : unmanaged {
			var array = builder.Allocate(ref destination, source.Length);
			for (int i = 0; i < source.Length; i++) {
				array[i] = source[i];
			}
		}
	}

}
