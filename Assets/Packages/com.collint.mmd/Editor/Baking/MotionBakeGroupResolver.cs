using System.Collections.Generic;
using System.Text;
using System;
using ColInt.MMD.Database.Data;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	public static class MotionBakeGroupResolver {
		public static MotionBakeSelection Resolve(MotionBakeGroup[] groups) {
			if (groups == null) {
				throw new ArgumentException("An animation group list is required.");
			}
			var names = new HashSet<string>(StringComparer.Ordinal);
			var clips = new List<MotionBakeClip>();
			var indices = new Dictionary<AnimationClip, int>();
			var metadata = new MotionGroup[groups.Length];
			for (int index = 0; index < groups.Length; index++) {
				MotionBakeGroup group = groups[index];
				if (group == null || string.IsNullOrWhiteSpace(group.Name) || group.Name != group.Name.Trim() ||
					Encoding.UTF8.GetByteCount(group.Name) > 61 || !names.Add(group.Name) || group.Clips == null ||
					(group.Playback != MotionPlaybackMode.MotionMatching && group.Playback != MotionPlaybackMode.Action && group.Playback != MotionPlaybackMode.LayerOnly)) {
					throw new ArgumentException("Groups need unique names without surrounding whitespace (up to 61 UTF-8 bytes), a clip list and a valid playback mode.");
				}
				int id = index + 1;
				int action = group.Playback == MotionPlaybackMode.Action ? index + 1 : 0;
				metadata[index] = new MotionGroup { Name = new FixedString64Bytes(group.Name), Id = id, ActionId = action, Mode = group.Playback };
				foreach (AnimationClip clip in group.Clips) {
					if (clip == null || clip.length <= 0f) {
						throw new ArgumentException($"Group '{group.Name}' contains a missing or empty animation.");
					}
					bool loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
					if (action > 0 && loop) {
						throw new ArgumentException($"Action '{group.Name}' contains looping clip '{clip.name}'. Disable Loop Time on the animation.");
					}
					if (indices.TryGetValue(clip, out int existing)) {
						if (clips[existing].ActionId != action || clips[existing].Mode != group.Playback) {
							throw new ArgumentException($"Clip '{clip.name}' cannot belong to different actions or groups with different playback modes.");
						}
						var memberships = new List<int>(clips[existing].Groups);
						if (!memberships.Contains(id)) {
							memberships.Add(id);
						}
						clips[existing].Groups = memberships.ToArray();
					} else {
						indices.Add(clip, clips.Count);
						clips.Add(new MotionBakeClip { Clip = clip, Loop = loop, Groups = new[] { id }, ActionId = action, Mode = group.Playback });
					}
				}
			}
			return new MotionBakeSelection { Groups = metadata, Clips = clips.ToArray() };
		}
	}
}
