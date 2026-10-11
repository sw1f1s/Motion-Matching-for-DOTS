using System;
using ColInt.MMD.Database.Data;
using Unity.Mathematics;

namespace ColInt.MMD.Editor.Baking {
	public static class MotionFeatureNormalization {
		public static uint ResolveTags(MotionGroup[] groups, string[] names) {
			uint tags = 0;
			foreach (string name in names) {
				bool found = false;
				foreach (var group in groups) {
					if (group.Name.ToString() == name) {
						tags |= group.Tags;
						found = true;
						break;
					}
				}
				if (!found) {
					throw new ArgumentException($"Normalization group '{name}' does not exist.");
				}
			}
			return tags;
		}

		public static void Normalize(MotionDatabaseSource source, uint tags) {
			int count = 0;
			foreach (var frame in source.Frames) {
				if (tags == 0 || (source.Clips[frame.ClipIndex].Tags & tags) != 0) {
					count++;
				}
			}
			if (count == 0) {
				throw new ArgumentException("Normalization groups must contain animation frames.");
			}
			for (int feature = 0; feature < source.FeatureCount; feature++) {
				double mean = 0d;
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					if (tags == 0 || (source.Clips[source.Frames[frame].ClipIndex].Tags & tags) != 0) {
						mean += source.Features[frame * source.FeatureCount + feature];
					}
				}
				mean /= count;
				double variance = 0d;
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					if (tags == 0 || (source.Clips[source.Frames[frame].ClipIndex].Tags & tags) != 0) {
						double difference = source.Features[frame * source.FeatureCount + feature] - mean;
						variance += difference * difference;
					}
				}
				source.FeatureScales[feature] = 1f / math.max(0.1f, (float)Math.Sqrt(variance / count));
			}
		}
	}
}
