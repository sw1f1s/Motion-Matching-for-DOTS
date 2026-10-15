using System.IO;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Helpers;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace ColInt.MMD.Tests {
	public class MotionDatabasePackingTests {
		[Test]
		public void ConstantChannelsAreStoredOncePerClipAndAnimatedChannelsKeepEverySample() {
			var source = MotionDatabaseFixture.Create();
			for (int frame = 0; frame < 11; frame++) {
				JointPose pose = source.Poses[frame * 2];
				pose.Rotation = quaternion.RotateY(frame * 0.03f);
				pose.Scale = new float3(1f + frame * 0.01f);
				source.Poses[frame * 2] = pose;
			}
			using (var database = MotionDatabaseBuilder.Build(source)) {
				Assert.That(database.Value.PoseTracks.Length, Is.EqualTo(6));
				Assert.That(database.Value.Positions.Length, Is.EqualTo(36));
				Assert.That(database.Value.Rotations.Length, Is.EqualTo(16));
				Assert.That(database.Value.Scales.Length, Is.EqualTo(16));
				Assert.That(database.Value.PoseTracks[0].AnimatedChannels, Is.EqualTo(MotionPoseChannels.Rotation | MotionPoseChannels.Scale));
				AssertSamples(source, ref database.Value);
			}
			using (var database = MotionDatabaseCodec.Load(MotionDatabaseCodec.Write(source))) {
				AssertSamples(source, ref database.Value);
				Assert.That(MmdAnimationMath.PoseAt(ref database.Value, 0, 0.35f, 1).Position.y, Is.EqualTo(1.35f).Within(0.000001f));
				Assert.That(MmdAnimationMath.PoseAt(ref database.Value, 0, 1f, 1).Position.y, Is.EqualTo(1f));
				Assert.That(MmdAnimationMath.PoseAt(ref database.Value, 2, 1f, 1).Position.y, Is.EqualTo(2f));
			}
		}

		[Test]
		public void TinyChangesAndSignedZeroArePreservedWithoutToleranceOrQuaternionCanonicalization() {
			var source = MotionDatabaseFixture.Create();
			JointPose changed = source.Poses[2];
			changed.Position.x = math.asfloat(1);
			changed.Position.z = math.asfloat(unchecked((int)0x80000000));
			changed.Rotation.value = -changed.Rotation.value;
			source.Poses[2] = changed;
			using (var database = MotionDatabaseCodec.Load(MotionDatabaseCodec.Write(source))) {
				Assert.That(database.Value.PoseTracks[0].AnimatedChannels, Is.EqualTo(MotionPoseChannels.Position | MotionPoseChannels.Rotation));
				AssertSamples(source, ref database.Value);
			}
		}

		[Test]
		public void MalformedCompactCountsTracksAndChannelValuesAreRejected() {
			byte[] valid = MotionDatabaseCodec.Write(MotionDatabaseFixture.Create());
			int trackOffset = 40 + 2 * 8 + 3 * 30 + 3 * 4 + 33 * 36;
			byte[] damaged = (byte[])valid.Clone();
			Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, damaged, 24, 4);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
			damaged = (byte[])valid.Clone();
			damaged[trackOffset + 12] = 128;
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
			damaged = (byte[])valid.Clone();
			Array.Copy(BitConverter.GetBytes(1), 0, damaged, trackOffset, 4);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
			damaged = (byte[])valid.Clone();
			Array.Copy(BitConverter.GetBytes(float.NaN), 0, damaged, trackOffset + 6 * 13, 4);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
			damaged = (byte[])valid.Clone();
			Array.Copy(BitConverter.GetBytes(float.NaN), 0, damaged, 40 + 2 * 8 + 3 * 30 + 3 * 4 + 4, 4);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
			damaged = (byte[])valid.Clone();
			Array.Resize(ref damaged, 32);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));

			damaged = (byte[])valid.Clone();
			Array.Resize(ref damaged, damaged.Length - 1);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
		}

		[Test]
		public void StreamAndNativeReadersRetainBorrowedInputOwnership() {
			byte[] bytes = MotionDatabaseCodec.Write(MotionDatabaseFixture.Create());
			using (var stream = new MemoryStream(bytes, false)) {
				using (var database = MotionDatabaseCodec.Load(stream)) {
					Assert.That(database.Value.Clips.Length, Is.EqualTo(3));
				}
				Assert.That(stream.CanRead, Is.True);
				Assert.That(stream.Position, Is.EqualTo(stream.Length));
			}
			using (var native = new NativeArray<byte>(bytes, Allocator.Temp)) {
				using (var database = MotionDatabaseCodec.Load(native)) {
					Assert.That(database.Value.Frames.Length, Is.EqualTo(33));
				}
				Assert.That(native[0], Is.EqualTo(bytes[0]));
			}
		}

		[Test]
		public void NativeLoadingDoesNotAllocateDenseManagedPoseArrays() {
			// Unity's Mono can report zero from GetAllocatedBytesForCurrentThread even for large allocations.
			long probeBefore = Profiler.GetMonoUsedSizeLong();
			byte[] probe = new byte[1024 * 1024];
			long probeDelta = Profiler.GetMonoUsedSizeLong() - probeBefore;
			GC.KeepAlive(probe);
			Assert.That(probeDelta, Is.GreaterThan(512 * 1024), "The managed-memory counter must observe a known allocation.");
			const int bones = 2048;
			var source = MotionDatabaseFixture.Create();
			source.Parents = new int[bones];
			source.BoneNameHashes = new int[bones];
			source.Parents[0] = -1;
			source.Poses = new JointPose[source.Frames.Length * bones];
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int bone = 0; bone < bones; bone++) {
					source.Poses[frame * bones + bone] = new JointPose {
						Position = new float3(0f, bone * 0.001f, 0f), Rotation = quaternion.identity, Scale = new float3(1f)
					};
				}
			}
			using (var warmup = MotionDatabaseCodec.Load(MotionDatabaseCodec.Write(MotionDatabaseFixture.Create()))) {
				Assert.That(warmup.IsCreated, Is.True);
			}
			using (var native = new NativeArray<byte>(MotionDatabaseCodec.Write(source), Allocator.Temp)) {
				GC.Collect();
				long before = Profiler.GetMonoUsedSizeLong();
				int collections = GC.CollectionCount(0);
				var database = MotionDatabaseCodec.Load(native);
				long allocated = Profiler.GetMonoUsedSizeLong() - before;
				GC.KeepAlive(source);
				try {
					Assert.That(GC.CollectionCount(0), Is.EqualTo(collections), "Collection would invalidate the managed-memory delta.");
					Assert.That(allocated, Is.LessThan(128 * 1024), "The runtime reader must not expand the multi-megabyte pose array or copy the input.");
					Assert.That(database.Value.Rotations.Length, Is.EqualTo(3 * bones));
				} finally {
					database.Dispose();
				}
			}
		}

		private static void AssertSamples(MotionDatabaseSource source, ref MotionDatabase database) {
			Assert.That(database.Features.ToArray(), Is.EqualTo(source.Features));
			Assert.That(database.FeatureScales.ToArray(), Is.EqualTo(source.FeatureScales));
			Assert.That(database.Frames.ToArray(), Is.EqualTo(source.Frames));
			Assert.That(database.Clips.Length, Is.EqualTo(source.Clips.Length));
			for (int index = 0; index < source.Clips.Length; index++) {
				var stored = database.Clips[index];
				var original = source.Clips[index];
				Assert.That((stored.FirstFrame, stored.FrameCount, stored.Duration, stored.SampleRate, stored.ActionId, stored.Loop),
					Is.EqualTo((original.FirstFrame, original.FrameCount, original.Duration, original.SampleRate, original.ActionId, original.Loop)));
				var groups = MotionDatabaseGroups.GetIds(ref database, stored);
				Assert.That(groups, Is.EqualTo(original.Groups));
			}
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int bone = 0; bone < source.Parents.Length; bone++) {
					JointPose expected = source.Poses[frame * source.Parents.Length + bone];
					JointPose actual = MmdAnimationMath.PoseSample(ref database, frame, bone);
					Assert.That(math.asint(actual.Position), Is.EqualTo(math.asint(expected.Position)));
					Assert.That(MmdRotationPacking.ErrorDegrees(expected.Rotation, actual.Rotation), Is.LessThanOrEqualTo(source.MaximumRotationErrorDegrees));
					Assert.That(math.dot(expected.Rotation.value, actual.Rotation.value), Is.GreaterThan(0f));
					Assert.That(math.asint(actual.Scale), Is.EqualTo(math.asint(expected.Scale)));
				}
			}
		}
	}
}
