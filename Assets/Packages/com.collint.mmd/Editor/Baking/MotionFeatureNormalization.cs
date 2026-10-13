using System;
using ColInt.MMD.Database.Data;
using Unity.Mathematics;

namespace ColInt.MMD.Editor.Baking {
	public static class MotionFeatureNormalization {
		public static int[] ResolveGroups(MotionGroup[] groups, string[] names) {
			var ids = new System.Collections.Generic.HashSet<int>();
			foreach (string name in names) {
				bool found = false;
				foreach (var group in groups) {
					if (group.Name.ToString() == name) {
						ids.Add(group.Id);
						found = true;
						break;
					}
				}
				if (!found) {
					throw new ArgumentException($"Normalization group '{name}' does not exist.");
				}
			}
			return System.Linq.Enumerable.ToArray(ids);
		}

		public static void Normalize(MotionDatabaseSource source, int[] groups) {
			int count = 0;
			foreach (var frame in source.Frames) {
				if (Eligible(source.Clips[frame.ClipIndex], groups)) {
					count++;
				}
			}
			if (count == 0) {
				throw new ArgumentException("Normalization groups must contain animation frames.");
			}
			for (int feature = 0; feature < source.FeatureCount; feature++) {
				double mean = 0d;
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					if (Eligible(source.Clips[source.Frames[frame].ClipIndex], groups)) {
						mean += source.Features[frame * source.FeatureCount + feature];
					}
				}
				mean /= count;
				double variance = 0d;
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					if (Eligible(source.Clips[source.Frames[frame].ClipIndex], groups)) {
						double difference = source.Features[frame * source.FeatureCount + feature] - mean;
						variance += difference * difference;
					}
				}
				source.FeatureScales[feature] = 1f / math.max(0.1f, (float)Math.Sqrt(variance / count));
			}
		}
		private static bool Eligible(in MotionClipSource clip, int[] groups) {
			if (groups.Length == 0) {
				return true;
			}
			foreach (int group in groups) {
				if (System.Array.IndexOf(clip.Groups, group) >= 0) {
					return true;
				}
			}
			return false;
		}

	}
}
