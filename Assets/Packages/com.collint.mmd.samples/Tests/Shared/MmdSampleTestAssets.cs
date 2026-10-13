#if UNITY_EDITOR
using System;
using UnityEditor;

namespace ColInt.MMD.Tests {
	public static class MmdSampleTestAssets {
		private const string FixtureScriptGuid = "46cb6f0ed76ce418cb060c9dc2798c1c";
		private const string FixtureScriptSuffix = "/Tests/Shared/MotionDatabaseFixture.cs";

		public static string GetPath(string relativePath) {
			string scriptPath = AssetDatabase.GUIDToAssetPath(FixtureScriptGuid);
			if (!scriptPath.EndsWith(FixtureScriptSuffix, StringComparison.Ordinal)) {
				throw new InvalidOperationException("The MMD Samples test fixture script is missing.");
			}
			return scriptPath.Substring(0, scriptPath.Length - FixtureScriptSuffix.Length) + "/" + relativePath;
		}
	}
}
#endif
