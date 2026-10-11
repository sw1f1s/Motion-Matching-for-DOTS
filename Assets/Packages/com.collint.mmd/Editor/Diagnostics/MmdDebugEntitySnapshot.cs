using ColInt.MMD.Actions.Components;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Editor.Diagnostics {
	public sealed class MmdDebugEntitySnapshot {
		public World World { get; set; }
		public Entity Entity { get; set; }
		public string Name { get; set; }
		public string WorldLabel { get; set; }
		public string ClipName { get; set; }
		public string ClipPath { get; set; }
		public string Groups { get; set; }
		public string RequiredGroups { get; set; }
		public string ExcludedGroups { get; set; }
		public string Status { get; set; }
		public string RenderStatus { get; set; }
		public string DatabasePath { get; set; }
		public string Problem { get; set; }
		public bool Disabled { get; set; }
		public bool Prefab { get; set; }
		public bool Paused { get; set; }
		public bool ValidClip { get; set; }
		public bool HasNames { get; set; }
		public bool Loop { get; set; }
		public float Duration { get; set; }
		public float Progress { get; set; }
		public int Frame { get; set; }
		public int ClipCount { get; set; }
		public int FrameCount { get; set; }
		public int BoneCount { get; set; }
		public int FeatureCount { get; set; }
		public MmdPlayback Playback { get; set; }
		public MmdSearchSettings Search { get; set; }
		public MmdActionState Action { get; set; }
		public float3 Position { get; set; }
		public float3 Velocity { get; set; }
		public bool HasPosition { get; set; }
		public bool HasVelocity { get; set; }
		public bool HasSmoothing { get; set; }
		public bool SmoothingCurrent { get; set; }
		public bool TransitionActive { get; set; }
		public float TransitionElapsed { get; set; }
		public float PoseTransitionDuration { get; set; }
		public float RootTransitionDuration { get; set; }
		public float PoseTransitionProgress { get; set; }
		public float RootTransitionProgress { get; set; }
		public bool HasObservedTransition { get; set; }
		public string TransitionFrom { get; set; }
		public float TransitionFromTime { get; set; }
		public bool SameClipTransition { get; set; }
		public uint TransitionRevisionGap { get; set; }
		public MmdDebugClipHistoryEntry[] ClipHistory { get; set; } = System.Array.Empty<MmdDebugClipHistoryEntry>();
	}
}
