using System;
using System.Collections.Generic;
using ColInt.MMD.Playback.Data;
using UnityEngine.UIElements;

namespace ColInt.MMD.Editor.Playback {
	public static class MmdAnimationMaskTree {
		public static List<TreeViewItemData<MmdAnimationMaskTreeItem>> Build(MmdAnimationBoneWeight[] bones, string filter) {
			var visible = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int index = 0; index < bones.Length; index++) {
				if (string.IsNullOrEmpty(filter) || bones[index].Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) {
					visible.Add(bones[index].Path, index);
				}
			}
			var children = new Dictionary<int, List<int>>();
			var roots = new List<int>();
			for (int index = 0; index < bones.Length; index++) {
				string path = bones[index].Path;
				if (!visible.ContainsKey(path)) {
					continue;
				}
				int slash = path.LastIndexOf('/');
				if (slash >= 0 && visible.TryGetValue(path.Substring(0, slash), out int parent)) {
					if (!children.TryGetValue(parent, out var branch)) {
						branch = new List<int>();
						children.Add(parent, branch);
					}
					branch.Add(index);
				} else {
					roots.Add(index);
				}
			}
			return BuildBranch(roots, children, 0);
		}

		private static List<TreeViewItemData<MmdAnimationMaskTreeItem>> BuildBranch(List<int> indices, Dictionary<int, List<int>> children, int depth) {
			var result = new List<TreeViewItemData<MmdAnimationMaskTreeItem>>(indices.Count);
			foreach (int index in indices) {
				var item = new MmdAnimationMaskTreeItem { BoneIndex = index, Depth = depth };
				result.Add(children.TryGetValue(index, out var branch)
					? new TreeViewItemData<MmdAnimationMaskTreeItem>(index + 1, item, BuildBranch(branch, children, depth + 1))
					: new TreeViewItemData<MmdAnimationMaskTreeItem>(index + 1, item));
			}
			return result;
		}
	}
}
