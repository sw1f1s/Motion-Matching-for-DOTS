using System;
using Unity.Collections;

namespace ColInt.MMD.MotionMatching.Data {
	// A non-owning view over caller-owned words. Copies share storage; the owner disposes it.
	public struct BitMask {
		private NativeArray<ulong> _words;
		public int BitCount { get; }
		public int WordCount => _words.Length;

		public BitMask(NativeArray<ulong> words, int bitCount) {
			if (bitCount < 0 || words.Length != WordsForBits(bitCount)) {
				throw new ArgumentException("Mask storage must match its bit count.");
			}
			_words = words;
			BitCount = bitCount;
		}

		public static int WordsForBits(int bitCount) {
			return (int)(((long)bitCount + 63) / 64);
		}

		public static int WordIndex(int bit) {
			return bit >> 6;
		}

		public static ulong Bit(int bit) {
			return 1UL << (bit & 63);
		}

		public ulong GetWord(int index) {
			if (index < 0 || index >= WordCount) {
				return 0;
			}
			ulong word = _words[index];
			if (index == WordCount - 1 && (BitCount & 63) != 0) {
				word &= (1UL << (BitCount & 63)) - 1;
			}
			return word;
		}

		public bool Contains(int bit) {
			return bit >= 0 && bit < BitCount && (_words[WordIndex(bit)] & Bit(bit)) != 0;
		}

		public void Set(int bit, bool enabled = true) {
			if (bit < 0 || bit >= BitCount) {
				throw new ArgumentOutOfRangeException(nameof(bit));
			}
			int index = WordIndex(bit);
			_words[index] = enabled ? _words[index] | Bit(bit) : _words[index] & ~Bit(bit);
		}

		// In-place operations allocate nothing and preserve the caller's storage.
		public void And(in BitMask other) {
			RequireSameSize(other);
			for (int index = 0; index < WordCount; index++) {
				_words[index] &= other._words[index];
			}
		}

		public void Or(in BitMask other) {
			RequireSameSize(other);
			for (int index = 0; index < WordCount; index++) {
				_words[index] |= other._words[index];
			}
		}

		public void Xor(in BitMask other) {
			RequireSameSize(other);
			for (int index = 0; index < WordCount; index++) {
				_words[index] ^= other._words[index];
			}
		}

		public void AndNot(in BitMask other) {
			RequireSameSize(other);
			for (int index = 0; index < WordCount; index++) {
				_words[index] &= ~other._words[index];
			}
		}

		public void Not() {
			for (int index = 0; index < WordCount; index++) {
				_words[index] = ~_words[index];
			}
			if (WordCount != 0 && (BitCount & 63) != 0) {
				_words[WordCount - 1] &= (1UL << (BitCount & 63)) - 1;
			}
		}

		public bool Intersects(in BitMask other) {
			RequireSameSize(other);
			for (int index = 0; index < WordCount; index++) {
				if ((_words[index] & other._words[index]) != 0) {
					return true;
				}
			}
			return false;
		}

		public bool ContainsAll(in BitMask other) {
			RequireSameSize(other);
			for (int index = 0; index < WordCount; index++) {
				if ((_words[index] & other._words[index]) != other._words[index]) {
					return false;
				}
			}
			return true;
		}

		private void RequireSameSize(in BitMask other) {
			if (BitCount != other.BitCount) {
				throw new ArgumentException("Bitwise masks must have the same bit count.");
			}
		}
	}
}
