using System;
using ColInt.MMD.MotionMatching.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace ColInt.MMD.Tests {
	public class BitMaskTests {
		[Test]
		public void BitwiseOperationsCrossWordBoundariesAndDoNotChangeStorage() {
			using (var leftWords = new NativeArray<ulong>(16, Allocator.Temp)) {
				using (var rightWords = new NativeArray<ulong>(16, Allocator.Temp)) {
					var left = new BitMask(leftWords, 1000);
					var right = new BitMask(rightWords, 1000);
					int[] bits = { 0, 62, 63, 64, 126, 127, 128, 999 };
					foreach (int bit in bits) {
						left.Set(bit);
					}
					right.Set(63);
					right.Set(64);
					right.Set(500);
					Assert.That(left.Intersects(right), Is.True);
					Assert.That(left.ContainsAll(right), Is.False);
					left.Or(right);
					Assert.That(left.ContainsAll(right), Is.True);
					Assert.That(leftWords[0], Is.EqualTo(1UL | (1UL << 62) | (1UL << 63)));
					left.Xor(right);
					Assert.That(left.Intersects(right), Is.False);
					foreach (int bit in bits) {
						Assert.That(left.Contains(bit), Is.EqualTo(bit != 63 && bit != 64));
					}
					left.Or(right);
					left.And(right);
					Assert.That(left.Contains(999), Is.False);
					Assert.That(left.ContainsAll(right), Is.True);
					left.AndNot(right);
					Assert.That(leftWords.ToArray(), Is.EqualTo(new ulong[16]));
					left.Not();
					Assert.That(leftWords[15], Is.EqualTo((1UL << 40) - 1));
					Assert.That(left.Contains(999), Is.True);
					Assert.That(left.Contains(1000), Is.False);
					left.Set(999, false);
					Assert.That(left.Contains(999), Is.False);
				}
			}
		}

		[Test]
		public void SizeMismatchAndInvalidBitsFailBeforeChangingMask() {
			using (var words = new NativeArray<ulong>(1, Allocator.Temp)) {
				using (var otherWords = new NativeArray<ulong>(2, Allocator.Temp)) {
					var mask = new BitMask(words, 64);
					var other = new BitMask(otherWords, 65);
					mask.Set(63);
					Assert.Throws<ArgumentException>(() => mask.Or(other));
					Assert.Throws<ArgumentOutOfRangeException>(() => mask.Set(64));
					Assert.Throws<ArgumentOutOfRangeException>(() => mask.Set(-1));
					Assert.That(words[0], Is.EqualTo(1UL << 63));
				}
			}
			using (var emptyWords = new NativeArray<ulong>(0, Allocator.Temp)) {
				var empty = new BitMask(emptyWords, 0);
				empty.Not();
				Assert.That(empty.Contains(0), Is.False);
			}
		}

		[Test]
		public void MasksRunInBurstWithCallerOwnedStorage() {
			using (var left = new NativeArray<ulong>(16, Allocator.TempJob)) {
				using (var right = new NativeArray<ulong>(16, Allocator.TempJob)) {
					new BitMask(right, 1000).Set(999);
					new BitMaskTestJob { Left = left, Right = right }.Schedule().Complete();
					Assert.That(new BitMask(left, 1000).Contains(999), Is.True);
					Assert.That(left[0], Is.EqualTo(1UL << 63));
				}
			}
		}
	}
}
