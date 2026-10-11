using System.IO;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using NUnit.Framework;

namespace ColInt.MMD.Tests {
	public class MotionDatabaseTests {
		[Test]
		public void BinaryRoundTripPreservesAnimationSamples() {
			var original = MotionDatabaseFixture.Create();
			byte[] bytes = MotionDatabaseCodec.Write(original);
			var restored = MotionDatabaseCodec.Read(bytes);
			Assert.That(restored.Features, Is.EqualTo(original.Features));
			Assert.That(restored.Parents, Is.EqualTo(original.Parents));
			Assert.That(restored.Frames[32].Root.pos, Is.EqualTo(original.Frames[32].Root.pos));
			Assert.That(restored.Poses[5].Position, Is.EqualTo(original.Poses[5].Position));
		}

		[Test]
		public void TruncatedAndUnsupportedDatabaseIsRejectedBeforeAllocation() {
			byte[] bytes = MotionDatabaseCodec.Write(MotionDatabaseFixture.Create());
			Array.Resize(ref bytes, bytes.Length - 1);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Read(bytes));
			bytes[4] = 255;
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Read(bytes));
		}

		[Test]
		public void NamedGroupsRoundTripIncludingUnicodeAndActionLookup() {
			var source = MotionDatabaseFixture.Create();
			source.Groups = new[] {
				new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Бег"), Tags = 2 },
				new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Vault"), Tags = 4, ActionId = 7 }
			};
			var restored = MotionDatabaseCodec.Read(MotionDatabaseCodec.Write(source));
			Assert.That(restored.Groups, Is.EqualTo(source.Groups));
			var database = MotionDatabaseBuilder.Build(restored);
			try {
				Assert.That(MotionDatabaseGroups.GetActionId(ref database.Value, new Unity.Collections.FixedString64Bytes("Vault")), Is.EqualTo(7));
			} finally {
				database.Dispose();
			}
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		public void NonCurrentDatabaseVersionsAreRejected(int version) {
			byte[] bytes = MotionDatabaseCodec.Write(MotionDatabaseFixture.Create());
			Array.Copy(BitConverter.GetBytes(version), 0, bytes, 4, 4);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Read(bytes));
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(bytes));
			using (var stream = new MemoryStream(bytes, false)) {
				Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(stream));
				Assert.That(stream.CanRead, Is.True);
			}
			using (var nativeBytes = new Unity.Collections.NativeArray<byte>(bytes, Unity.Collections.Allocator.Temp)) {
				Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Load(nativeBytes));
				Assert.That(nativeBytes.IsCreated, Is.True);
			}
		}

		[Test]
		public void MalformedGroupCatalogAndDuplicateMetadataAreRejected() {
			var source = MotionDatabaseFixture.Create();
			source.Groups = new[] { new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Run"), Tags = 2 } };
			byte[] bytes = MotionDatabaseCodec.Write(source);
			bytes[bytes.Length - 78] = 62;
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Read(bytes));
			bytes = MotionDatabaseCodec.Write(source);
			Array.Copy(BitConverter.GetBytes(33), 0, bytes, bytes.Length - 82, 4);
			Assert.Throws<InvalidDataException>(() => MotionDatabaseCodec.Read(bytes));
			source.Groups = new[] { source.Groups[0], source.Groups[0] };
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
		}

		[Test]
		public void FootBindingsRoundTrip() {
			var source = MotionDatabaseFixture.Create();
			source.LeftFootBone = 0;
			source.RightFootBone = 1;
			byte[] bytes = MotionDatabaseCodec.Write(source);
			var restored = MotionDatabaseCodec.Read(bytes);
			Assert.That(restored.LeftFootBone, Is.Zero);
			Assert.That(restored.RightFootBone, Is.EqualTo(1));
		}

		[Test]
		public void InvalidFootBindingsAreRejected() {
			var source = MotionDatabaseFixture.Create();
			source.LeftFootBone = 1;
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
			source.RightFootBone = 1;
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
			source.RightFootBone = 2;
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
		}

		[Test]
		public void InvalidBoneOrderAndNonFiniteFeaturesAreRejected() {
			var source = MotionDatabaseFixture.Create();
			source.Parents[1] = 1;
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
			source.Parents[1] = 0;
			source.Features[0] = float.NaN;
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
		}
	}

}
