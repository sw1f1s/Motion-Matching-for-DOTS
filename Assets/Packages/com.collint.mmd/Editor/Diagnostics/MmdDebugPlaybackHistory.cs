using System.Collections.Generic;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugPlaybackHistory {
		public const int Capacity = 5;

		private readonly List<MmdDebugClipHistoryEntry> _clips = new List<MmdDebugClipHistoryEntry>();
		private bool _hasPrevious;
		private int _lastClip;
		private uint _lastRevision;
		private string _lastName;
		private float _lastTime;

		public bool HasTransition { get; private set; }
		public string FromClip { get; private set; }
		public float FromTime { get; private set; }
		public bool SameClip { get; private set; }
		public uint RevisionGap { get; private set; }
		public IReadOnlyList<MmdDebugClipHistoryEntry> Clips => _clips;

		public void Observe(MmdDebugEntitySnapshot snapshot, string observedAt) {
			if (!snapshot.ValidClip) {
				return;
			}
			bool changed = _hasPrevious && (_lastClip != snapshot.Playback.ClipIndex || _lastRevision != snapshot.Playback.Revision);
			if (changed) {
				HasTransition = true;
				FromClip = _lastName;
				FromTime = _lastTime;
				SameClip = _lastClip == snapshot.Playback.ClipIndex;
				RevisionGap = unchecked(snapshot.Playback.Revision - _lastRevision);
			}
			if (!_hasPrevious || _lastClip != snapshot.Playback.ClipIndex) {
				_clips.Insert(0, new MmdDebugClipHistoryEntry(snapshot, observedAt));
				if (_clips.Count > Capacity) {
					_clips.RemoveAt(_clips.Count - 1);
				}
			}
			_hasPrevious = true;
			_lastClip = snapshot.Playback.ClipIndex;
			_lastRevision = snapshot.Playback.Revision;
			_lastName = snapshot.ClipName;
			_lastTime = snapshot.Playback.Time;
			snapshot.HasObservedTransition = HasTransition;
			snapshot.TransitionFrom = FromClip;
			snapshot.TransitionFromTime = FromTime;
			snapshot.SameClipTransition = SameClip;
			snapshot.TransitionRevisionGap = RevisionGap;
			snapshot.ClipHistory = _clips.ToArray();
		}
	}
}
