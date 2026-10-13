using ColInt.MMD.Editor.Diagnostics;
using ColInt.MMD.Playback.Components;
using NUnit.Framework;

namespace ColInt.MMD.Tests {
	public class MmdDebugPlaybackHistoryTests {
		[Test]
		public void KeepsCurrentClipAndOnlyFourPreviousClipsNewestFirst() {
			var history = new MmdDebugPlaybackHistory();
			for (int index = 0; index < 9; index++) {
				var snapshot = Snapshot(index, (uint)index, 0.1f);
				history.Observe(snapshot, "12:00:00");
				Assert.That(snapshot.ClipHistory.Length, Is.LessThanOrEqualTo(5));
			}
			Assert.That(history.Clips.Count, Is.EqualTo(5));
			for (int index = 0; index < 5; index++) {
				Assert.That(history.Clips[index].ClipIndex, Is.EqualTo(8 - index));
			}
		}

		[Test]
		public void SameClipReselectionRecordsTransitionWithoutFillingClipHistory() {
			var history = new MmdDebugPlaybackHistory();
			history.Observe(Snapshot(2, 1, 0.4f), "12:00:00");
			history.Observe(Snapshot(2, 1, 0.6f), "12:00:01");
			Assert.That(history.HasTransition, Is.False);
			var seek = Snapshot(2, 2, 0.2f);
			history.Observe(seek, "12:00:02");
			Assert.That(seek.HasObservedTransition, Is.True);
			Assert.That(seek.SameClipTransition, Is.True);
			Assert.That(seek.TransitionFrom, Is.EqualTo("Clip 2"));
			Assert.That(seek.TransitionFromTime, Is.EqualTo(0.6f));
			Assert.That(seek.ClipHistory.Length, Is.EqualTo(1));
			var next = Snapshot(3, 5, 1f);
			history.Observe(next, "12:00:03");
			Assert.That(next.SameClipTransition, Is.False);
			Assert.That(next.TransitionFrom, Is.EqualTo("Clip 2"));
			Assert.That(next.TransitionRevisionGap, Is.EqualTo(3));
			Assert.That(next.ClipHistory.Length, Is.EqualTo(2));
		}

		[Test]
		public void InvalidPlaybackDoesNotAddClipAndEarlierSnapshotsRemainStable() {
			var history = new MmdDebugPlaybackHistory();
			var first = Snapshot(0, 0, 0f);
			history.Observe(first, "12:00:00");
			var invalid = Snapshot(99, 1, 0f);
			invalid.ValidClip = false;
			history.Observe(invalid, "12:00:01");
			history.Observe(Snapshot(1, 1, 0f), "12:00:02");
			Assert.That(history.Clips.Count, Is.EqualTo(2));
			Assert.That(first.ClipHistory.Length, Is.EqualTo(1));
			Assert.That(first.ClipHistory[0].ClipIndex, Is.Zero);
		}

		private static MmdDebugEntitySnapshot Snapshot(int clip, uint revision, float time) {
			return new MmdDebugEntitySnapshot {
				ValidClip = true, ClipName = $"Clip {clip}", Groups = "Locomotion",
				Playback = new MmdPlayback { ClipIndex = clip, Revision = revision, Time = time }
			};
		}
	}
}
