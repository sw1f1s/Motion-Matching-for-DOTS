using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Editor.Baking;
using NUnit.Framework;
using Unity.Collections;

namespace ColInt.MMD.Tests {
	public class MotionFeatureNormalizationTests {
		[Test]
		public void ReferenceGroupsKeepFeatureScalesStableWhenOtherStancesAreAdded() {
			var source = new MotionDatabaseSource {
				FeatureCount = 1, FeatureScales = new float[1], Features = new[] { 0f, 2f, 100f, 200f },
				Clips = new[] { new MotionClip { Tags = 1 }, new MotionClip { Tags = 2 } },
				Frames = new[] { new MotionFrame { ClipIndex = 0 }, new MotionFrame { ClipIndex = 0 },
					new MotionFrame { ClipIndex = 1 }, new MotionFrame { ClipIndex = 1 } }
			};
			MotionFeatureNormalization.Normalize(source, 1);
			Assert.That(source.FeatureScales[0], Is.EqualTo(1f));
			MotionFeatureNormalization.Normalize(source, 0);
			Assert.That(source.FeatureScales[0], Is.LessThan(0.02f));
			Assert.Throws<ArgumentException>(() => MotionFeatureNormalization.Normalize(source, 4));
		}

		[Test]
		public void ReferenceNamesResolveSharedTagsAndRejectMissingGroups() {
			var groups = new[] { new MotionGroup { Name = new FixedString64Bytes("Walk"), Tags = 1 },
				new MotionGroup { Name = new FixedString64Bytes("Start"), Tags = 2 } };
			Assert.That(MotionFeatureNormalization.ResolveTags(groups, Array.Empty<string>()), Is.Zero);
			Assert.That(MotionFeatureNormalization.ResolveTags(groups, new[] { "Walk", "Start", "Walk" }), Is.EqualTo(3));
			Assert.Throws<ArgumentException>(() => MotionFeatureNormalization.ResolveTags(groups, new[] { "Missing" }));
		}
	}
}
