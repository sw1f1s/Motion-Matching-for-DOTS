using System.IO;
using System.Text;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Playback.Helpers;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Database.Helpers {
	public static class MotionDatabaseCodec {
		public const int Version = 5;
		private const int GroupRecordBytes = 70;
		private const uint Magic = 0x31444D4D;
		private const int MaximumElements = 100000000;

		public static byte[] Write(MotionDatabaseSource source) {
			MotionDatabaseBuilder.Validate(source);
			MotionPoseTrack[] tracks = MotionDatabaseBuilder.CreatePoseTracks(source, out int positions, out int rotations, out int scales);
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream)) {
					writer.Write(Magic);
					writer.Write(Version);
					writer.Write(source.FeatureCount);
					writer.Write(source.Parents.Length);
					writer.Write(source.Clips.Length);
					writer.Write(source.Frames.Length);
					writer.Write(positions);
					writer.Write(rotations);
					writer.Write(scales);
					for (int bone = 0; bone < source.Parents.Length; bone++) {
						writer.Write(source.Parents[bone]);
						writer.Write(source.BoneNameHashes[bone]);
					}
					foreach (MotionClip clip in source.Clips) {
						writer.Write(clip.FirstFrame);
						writer.Write(clip.FrameCount);
						writer.Write(clip.Duration);
						writer.Write(clip.SampleRate);
						writer.Write(clip.Tags);
						writer.Write(clip.ActionId);
						writer.Write(clip.Loop);
					}
					foreach (MotionFrame frame in source.Frames) {
						writer.Write(frame.ClipIndex);
						writer.Write(frame.Time);
						WriteVector(writer, frame.Root.pos);
						WriteRotation(writer, frame.Root.rot);
					}
					foreach (MotionPoseTrack track in tracks) {
						writer.Write(track.PositionOffset);
						writer.Write(track.RotationOffset);
						writer.Write(track.ScaleOffset);
						writer.Write((byte)track.AnimatedChannels);
					}
					WriteChannels(writer, source, tracks, MotionPoseChannels.Position);
					WriteChannels(writer, source, tracks, MotionPoseChannels.Rotation);
					WriteChannels(writer, source, tracks, MotionPoseChannels.Scale);
					foreach (float feature in source.Features) {
						writer.Write(feature);
					}
					foreach (float scale in source.FeatureScales) {
						writer.Write(scale);
					}
					writer.Write(source.Groups.Length);
					foreach (MotionGroup group in source.Groups) {
						byte[] name = Encoding.UTF8.GetBytes(group.Name.ToString());
						writer.Write((byte)name.Length);
						writer.Write(name);
						writer.Write(new byte[61 - name.Length]);
						writer.Write(group.Tags);
						writer.Write(group.ActionId);
					}
					writer.Write(source.LeftFootBone);
					writer.Write(source.RightFootBone);
					return stream.ToArray();
				}
			}
		}

		// Dense source arrays are only for offline baking/editing. Runtime callers use Load.
		public static MotionDatabaseSource Read(byte[] bytes) {
			using (var stream = new MemoryStream(bytes, false)) {
				using (var database = Load(stream)) {
					return ExpandSource(ref database.Value);
				}
			}
		}

		public static BlobAssetReference<MotionDatabase> Load(byte[] bytes) {
			using (var stream = new MemoryStream(bytes, false)) {
				return Load(stream);
			}
		}

		// TextAsset.GetData<byte>() exposes the asset's existing memory without a managed byte[] copy.
		public static unsafe BlobAssetReference<MotionDatabase> Load(NativeArray<byte> bytes) {
			if (bytes.Length < 36) {
				throw new InvalidDataException("Truncated MMD database.");
			}
			using (var stream = new UnmanagedMemoryStream((byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes), bytes.Length)) {
				return Load(stream);
			}
		}

		// The caller retains ownership of a seekable input stream, including on failure.
		public static BlobAssetReference<MotionDatabase> Load(Stream stream) {
			if (!stream.CanRead || !stream.CanSeek || stream.Position != 0) {
				throw new ArgumentException("A readable, seekable database stream positioned at its start is required.");
			}
			using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) {
				ValidateVersion(reader);
				int features = Count(reader);
				int bones = Count(reader);
				int clips = Count(reader);
				int frames = Count(reader);
				int positions = Count(reader);
				int rotations = Count(reader);
				int scales = Count(reader);
				long poses = (long)frames * bones;
				long tracks = (long)clips * bones;
				long featureSamples = (long)frames * features;
				long expected = 36L + bones * 8L + clips * 25L + frames * 36L + tracks * 13L +
					positions * 12L + rotations * 8L + scales * 12L + featureSamples * 4L + features * 4L;
				if (poses > MaximumElements || tracks > MaximumElements || featureSamples > MaximumElements ||
					positions < tracks || rotations < tracks || scales < tracks || positions > poses || rotations > poses || scales > poses ||
					expected + 12 > stream.Length) {
					throw new InvalidDataException("Invalid compact database size or truncated data.");
				}
				stream.Position = expected;
				int groups = reader.ReadInt32();
				if (groups < 0 || groups > 32 || stream.Length != expected + 12 + groups * GroupRecordBytes) {
					throw new InvalidDataException("Invalid animation group catalog size.");
				}
				stream.Position = 36;
				using (var builder = new BlobBuilder(Allocator.Temp)) {
					ref var database = ref builder.ConstructRoot<MotionDatabase>();
					database.FeatureCount = features;
					var parents = builder.Allocate(ref database.Parents, bones);
					var hashes = builder.Allocate(ref database.BoneNameHashes, bones);
					for (int bone = 0; bone < bones; bone++) {
						parents[bone] = reader.ReadInt32();
						hashes[bone] = reader.ReadInt32();
					}
					var clipData = builder.Allocate(ref database.Clips, clips);
					for (int index = 0; index < clips; index++) {
						clipData[index] = new MotionClip {
							FirstFrame = reader.ReadInt32(), FrameCount = reader.ReadInt32(), Duration = reader.ReadSingle(),
							SampleRate = reader.ReadSingle(), Tags = reader.ReadUInt32(), ActionId = reader.ReadInt32(), Loop = reader.ReadBoolean()
						};
					}
					var frameData = builder.Allocate(ref database.Frames, frames);
					for (int index = 0; index < frames; index++) {
						int clip = reader.ReadInt32();
						float time = reader.ReadSingle();
						float3 position = ReadVector(reader);
						frameData[index] = new MotionFrame { ClipIndex = clip, Time = time, Root = new RigidTransform(ReadRotation(reader), position) };
					}
					var trackData = builder.Allocate(ref database.PoseTracks, (int)tracks);
					for (int index = 0; index < tracks; index++) {
						trackData[index] = new MotionPoseTrack {
							PositionOffset = reader.ReadInt32(), RotationOffset = reader.ReadInt32(), ScaleOffset = reader.ReadInt32(),
							AnimatedChannels = (MotionPoseChannels)reader.ReadByte()
						};
					}
					var positionData = builder.Allocate(ref database.Positions, positions);
					for (int index = 0; index < positions; index++) {
						positionData[index] = ReadVector(reader);
					}
					var rotationData = builder.Allocate(ref database.Rotations, rotations);
					for (int index = 0; index < rotations; index++) {
						rotationData[index] = new MotionPackedRotation { Value = reader.ReadUInt64() };
					}
					var scaleData = builder.Allocate(ref database.Scales, scales);
					for (int index = 0; index < scales; index++) {
						scaleData[index] = ReadVector(reader);
					}
					var featureData = builder.Allocate(ref database.Features, (int)featureSamples);
					for (int index = 0; index < featureSamples; index++) {
						featureData[index] = reader.ReadSingle();
					}
					var featureScales = builder.Allocate(ref database.FeatureScales, features);
					for (int index = 0; index < features; index++) {
						featureScales[index] = reader.ReadSingle();
					}
					reader.ReadInt32();
					var groupData = builder.Allocate(ref database.Groups, groups);
					for (int index = 0; index < groups; index++) {
						groupData[index] = ReadGroup(reader);
					}
					database.LeftFootBone = reader.ReadInt32();
					database.RightFootBone = reader.ReadInt32();
					var result = builder.CreateBlobAssetReference<MotionDatabase>(Allocator.Persistent);
					try {
						MotionDatabaseBuilder.Validate(ref result.Value);
						return result;
					} catch (ArgumentException exception) {
						result.Dispose();
						throw new InvalidDataException("Invalid compact database contents.", exception);
					} catch {
						result.Dispose();
						throw;
					}
				}
			}
		}

		private static void ValidateVersion(BinaryReader reader) {
			if (reader.BaseStream.Length < 36 || reader.ReadUInt32() != Magic) {
				throw new InvalidDataException("Unsupported or truncated MMD database.");
			}
			int version = reader.ReadInt32();
			if (version != Version) {
				throw new InvalidDataException("Unsupported MMD database format.");
			}
		}

		private static MotionGroup ReadGroup(BinaryReader reader) {
			int length = reader.ReadByte();
			byte[] name = reader.ReadBytes(61);
			if (length == 0 || length > 61) {
				throw new InvalidDataException("Invalid animation group name length.");
			}
			try {
				return new MotionGroup {
					Name = new FixedString64Bytes(new UTF8Encoding(false, true).GetString(name, 0, length)),
					Tags = reader.ReadUInt32(), ActionId = reader.ReadInt32()
				};
			} catch (DecoderFallbackException exception) {
				throw new InvalidDataException("Invalid animation group name encoding.", exception);
			}
		}

		private static void WriteChannels(BinaryWriter writer, MotionDatabaseSource source, MotionPoseTrack[] tracks, MotionPoseChannels channel) {
			int bones = source.Parents.Length;
			for (int clipIndex = 0; clipIndex < source.Clips.Length; clipIndex++) {
				MotionClip clip = source.Clips[clipIndex];
				for (int bone = 0; bone < bones; bone++) {
					int count = (tracks[clipIndex * bones + bone].AnimatedChannels & channel) != 0 ? clip.FrameCount : 1;
					for (int sample = 0; sample < count; sample++) {
						JointPose pose = source.Poses[(clip.FirstFrame + sample) * bones + bone];
						if (channel == MotionPoseChannels.Rotation) {
							writer.Write(MmdRotationPacking.Pack(pose.Rotation).Value);
						} else {
							WriteVector(writer, channel == MotionPoseChannels.Position ? pose.Position : pose.Scale);
						}
					}
				}
			}
		}

		private static MotionDatabaseSource ExpandSource(ref MotionDatabase database) {
			var source = new MotionDatabaseSource {
				FeatureCount = database.FeatureCount, Groups = database.Groups.ToArray(), Clips = database.Clips.ToArray(),
				Frames = database.Frames.ToArray(), Parents = database.Parents.ToArray(), BoneNameHashes = database.BoneNameHashes.ToArray(),
				Features = database.Features.ToArray(), FeatureScales = database.FeatureScales.ToArray(),
				LeftFootBone = database.LeftFootBone, RightFootBone = database.RightFootBone,
				Poses = new JointPose[database.Frames.Length * database.Parents.Length]
			};
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int bone = 0; bone < source.Parents.Length; bone++) {
					source.Poses[frame * source.Parents.Length + bone] = MmdAnimationMath.PoseSample(ref database, frame, bone);
				}
			}
			return source;
		}

		private static int Count(BinaryReader reader) {
			int count = reader.ReadInt32();
			if (count <= 0 || count > MaximumElements) {
				throw new InvalidDataException("Invalid element count.");
			}
			return count;
		}

		private static void WriteVector(BinaryWriter writer, float3 vector) {
			writer.Write(vector.x);
			writer.Write(vector.y);
			writer.Write(vector.z);
		}

		private static float3 ReadVector(BinaryReader reader) {
			return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		}

		private static void WriteRotation(BinaryWriter writer, quaternion rotation) {
			writer.Write(rotation.value.x);
			writer.Write(rotation.value.y);
			writer.Write(rotation.value.z);
			writer.Write(rotation.value.w);
		}

		private static quaternion ReadRotation(BinaryReader reader) {
			return new quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
		}
	}
}
