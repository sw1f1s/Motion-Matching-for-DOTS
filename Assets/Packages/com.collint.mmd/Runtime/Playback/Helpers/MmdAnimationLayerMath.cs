using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Playback.Helpers {
	internal static class MmdAnimationLayerMath {
		public static bool Active(ref MotionDatabase database, in MmdPlayback playback, ref MmdAnimationLayerDefinition definition) {
			if (playback.ClipIndex < 0 || playback.ClipIndex >= database.Clips.Length || definition.Bones.Length == 0) {
				return false;
			}
			for (int word = 0; word < definition.ActiveGroups.Length; word++) {
				if ((MotionDatabaseGroups.Word(ref database, database.Clips[playback.ClipIndex], word) & definition.ActiveGroups[word]) != 0) {
					return true;
				}
			}
			return false;
		}

		public static bool Valid(in MmdAnimationLayerControl control, in MmdAnimationLayer state, int clips) {
			return (control.Source == MmdAnimationLayerSource.Clip || control.Source == MmdAnimationLayerSource.Group) &&
				(control.Source != MmdAnimationLayerSource.Group || control.GroupId > 0 && control.Speed > 0f) &&
				math.isfinite(state.SearchCountdown) && math.isfinite(state.TransitionElapsed) && state.TransitionElapsed >= 0f &&
				state.ClipIndex >= 0 && state.ClipIndex < clips && math.isfinite(state.Time) && state.Time >= 0f &&
				math.isfinite(control.Speed) && control.Speed >= 0f && math.isfinite(control.TargetWeight) && control.TargetWeight >= 0f && control.TargetWeight <= 1f &&
				math.isfinite(control.FadeDuration) && control.FadeDuration >= 0f && math.isfinite(state.Weight) && state.Weight >= 0f && state.Weight <= 1f;
		}

		public static void Select(ref MotionDatabase database, ref MmdAnimationLayer state, ref MmdAnimationLayerDefinition definition,
			DynamicBuffer<MmdAnimationLayerPose> history, int clip, float time) {
			for (int slot = 0; slot < definition.Bones.Length; slot++) {
				int index = definition.FirstPose + slot;
				var entry = history[index];
				var target = MmdAnimationMath.PoseAt(ref database, clip, time, definition.Bones[slot].BoneIndex);
				bool first = state.MatchedFrame < 0 || state.Weight <= 0f;
				entry.PositionOffset = first ? float3.zero : entry.Previous.Position - target.Position;
				entry.RotationOffset = first ? float3.zero : MmdAnimationMath.RotationVector(math.mul(entry.Previous.Rotation, math.inverse(target.Rotation)));
				entry.ScaleOffset = first ? float3.zero : entry.Previous.Scale - target.Scale;
				history[index] = entry;
			}
			state.ClipIndex = clip;
			state.Time = time;
			state.TransitionElapsed = state.MatchedFrame < 0 || state.Weight <= 0f ? definition.TransitionDuration : 0f;
		}
	}
}
