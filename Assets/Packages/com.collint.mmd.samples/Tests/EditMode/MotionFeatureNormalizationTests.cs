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
				Clips = new[] { new MotionClipSource { Groups = new[] { 1 } }, new MotionClipSource { Groups = new[] { 2 } } },
				Frames = new[] { new MotionFrame { ClipIndex = 0 }, new MotionFrame { ClipIndex = 0 },
					new MotionFrame { ClipIndex = 1 }, new MotionFrame { ClipIndex = 1 } }
			};
			MotionFeatureNormalization.Normalize(source, new[] { 1 });
			Assert.That(source.FeatureScales[0], Is.EqualTo(1f));
			MotionFeatureNormalization.Normalize(source, Array.Empty<int>());
			Assert.That(source.FeatureScales[0], Is.LessThan(0.02f));
			Assert.Throws<ArgumentException>(() => MotionFeatureNormalization.Normalize(source, new[] { 4 }));
		}

		[Test]
		public void ReferenceNamesResolveSharedGroupsAndRejectMissingGroups() {
			var groups = new[] { new MotionGroup { Name = new FixedString64Bytes("Walk"), Id = 1 },
				new MotionGroup { Name = new FixedString64Bytes("Start"), Id = 2 } };
			Assert.That(MotionFeatureNormalization.ResolveGroups(groups, Array.Empty<string>()), Is.Empty);
			Assert.That(MotionFeatureNormalization.ResolveGroups(groups, new[] { "Walk", "Start", "Walk" }), Is.EquivalentTo(new[] { 1, 2 }));
			Assert.Throws<ArgumentException>(() => MotionFeatureNormalization.ResolveGroups(groups, new[] { "Missing" }));
		}
	}
}
