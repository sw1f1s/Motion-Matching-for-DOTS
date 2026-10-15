using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using ColInt.MMD.MotionMatching.Data;
using Unity.Mathematics;
using ColInt.MMD.Database.Data;
using Unity.Collections;

namespace ColInt.MMD.Database.Helpers {
	public static class MotionDatabaseGroups {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains(ref MotionDatabase database, in MotionClip clip, int group) {
			return group > 0 && (Word(ref database, clip, BitMask.WordIndex(group - 1)) & BitMask.Bit(group - 1)) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Word(ref MotionDatabase database, in MotionClip clip, int index) {
			return index >= 0 && index < clip.GroupWordCount ? database.ClipGroupWords[clip.FirstGroupWord + index] : 0;
		}

		// Managed export for bake tools and diagnostics only; matching reads words directly.
		public static int[] GetIds(ref MotionDatabase database, in MotionClip clip) {
			var ids = new List<int>();
			for (int index = 0; index < clip.GroupWordCount; index++) {
				ulong word = Word(ref database, clip, index);
				while (word != 0) {
					ids.Add(index * 64 + math.tzcnt(word) + 1);
					word &= word - 1;
				}
			}
			return ids.ToArray();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetId(ref MotionDatabase database, FixedString64Bytes name) {
			return GetGroup(ref database, name).Id;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetActionId(ref MotionDatabase database, FixedString64Bytes name) {
			MotionGroup group = GetGroup(ref database, name);
			if (group.Mode != MotionPlaybackMode.Action) {
				throw new ArgumentException("The named group is not an action.");
			}
			return group.ActionId;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MotionGroup GetGroup(ref MotionDatabase database, FixedString64Bytes name) {
			for (int index = 0; index < database.Groups.Length; index++) {
				if (database.Groups[index].Name == name) {
					return database.Groups[index];
				}
			}
			throw new ArgumentException("Unknown animation group.");
		}
	}
}
