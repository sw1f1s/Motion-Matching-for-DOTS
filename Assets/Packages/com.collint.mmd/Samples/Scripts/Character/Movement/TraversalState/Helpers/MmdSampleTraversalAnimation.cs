using System.Runtime.CompilerServices;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public static class MmdSampleTraversalAnimation {
		public static MmdSampleTraversalClip Profile(ref MotionDatabase database, int clipIndex, bool catching) {
			ref var clip = ref database.Clips[clipIndex];
			float height = 0f;
			float peak = 0f;
			float lift = 0.06f;
			for (int frame = 0; frame < clip.FrameCount; frame++) {
				ref var sample = ref database.Frames[clip.FirstFrame + frame];
				if (sample.Root.pos.y > height) {
					height = sample.Root.pos.y;
					peak = sample.Time;
				}
			}
			for (int frame = 1; frame < clip.FrameCount; frame++) {
				ref var sample = ref database.Frames[clip.FirstFrame + frame];
				if (sample.Root.pos.y > math.max(0.02f, height * 0.03f)) {
					lift = sample.Time;
					break;
				}
			}
			float duration = clip.Duration;
			for (int frame = 1; frame < clip.FrameCount; frame++) {
				if (database.Frames[clip.FirstFrame + frame].Root.pos.y >= height * 0.999f) {
					peak = database.Frames[clip.FirstFrame + frame].Time;
					break;
				}
			}
			lift = math.clamp(lift, duration * 0.06f, peak - duration * 0.08f);
			int tail = math.max(0, clip.FrameCount - 1 - (int)(clip.SampleRate * 0.15f));
			ref var last = ref database.Frames[clip.FirstFrame + clip.FrameCount - 1];
			ref var previous = ref database.Frames[clip.FirstFrame + tail];
			float exitSpeed = math.length((last.Root.pos - previous.Root.pos).xz) / math.max(0.01f, last.Time - previous.Time);
			int entry = math.min(clip.FrameCount - 1, math.max(1, (int)(clip.SampleRate * 0.15f)));
			ref var initial = ref database.Frames[clip.FirstFrame + entry];
			float entrySpeed = math.length(initial.Root.pos.xz) / math.max(0.01f, initial.Time);
			return new MmdSampleTraversalClip {
				ClipIndex = clipIndex, ActionId = clip.ActionId, LiftTime = lift, PeakTime = peak, RootHeight = height,
				TransferTime = math.lerp(peak, duration, 0.65f), Duration = duration,
				EntrySpeed = catching ? exitSpeed : entrySpeed, ExitSpeed = exitSpeed, CatchHeight = math.max(0.4f, height + 0.25f)
			};
		}

		public static MmdSampleTraversalClip Fit(ref MotionDatabase database, MmdSampleTraversalClip profile,
			in MmdSampleTraversalTarget target, bool airborne) {
			float distance = math.max(0f, math.dot(target.Front - target.Start, math.forward(target.Rotation)));
			profile.StartTime = airborne ? 0f : math.max(0f, profile.LiftTime - distance / math.max(0.6f, profile.EntrySpeed) - 0.08f);
			float origin = MmdAnimationMath.RootAt(ref database, profile.ClipIndex, profile.StartTime).pos.y;
			float bodyLift = target.Across ? 0.6f : 0f;
			float fraction = math.saturate((target.Height + 0.04f - bodyLift) / (target.Height + 0.08f));
			float required = origin + (profile.RootHeight - origin) * fraction;
			ref var clip = ref database.Clips[profile.ClipIndex];
			profile.ClearanceTime = profile.PeakTime;
			profile.TransferTime = profile.PeakTime;
			for (int frame = 1; frame < clip.FrameCount; frame++) {
				ref var sample = ref database.Frames[clip.FirstFrame + frame];
				if (sample.Time >= profile.StartTime + 0.04f && sample.Root.pos.y >= required) {
					profile.ClearanceTime = sample.Time;
					break;
				}
			}
			if (target.Across) {
				for (int frame = 1; frame < clip.FrameCount; frame++) {
					ref var sample = ref database.Frames[clip.FirstFrame + frame];
					if (sample.Time >= profile.PeakTime && sample.Root.pos.y >= required) {
						profile.TransferTime = sample.Time;
					}
				}
			} else {
				// Fit the run-out to the remaining ledge distance rather than compressing
				// a fixed-length running tail into the same short physical displacement.
				float transferDuration = 0.65f;
				if (profile.EntrySpeed > 1.5f) {
					float transferDistance = math.max(0f, math.dot(target.End - target.Front, math.forward(target.Rotation)));
					transferDuration = math.clamp(transferDistance / math.max(profile.EntrySpeed, profile.ExitSpeed), 0.15f, 0.4f);
				}
				profile.Duration = math.min(profile.Duration, math.max(profile.ClearanceTime + transferDuration, profile.PeakTime + 0.08f));
				profile.TransferTime = profile.Duration;
			}
			profile.ClearanceTime = math.clamp(profile.ClearanceTime, profile.StartTime + 0.04f, profile.Duration - 0.08f);
			profile.TransferTime = math.clamp(profile.TransferTime, profile.ClearanceTime + 0.04f, profile.Duration);
			return profile;
		}

		public static MmdSampleTraversalClip Select(ref MotionDatabase database, DynamicBuffer<MmdSampleTraversalClip> clips,
			DynamicBuffer<MmdSearchQueryElement> query, int action, float speed, float height, bool catching) {
			float best = float.PositiveInfinity;
			MmdSampleTraversalClip selected = default;
			for (int index = 0; index < clips.Length; index++) {
				var candidate = clips[index];
				if (candidate.ActionId != action) {
					continue;
				}
				float cost = math.square(candidate.EntrySpeed - speed) * 2f;
				if (catching) {
					cost += math.square(candidate.CatchHeight - height) * 8f;
				}
				int offset = database.Clips[candidate.ClipIndex].FirstFrame * database.FeatureCount;
				for (int feature = 15; feature < database.FeatureCount; feature++) {
					float difference = (query[feature].Value - database.Features[offset + feature]) * database.FeatureScales[feature];
					cost += difference * difference * 0.05f;
				}
				if (cost < best) {
					best = cost;
					selected = candidate;
				}
			}
			return selected;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Start(ref MmdSampleTraversalProgress progress, ref MmdSampleMovement movement, ref MmdActionRequest request,
			ref MmdPlayback playback, ref PhysicsMassOverride massOverride, ref PhysicsGravityFactor gravity, ref MmdInertialization smoothing) {
			progress.PreviousMassOverride = massOverride;
			progress.PreviousGravity = gravity;
			progress.PreviousSmoothing = smoothing;
			massOverride = new PhysicsMassOverride { IsKinematic = 1 };
			gravity.Value = 0f;
			movement = new MmdSampleMovement { IgnoreGround = true };
			request.ActionId = progress.Clip.ActionId;
			request.PreferredClip = progress.Clip.ClipIndex;
			request.Revision++;
			playback.Speed = 1f;
			smoothing.PoseDuration = math.max(smoothing.PoseDuration, 0.18f);
			smoothing.RootDuration = 0f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Finish(in MmdSampleTraversalProgress progress, ref MmdSampleMovement movement,
			ref PhysicsMassOverride massOverride, ref PhysicsGravityFactor gravity, ref MmdInertialization smoothing) {
			movement = default;
			massOverride = progress.PreviousMassOverride;
			gravity = progress.PreviousGravity;
			smoothing = progress.PreviousSmoothing;
		}
	}
}
