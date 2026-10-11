using System.IO;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Helpers;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace ColInt.MMD.Tests {
	public class MmdRotationPackingTests {
		[Test]
		public void PackedRotationsUseEightBytesAndPreserveIdentityExactly() {
			Assert.That(UnsafeUtility.SizeOf<MotionPackedRotation>(), Is.EqualTo(8));
			Assert.That(MmdRotationPacking.Unpack(MmdRotationPacking.Pack(quaternion.identity)), Is.EqualTo(quaternion.identity));
		}

		[Test]
		public void AxisExtremesTiesAndBothQuaternionSignsRemainValid() {
			foreach (float4 value in new[] {
				new float4(1f, 0f, 0f, 0f), new float4(0f, 1f, 0f, 0f), new float4(0f, 0f, 1f, 0f), new float4(0f, 0f, 0f, 1f),
				new float4(0.5f), new float4(0.5f, -0.5f, 0.5f, -0.5f), new float4(1f, 1f, 0f, 0f), new float4(1f, 1.000001f, 0f, 0f)
			}) {
				AssertRoundTrip(new quaternion(math.normalize(value)));
				AssertRoundTrip(new quaternion(-math.normalize(value)));
			}
		}

		[Test]
		public void RandomRotationsStayWithinAngularBudgetIncludingMidpointInterpolation() {
			var random = new Unity.Mathematics.Random(0x12345678);
			for (int index = 0; index < 10000; index++) {
				quaternion a = random.NextQuaternionRotation();
				quaternion b = random.NextQuaternionRotation();
				AssertRoundTrip(a);
				Assert.That(MmdRotationPacking.ErrorDegrees(math.slerp(a, b, 0.5f),
					math.slerp(MmdRotationPacking.Unpack(MmdRotationPacking.Pack(a)), MmdRotationPacking.Unpack(MmdRotationPacking.Pack(b)), 0.5f)),
					Is.LessThan(MmdRotationPacking.DefaultMaximumErrorDegrees));
			}
		}

		[Test]
		public void BoneRotationStorageShrinksWhileRootMotionRemainsExact() {
			var source = MotionDatabaseFixture.Create();
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				JointPose pose = source.Poses[frame * 2];
				pose.Rotation = quaternion.EulerXYZ(frame * 0.07f, frame * 0.11f, frame * 0.13f);
				source.Poses[frame * 2] = pose;
				MotionFrame root = source.Frames[frame];
				root.Root.rot = quaternion.RotateY(frame * 0.023f);
				source.Frames[frame] = root;
			}
			byte[] bytes = MotionDatabaseCodec.Write(source);
			using (var database = MotionDatabaseCodec.Load(bytes)) {
				ref MotionDatabase data = ref database.Value;
				long expectedBytes = 36L + data.Parents.Length * 8L + data.Clips.Length * 25L + data.Frames.Length * 36L +
					data.PoseTracks.Length * 13L + data.Positions.Length * 12L + data.Rotations.Length * 8L + data.Scales.Length * 12L +
					data.Features.Length * 4L + data.FeatureScales.Length * 4L + 12L;
				Assert.That(bytes.LongLength, Is.EqualTo(expectedBytes));
				Assert.That(data.Frames.ToArray(), Is.EqualTo(source.Frames));
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					Assert.That(MmdRotationPacking.ErrorDegrees(source.Poses[frame * 2].Rotation, MmdAnimationMath.PoseSample(ref data, frame, 0).Rotation),
						Is.LessThan(MmdRotationPacking.DefaultMaximumErrorDegrees));
				}
			}
		}

		[Test]
		public void TighterBudgetAndInvalidBudgetsRejectPackingBeforeWriting() {
			var source = MotionDatabaseFixture.Create();
			JointPose pose = source.Poses[0];
			pose.Rotation = quaternion.EulerXYZ(0.123f, 0.456f, 0.789f);
			float error = MmdRotationPacking.ErrorDegrees(pose.Rotation, MmdRotationPacking.Unpack(MmdRotationPacking.Pack(pose.Rotation)));
			Assert.That(error, Is.GreaterThan(0f));
			source.Poses[0] = pose;
			source.MaximumRotationErrorDegrees = error * 0.5f;
			Assert.Throws<ArgumentException>(() => MotionDatabaseCodec.Write(source));
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
			source.MaximumRotationErrorDegrees = error * 2f;
			Assert.DoesNotThrow(() => MotionDatabaseCodec.Write(source));
			foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 1f }) {
				source.MaximumRotationErrorDegrees = invalid;
				Assert.Throws<ArgumentException>(() => MotionDatabaseCodec.Write(source));
			}
		}

		[Test]
		public void CorruptPackedRotationPayloadsAreRejectedByDatabaseLoader() {
			byte[] bytes = MotionDatabaseCodec.Write(MotionDatabaseFixture.Create());
			int rotationOffset = 36 + 2 * 8 + 3 * 25 + 33 * 36 + 6 * 13 + BitConverter.ToInt32(bytes, 24) * 12;
			foreach (ulong invalid in new[] { 1UL << 63, 0x8000UL, 0x7fff7fff7fffUL | (3UL << 48) }) {
				Assert.That(MmdRotationPacking.IsValid(new MotionPackedRotation { Value = invalid }), Is.False);
				byte[] damaged = (byte[])bytes.Clone();
				Array.Copy(BitConverter.GetBytes(invalid), 0, damaged, rotationOffset, 8);
				Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(damaged));
			}
		}

		private static void AssertRoundTrip(quaternion original) {
			MotionPackedRotation packed = MmdRotationPacking.Pack(original);
			quaternion restored = MmdRotationPacking.Unpack(packed);
			Assert.That(MmdRotationPacking.IsValid(packed), Is.True);
			Assert.That(math.lengthsq(restored.value), Is.EqualTo(1f).Within(0.000001f));
			Assert.That(math.dot(original.value, restored.value), Is.GreaterThan(0f));
			Assert.That(MmdRotationPacking.ErrorDegrees(original, restored), Is.LessThan(0.005f));
		}
	}
}
