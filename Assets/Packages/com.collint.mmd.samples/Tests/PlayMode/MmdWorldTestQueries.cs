using Unity.Entities;

namespace ColInt.MMD.Tests {
	public static class MmdWorldTestQueries {
		public static int CountLiveWorlds() {
			int count = 0;
			foreach (var world in World.All) {
				// Unity's scene loader also creates internal Streaming worlds without animation/render systems.
				if ((world.Flags & WorldFlags.Live) != 0) {
					count++;
				}
			}
			return count;
		}
	}
}
