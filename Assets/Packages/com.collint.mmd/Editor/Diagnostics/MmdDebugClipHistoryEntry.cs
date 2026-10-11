namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugClipHistoryEntry {
		public int ClipIndex { get; }
		public string ClipName { get; }
		public string Groups { get; }
		public string ObservedAt { get; }
		public float EntryTime { get; }
		public uint Revision { get; }

		public MmdDebugClipHistoryEntry(MmdDebugEntitySnapshot snapshot, string observedAt) {
			ClipIndex = snapshot.Playback.ClipIndex;
			ClipName = snapshot.ClipName;
			Groups = snapshot.Groups;
			ObservedAt = observedAt;
			EntryTime = snapshot.Playback.Time;
			Revision = snapshot.Playback.Revision;
		}
	}
}
