using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.Playback.Helpers;
using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.MotionMatching.Helpers {
	public static class MotionSearch {
		private const float StationarySpeed = 0.05f;
		private const float StationaryDistance = 0.01f;
		private const float StationaryFacingDifference = 0.025f;

		public static int Find(ref MotionDatabase database, in DynamicBuffer<MmdSearchQueryElement> query,
			in MmdSearchSettings settings, in DynamicBuffer<MmdMotionGroup> groups, int continuationFrame, float playbackSpeed = 1f, float continuationTime = float.NaN) {
			if (query.Length != database.FeatureCount) {
				return -1;
			}
			float speed = math.clamp(playbackSpeed, 0f, 1f);
			float continuationAmount = 0f;
			if (math.isfinite(continuationTime) && continuationFrame >= 0 && continuationFrame + 1 < database.Frames.Length &&
				database.Frames[continuationFrame].ClipIndex == database.Frames[continuationFrame + 1].ClipIndex) {
				ref var sample = ref database.Frames[continuationFrame];
				float time = MmdAnimationMath.LocalTime(database.Clips[sample.ClipIndex], continuationTime);
				continuationAmount = math.saturate((time - sample.Time) / (database.Frames[continuationFrame + 1].Time - sample.Time));
			}
			bool stationary = IsStationary(query) && HasEligibleLoop(ref database, groups);
			bool leavingAction = continuationFrame >= 0 && continuationFrame < database.Frames.Length &&
				database.Clips[database.Frames[continuationFrame].ClipIndex].ActionId != 0;
			bool settledLoop = continuationFrame >= 0 && continuationFrame < database.Frames.Length &&
				database.Clips[database.Frames[continuationFrame].ClipIndex].Loop && IsStationary(ref database, continuationFrame, speed);
			// A settled loop must not re-enter a moving stop just to match the outgoing foot pose.
			bool requireLoop = stationary && (leavingAction || settledLoop || CanFinishOneShot(ref database, continuationFrame, speed)) ||
				settings.PreferLoop && HasEligibleLoop(ref database, groups, false);
			float2 requestedDirection = float2.zero;
			bool scoreDirection = settings.TrajectoryDirectionWeight > 0f && database.FeatureCount == LocomotionSchema.Dimension;
			if (scoreDirection) {
				float2 requested = new float2(query[8].Value, query[9].Value);
				scoreDirection = math.lengthsq(requested) > StationaryDistance * StationaryDistance;
				if (scoreDirection) {
					requestedDirection = math.normalize(requested);
				}
			}
			int best = -1;
			float bestCost = float.PositiveInfinity;
			// The current frame provides an initial bound; equal-cost matches still prefer the earliest frame.
			if (continuationFrame >= 0 && continuationFrame < database.Frames.Length) {
				ref var continuation = ref database.Frames[continuationFrame];
				ref var clip = ref database.Clips[continuation.ClipIndex];
				if (clip.ActionId == 0 && MmdMotionGroups.Matches(ref database, clip, groups) && (!requireLoop || clip.Loop) &&
					(clip.Loop || !(clip.Duration - continuation.Time < settings.MinimumRemainingTime)) &&
					continuationFrame != clip.FirstFrame + clip.FrameCount - 1) {
					float cost = Cost(ref database, query, settings, continuationFrame, speed, continuationAmount, 0f,
						requestedDirection, scoreDirection, bestCost);
					if (math.isfinite(cost)) {
						best = continuationFrame;
						bestCost = cost;
					}
				}
			}
			for (int clipIndex = 0; clipIndex < database.Clips.Length; clipIndex++) {
				ref MotionClip clip = ref database.Clips[clipIndex];
				if (clip.ActionId != 0 || !MmdMotionGroups.Matches(ref database, clip, groups) || requireLoop && !clip.Loop) {
					continue;
				}
				int end = clip.FirstFrame + clip.FrameCount - 1;
				for (int frame = clip.FirstFrame; frame < end; frame++) {
					ref MotionFrame candidate = ref database.Frames[frame];
					if (!clip.Loop && clip.Duration - candidate.Time < settings.MinimumRemainingTime) {
						// Frame times increase, so the rest of this one-shot is also ineligible.
						break;
					}
					// Keep playing a stop, but do not enter another stop's already stationary tail.
					if (!clip.Loop && frame != continuationFrame && stationary && IsStationary(ref database, frame, speed)) {
						continue;
					}
					// Nearby frames of the current clip repeat or skip the same step instead of continuing it.
					if (frame != continuationFrame && continuationFrame >= 0 && continuationFrame < database.Frames.Length && settings.MinimumSwitchTime > 0f) {
						ref var continuation = ref database.Frames[continuationFrame];
						if (candidate.ClipIndex == continuation.ClipIndex) {
							float distance = math.abs(candidate.Time - continuation.Time);
							if (clip.Loop) {
								distance = math.min(distance, clip.Duration - distance);
							}
							if (distance < settings.MinimumSwitchTime) {
								continue;
							}
						}
					}
					float cost = Cost(ref database, query, settings, frame, speed, frame == continuationFrame ? continuationAmount : 0f,
						frame == continuationFrame ? 0f : settings.SwitchPenalty, requestedDirection, scoreDirection, bestCost);
					if (math.isfinite(cost) && (cost < bestCost || cost == bestCost && frame < best)) {
						bestCost = cost;
						best = frame;
					}
				}
			}
			return best;
		}

		private static float Cost(ref MotionDatabase database, in DynamicBuffer<MmdSearchQueryElement> query,
			in MmdSearchSettings settings, int frame, float speed, float amount, float penalty,
			float2 requestedDirection, bool scoreDirection, float limit) {
			float cost = penalty;
			for (int feature = 0; feature < database.FeatureCount; feature++) {
				float difference = (query[feature].Value - ReadCandidate(ref database, frame, feature, speed, amount))
					* database.FeatureScales[feature];
				if (database.FeatureCount == LocomotionSchema.Dimension && feature < 12 && feature % 4 < 2) {
					difference *= math.max(1f, settings.TrajectoryPositionWeight);
				}
				cost += difference * difference;
				// Remaining squared errors and the optional direction penalty cannot lower this cost.
				if (cost > limit) {
					break;
				}
			}
			if (cost > limit) {
				return cost;
			}
			if (scoreDirection) {
				// Match the resolved movement path even when facing differs during a turn or wall slide.
				float2 predicted = new float2(ReadCandidate(ref database, frame, 8, speed, amount),
					ReadCandidate(ref database, frame, 9, speed, amount));
				if (math.lengthsq(predicted) > StationaryDistance * StationaryDistance) {
					cost += settings.TrajectoryDirectionWeight * math.distancesq(requestedDirection, math.normalize(predicted));
				}
			}
			return cost;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float ReadCandidate(ref MotionDatabase database, int frame, int feature, float speed, float amount) {
			float value = LocomotionSchema.ReadFeature(ref database, frame, feature, speed);
			return amount > 0f ? math.lerp(value, LocomotionSchema.ReadFeature(ref database, frame + 1, feature, speed), amount) : value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool HasEligibleLoop(ref MotionDatabase database, in DynamicBuffer<MmdMotionGroup> groups, bool stationaryOnly = true) {
			for (int index = 0; index < database.Clips.Length; index++) {
				ref MotionClip clip = ref database.Clips[index];
				if (clip.Loop && clip.ActionId == 0 && MmdMotionGroups.Matches(ref database, clip, groups) && (!stationaryOnly || IsStationary(ref database, clip.FirstFrame, 1f))) {
					return true;
				}
			}
			return false;
		}

		private static bool CanFinishOneShot(ref MotionDatabase database, int frame, float speed) {
			if (database.FeatureCount != LocomotionSchema.Dimension || frame < 0 || frame >= database.Frames.Length || speed <= 0f) {
				return false;
			}
			ref var sample = ref database.Frames[frame];
			if (database.Clips[sample.ClipIndex].Loop || !IsStationary(ref database, frame, speed)) {
				return false;
			}
			// A stop may plant one foot briefly before its recovery step. Check both feet
			// over the prediction horizon before retiring its stationary continuation.
			for (int foot = 0; foot < 2; foot++) {
				int position = 15 + foot * 6;
				float3 origin = ReadVector(ref database, frame, position, speed);
				for (int point = 0; point <= 3; point++) {
					float time = point == 0 ? sample.Time : sample.Time + LocomotionSchema.PredictionTimes[point - 1] * speed;
					int future = MmdAnimationMath.FrameAt(ref database, sample.ClipIndex, time);
					float3 velocity = ReadVector(ref database, future, position + 3, speed);
					float3 displacement = ReadVector(ref database, future, position, speed) - origin;
					if (math.lengthsq(velocity) > StationarySpeed * StationarySpeed ||
						math.lengthsq(displacement) > StationaryDistance * StationaryDistance) {
						return false;
					}
				}
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float3 ReadVector(ref MotionDatabase database, int frame, int feature, float speed) {
			return new float3(LocomotionSchema.ReadFeature(ref database, frame, feature, speed),
				LocomotionSchema.ReadFeature(ref database, frame, feature + 1, speed),
				LocomotionSchema.ReadFeature(ref database, frame, feature + 2, speed));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsStationary(in DynamicBuffer<MmdSearchQueryElement> query) {
			if (query.Length != LocomotionSchema.Dimension) {
				return false;
			}
			// Ground snapping can report vertical speed even while the requested planar path is stationary.
			var velocity = new float2(query[12].Value, query[14].Value);
			if (math.lengthsq(velocity) > StationarySpeed * StationarySpeed) {
				return false;
			}
			for (int point = 0; point < 3; point++) {
				int offset = point * 4;
				if (!IsStationaryPoint(query[offset].Value, query[offset + 1].Value, query[offset + 2].Value, query[offset + 3].Value)) {
					return false;
				}
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsStationary(ref MotionDatabase database, int frame, float speed) {
			var velocity = new float3(LocomotionSchema.ReadFeature(ref database, frame, 12, speed),
				LocomotionSchema.ReadFeature(ref database, frame, 13, speed), LocomotionSchema.ReadFeature(ref database, frame, 14, speed));
			if (math.lengthsq(velocity) > StationarySpeed * StationarySpeed) {
				return false;
			}
			for (int point = 0; point < 3; point++) {
				int offset = point * 4;
				if (!IsStationaryPoint(LocomotionSchema.ReadFeature(ref database, frame, offset, speed),
					LocomotionSchema.ReadFeature(ref database, frame, offset + 1, speed),
					LocomotionSchema.ReadFeature(ref database, frame, offset + 2, speed),
					LocomotionSchema.ReadFeature(ref database, frame, offset + 3, speed))) {
					return false;
				}
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsStationaryPoint(float x, float z, float facingX, float facingZ) {
			return x * x + z * z <= StationaryDistance * StationaryDistance &&
				facingX * facingX + (facingZ - 1f) * (facingZ - 1f) <= StationaryFacingDifference * StationaryFacingDifference;
		}
	}

}
