using System;

namespace ColInt.MMD.Editor.Diagnostics {
	[Serializable]
	public sealed class MmdDebugClipCatalog {
		public int Version { get; set; } = 1;
		public string Fingerprint { get; set; }
		public string DatabasePath { get; set; }
		public string[] Names { get; set; }
		public string[] AssetPaths { get; set; }
	}
}
