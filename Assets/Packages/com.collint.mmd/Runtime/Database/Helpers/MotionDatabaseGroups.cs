using System.Runtime.CompilerServices;
using System;
using ColInt.MMD.Database.Data;
using Unity.Collections;

namespace ColInt.MMD.Database.Helpers {
	public static class MotionDatabaseGroups {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetTags(ref MotionDatabase database, FixedString64Bytes name) {
			return GetGroup(ref database, name).Tags;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetActionId(ref MotionDatabase database, FixedString64Bytes name) {
			MotionGroup group = GetGroup(ref database, name);
			if (group.ActionId == 0) {
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
