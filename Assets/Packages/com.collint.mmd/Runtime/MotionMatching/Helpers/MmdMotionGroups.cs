using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Helpers {
	public static class MmdMotionGroups {
		public static bool Matches(ref MotionDatabase database, in MotionClip clip, in DynamicBuffer<MmdMotionGroup> groups) {
			for (int index = 0; index < groups.Length; index++) {
				var filter = groups[index];
				ulong word = MotionDatabaseGroups.Word(ref database, clip, index);
				if ((word & filter.Required) != filter.Required || (word & filter.Excluded) != 0) {
					return false;
				}
			}
			return true;
		}

		public static bool Any(ref MotionDatabase database, in MotionClip clip, in DynamicBuffer<MmdMotionGroup> groups, MmdMotionGroupUsage usage) {
			for (int index = 0; index < groups.Length; index++) {
				if ((MotionDatabaseGroups.Word(ref database, clip, index) & Word(groups[index], usage)) != 0) {
					return true;
				}
			}
			return false;
		}

		public static bool Contains(in DynamicBuffer<MmdMotionGroup> groups, int id, MmdMotionGroupUsage usage) {
			int index = BitMask.WordIndex(id - 1);
			return id > 0 && index < groups.Length && (Word(groups[index], usage) & BitMask.Bit(id - 1)) != 0;
		}

		public static MmdMotionGroupUsage Usage(in DynamicBuffer<MmdMotionGroup> groups, int id) {
			var usage = MmdMotionGroupUsage.None;
			if (Contains(groups, id, MmdMotionGroupUsage.Required)) {
				usage |= MmdMotionGroupUsage.Required;
			}
			if (Contains(groups, id, MmdMotionGroupUsage.Excluded)) {
				usage |= MmdMotionGroupUsage.Excluded;
			}
			if (Contains(groups, id, MmdMotionGroupUsage.StationaryPose)) {
				usage |= MmdMotionGroupUsage.StationaryPose;
			}
			return usage;
		}

		public static int First(in DynamicBuffer<MmdMotionGroup> groups, MmdMotionGroupUsage usage, int ignored = 0) {
			for (int index = 0; index < groups.Length; index++) {
				ulong word = Word(groups[index], usage);
				if (ignored > 0 && BitMask.WordIndex(ignored - 1) == index) {
					word &= ~BitMask.Bit(ignored - 1);
				}
				if (word != 0) {
					return index * 64 + math.tzcnt(word) + 1;
				}
			}
			return 0;
		}

		public static bool Is(in DynamicBuffer<MmdMotionGroup> groups, MmdMotionGroupUsage usage, int first, int second = 0) {
			for (int index = 0; index < groups.Length; index++) {
				ulong expected = 0;
				if (first > 0 && BitMask.WordIndex(first - 1) == index) {
					expected |= BitMask.Bit(first - 1);
				}
				if (second > 0 && BitMask.WordIndex(second - 1) == index) {
					expected |= BitMask.Bit(second - 1);
				}
				if (Word(groups[index], usage) != expected) {
					return false;
				}
			}
			return (first <= 0 || Contains(groups, first, usage)) && (second <= 0 || Contains(groups, second, usage));
		}

		public static void Set(DynamicBuffer<MmdMotionGroup> groups, MmdMotionGroupUsage usage, int first, int second = 0) {
			for (int index = 0; index < groups.Length; index++) {
				var word = groups[index];
				Clear(ref word, ulong.MaxValue, usage);
				groups[index] = word;
			}
			Add(groups, first, usage);
			Add(groups, second, usage);
		}

		public static void Set(DynamicBuffer<MmdMotionGroup> groups, MmdMotionGroupUsage usage, in BitMask mask) {
			Set(groups, usage, 0);
			Add(groups, mask, usage);
		}

		public static void Add(DynamicBuffer<MmdMotionGroup> groups, in BitMask mask, MmdMotionGroupUsage usage) {
			if (groups.Length < mask.WordCount) {
				groups.Resize(mask.WordCount, Unity.Collections.NativeArrayOptions.ClearMemory);
			}
			for (int index = 0; index < mask.WordCount; index++) {
				var word = groups[index];
				AddBits(ref word, mask.GetWord(index), usage);
				groups[index] = word;
			}
		}

		public static void Remove(DynamicBuffer<MmdMotionGroup> groups, in BitMask mask, MmdMotionGroupUsage usage) {
			for (int index = 0; index < math.min(groups.Length, mask.WordCount); index++) {
				var word = groups[index];
				Clear(ref word, mask.GetWord(index), usage);
				groups[index] = word;
			}
		}

		public static void Remove(DynamicBuffer<MmdMotionGroup> groups, int id, MmdMotionGroupUsage usage) {
			int index = BitMask.WordIndex(id - 1);
			if (id <= 0 || index >= groups.Length) {
				return;
			}
			var word = groups[index];
			Clear(ref word, BitMask.Bit(id - 1), usage);
			groups[index] = word;
		}

		public static void Add(DynamicBuffer<MmdMotionGroup> groups, int id, MmdMotionGroupUsage usage) {
			if (id <= 0 || usage == MmdMotionGroupUsage.None) {
				return;
			}
			int index = BitMask.WordIndex(id - 1);
			if (groups.Length <= index) {
				groups.Resize(index + 1, Unity.Collections.NativeArrayOptions.ClearMemory);
			}
			var word = groups[index];
			AddBits(ref word, BitMask.Bit(id - 1), usage);
			groups[index] = word;
		}

		private static void AddBits(ref MmdMotionGroup word, ulong bits, MmdMotionGroupUsage usage) {
			if ((usage & MmdMotionGroupUsage.Required) != 0) {
				word.Required |= bits;
			}
			if ((usage & MmdMotionGroupUsage.Excluded) != 0) {
				word.Excluded |= bits;
			}
			if ((usage & MmdMotionGroupUsage.StationaryPose) != 0) {
				word.StationaryPose |= bits;
			}
		}

		private static ulong Word(in MmdMotionGroup word, MmdMotionGroupUsage usage) {
			ulong result = 0;
			if ((usage & MmdMotionGroupUsage.Required) != 0) {
				result |= word.Required;
			}
			if ((usage & MmdMotionGroupUsage.Excluded) != 0) {
				result |= word.Excluded;
			}
			if ((usage & MmdMotionGroupUsage.StationaryPose) != 0) {
				result |= word.StationaryPose;
			}
			return result;
		}

		private static void Clear(ref MmdMotionGroup word, ulong bits, MmdMotionGroupUsage usage) {
			if ((usage & MmdMotionGroupUsage.Required) != 0) {
				word.Required &= ~bits;
			}
			if ((usage & MmdMotionGroupUsage.Excluded) != 0) {
				word.Excluded &= ~bits;
			}
			if ((usage & MmdMotionGroupUsage.StationaryPose) != 0) {
				word.StationaryPose &= ~bits;
			}
		}
	}
}
