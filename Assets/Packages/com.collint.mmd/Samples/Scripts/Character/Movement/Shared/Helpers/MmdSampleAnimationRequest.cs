using System.Runtime.CompilerServices;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Components;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Helpers {
	public static class MmdSampleAnimationRequest {
		private const float TakeoffLift = 0.03f;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Cancel(ref MmdActionRequest request) {
			request.ActionId = 0;
			request.PreferredClip = -1;
			request.Revision++;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GaitAction(int stand, int walk, int run, float speed, float walkSpeed) {
			return speed < walkSpeed * 0.3f ? stand : speed < walkSpeed * 1.5f ? walk : run;
		}

		public static float TakeoffTime(ref MotionDatabase database, int clipIndex, float fallback) {
			ref var clip = ref database.Clips[clipIndex];
			float origin = database.Frames[clip.FirstFrame].Root.pos.y;
			for (int frame = 1; frame < clip.FrameCount; frame++) {
				ref var sample = ref database.Frames[clip.FirstFrame + frame];
				if (sample.Root.pos.y - origin >= TakeoffLift) {
					return sample.Time;
				}
			}
			return fallback;
		}

		public static void Play(ref MotionDatabase database, DynamicBuffer<MmdSearchQueryElement> query, int action, ref MmdActionRequest request) {
			float best = float.PositiveInfinity;
			int selected = -1;
			for (int clip = 0; clip < database.Clips.Length; clip++) {
				if (database.Clips[clip].ActionId != action) {
					continue;
				}
				int offset = database.Clips[clip].FirstFrame * database.FeatureCount;
				float cost = 0f;
				for (int feature = 15; feature < database.FeatureCount; feature++) {
					float difference = (query[feature].Value - database.Features[offset + feature]) * database.FeatureScales[feature];
					cost += difference * difference;
				}
				if (cost < best) {
					best = cost;
					selected = clip;
				}
			}
			request.ActionId = action;
			request.PreferredClip = selected;
			request.Revision++;
		}
	}
}
