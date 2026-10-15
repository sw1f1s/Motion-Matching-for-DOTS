using System;
using System.Collections.Generic;
using System.Text;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Playback.Authoring;
using ColInt.MMD.Playback.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Helpers {
	public static class MmdAnimationLayerBuilder {
		// The caller owns the set; retain its database until all attached actors and the set are disposed.
		public static MmdAnimationLayerSet Build(BlobAssetReference<MotionDatabase> database, MmdAnimationLayerSettings[] settings) {
			if (!database.IsCreated) {
				throw new ArgumentException("A loaded database is required.");
			}
			var prepared = Prepare(ref database.Value, settings, out var weights, out var groups);
			using var builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref builder.ConstructRoot<MmdAnimationLayersDefinition>();
			var layers = builder.Allocate(ref root.Layers, prepared.Length);
			for (int index = 0; index < prepared.Length; index++) {
				var source = prepared[index];
				ref var layer = ref layers[index];
				layer.Name = source.Name;
				layer.Source = settings[index].Source;
				layer.GroupId = source.GroupId;
				layer.ClipIndex = source.ClipIndex;
				layer.Time = source.Time;
				layer.Speed = source.Speed;
				layer.Weight = source.TargetWeight;
				layer.FadeDuration = source.FadeDuration;
				layer.TransitionDuration = settings[index].Matching.TransitionDuration;
				layer.Search = source.SearchSettings;
				layer.Channels = source.Channels;
				layer.FirstPose = root.PoseCount;
				int count = 0;
				for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
					if (weights[index * database.Value.Parents.Length + bone] > 0f) {
						count++;
					}
				}
				var bones = builder.Allocate(ref layer.Bones, count);
				int slot = 0;
				for (int bone = 0; bone < database.Value.Parents.Length; bone++) {
					float weight = weights[index * database.Value.Parents.Length + bone];
					if (weight > 0f) {
						bones[slot++] = new MmdAnimationMaskedBone { BoneIndex = bone, Weight = weight };
					}
				}
				root.PoseCount = checked(root.PoseCount + count);
				var active = builder.Allocate(ref layer.ActiveGroups, source.GroupWordCount);
				for (int word = 0; word < active.Length; word++) {
					active[word] = groups[source.FirstGroupWord + word];
				}
			}
			return new MmdAnimationLayerSet(database, builder.CreateBlobAssetReference<MmdAnimationLayersDefinition>(Allocator.Persistent));
		}

		public static float[] ResolveMask(ref MotionDatabase database, MmdAnimationMask mask) {
			if (mask == null || mask.Bones == null || mask.Bones.Length != database.Parents.Length) {
				throw new ArgumentException("The animation mask must contain the same skeleton as the baked database. Synchronize it with the target rig.");
			}
			var indices = new Dictionary<int, int>();
			for (int bone = 0; bone < database.BoneNameHashes.Length; bone++) {
				if (!indices.TryAdd(database.BoneNameHashes[bone], bone)) {
					throw new ArgumentException("The database contains duplicate bone path hashes.");
				}
			}
			var result = new float[database.Parents.Length];
			var visited = new HashSet<int>();
			foreach (var bone in mask.Bones) {
				if (string.IsNullOrEmpty(bone.Path) || !indices.TryGetValue(BonePathHash.Calculate(bone.Path), out int index) ||
					!visited.Add(index) || !math.isfinite(bone.Weight) || bone.Weight < 0f || bone.Weight > 1f) {
					throw new ArgumentException("The mask contains an unknown/duplicate bone path or an invalid weight: " + bone.Path);
				}
				result[index] = bone.Weight;
			}
			return result;
		}

		public static int ResolveClip(ref MotionDatabase database, string group, int clipInGroup) {
			if (string.IsNullOrWhiteSpace(group) || Encoding.UTF8.GetByteCount(group) > 61 || clipInGroup < 0) {
				throw new ArgumentException("Select an animation group and a clip in that group.");
			}
			int id = MotionDatabaseGroups.GetId(ref database, new FixedString64Bytes(group));
			for (int clip = 0; clip < database.Clips.Length; clip++) {
				if (MotionDatabaseGroups.Contains(ref database, database.Clips[clip], id)) {
					if (clipInGroup == 0) {
						return clip;
					}
					clipInGroup--;
				}
			}
			throw new ArgumentException("The selected clip does not exist in the animation group.");
		}

		private static MmdAnimationLayerInitialization[] Prepare(ref MotionDatabase database, MmdAnimationLayerSettings[] settings, out float[] weights, out ulong[] activeGroups) {
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			var layers = new MmdAnimationLayerInitialization[settings.Length];
			weights = new float[checked(settings.Length * database.Parents.Length)];
			var names = new HashSet<string>(StringComparer.Ordinal);
			var groups = new List<ulong>();
			for (int index = 0; index < settings.Length; index++) {
				var value = settings[index];
				if (value == null || string.IsNullOrWhiteSpace(value.Name) || Encoding.UTF8.GetByteCount(value.Name) > 61 || !names.Add(value.Name) ||
					!math.isfinite(value.Time) || value.Time < 0f || !math.isfinite(value.Speed) || value.Speed < 0f ||
					!math.isfinite(value.Weight) || value.Weight < 0f || value.Weight > 1f ||
					!math.isfinite(value.FadeDuration) || value.FadeDuration < 0f || value.Channels == MotionPoseChannels.None ||
					(value.Channels & ~MotionPoseChannels.All) != 0) {
					throw new ArgumentException("Layers require unique names, finite nonnegative timing, a weight in 0–1 and valid pose channels.");
				}
				if (value.Source != MmdAnimationLayerSource.Clip && value.Source != MmdAnimationLayerSource.Group) {
					throw new ArgumentException("Select Clip or Group as the layer source.");
				}
				bool group = value.Source == MmdAnimationLayerSource.Group;
				if (!ValidMatching(value.Matching) || group && value.Speed <= 0f) {
					throw new ArgumentException("Group layers need positive playback speed and finite nonnegative matching settings.");
				}
				int clip = ResolveClip(ref database, value.Group, group ? 0 : value.ClipInGroup);
				float[] mask = ResolveMask(ref database, value.Mask);
				int firstGroup = groups.Count;
				ResolveActiveGroups(ref database, value, groups);
				Array.Copy(mask, 0, weights, index * mask.Length, mask.Length);
				layers[index] = new MmdAnimationLayerInitialization {
					FirstGroupWord = firstGroup, GroupWordCount = groups.Count - firstGroup,
					GroupId = group ? MotionDatabaseGroups.GetId(ref database, new FixedString64Bytes(value.Group)) : 0,
					SearchSettings = new MmdSearchSettings {
						Interval = value.Matching.Interval, SwitchPenalty = value.Matching.SwitchPenalty,
						MinimumRemainingTime = value.Matching.MinimumRemainingTime, MinimumSwitchTime = value.Matching.MinimumSwitchTime,
						MinimumTransitionTime = value.Matching.TransitionDuration, TrajectoryDirectionWeight = 4f
					},
					Name = new FixedString64Bytes(value.Name), ClipIndex = clip, Time = MmdAnimationMath.LocalTime(database.Clips[clip], value.Time),
					Speed = value.Speed, TargetWeight = value.Weight, FadeDuration = value.FadeDuration,
					Channels = value.Channels
				};
			}
			activeGroups = groups.ToArray();
			return layers;
		}

		private static bool ValidMatching(MmdAnimationLayerMatchingSettings settings) {
			return settings != null && math.isfinite(settings.Interval) && settings.Interval >= 0f &&
				math.isfinite(settings.SwitchPenalty) && settings.SwitchPenalty >= 0f &&
				math.isfinite(settings.MinimumRemainingTime) && settings.MinimumRemainingTime >= 0f &&
				math.isfinite(settings.MinimumSwitchTime) && settings.MinimumSwitchTime >= 0f &&
				math.isfinite(settings.TransitionDuration) && settings.TransitionDuration >= 0f;
		}

		private static void ResolveActiveGroups(ref MotionDatabase database, MmdAnimationLayerSettings settings, List<ulong> groups) {
			if (settings.ActiveGroups == null) {
				throw new ArgumentException("A base group selection is required. Use an empty list to disable the layer.");
			}
			var visited = new HashSet<string>(StringComparer.Ordinal);
			var ids = new List<int>();
			int bitCount = 0;
			foreach (string name in settings.ActiveGroups) {
				if (string.IsNullOrWhiteSpace(name) || Encoding.UTF8.GetByteCount(name) > 61 || !visited.Add(name)) {
					throw new ArgumentException("Active base groups require unique nonempty names.");
				}
				var group = MotionDatabaseGroups.GetGroup(ref database, new FixedString64Bytes(name));
				if (group.Mode == MotionPlaybackMode.LayerOnly) {
					throw new ArgumentException("Layer Only groups cannot activate a layer. Select base animation groups.");
				}
				ids.Add(group.Id);
				bitCount = math.max(bitCount, group.Id);
			}
			using (var words = new NativeArray<ulong>(BitMask.WordsForBits(bitCount), Allocator.Temp)) {
				var mask = new BitMask(words, bitCount);
				foreach (int id in ids) {
					mask.Set(id - 1);
				}
				for (int index = 0; index < mask.WordCount; index++) {
					groups.Add(mask.GetWord(index));
				}
			}
		}
	}
}
