using System.Runtime.CompilerServices;

namespace ColInt.MMD.Database.Helpers {
	public static class BonePathHash {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Calculate(string path) {
			uint hash = 2166136261;
			foreach (char character in path) {
				hash = (hash ^ character) * 16777619;
			}
			return unchecked((int)hash);
		}
	}

}
