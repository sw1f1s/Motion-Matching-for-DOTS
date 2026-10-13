using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Lifecycle.Data;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdSearchTests {
		[Test]
		public void EqualCostsPreferTheEarliestFrameEvenWhenContinuationIsEvaluatedFirst() {
			int requiredGroup = 0;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			for (int frame = 11; frame < 22; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = source.Features[feature];
				}
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var database = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, float3.zero, 0f);
				Assert.That(MotionSearch.Find(ref database.Value.Value, query, default, SelectGroups(manager, actor, requiredGroup, excludedGroup), 16), Is.Zero);
			}
		}

		[TestCase("RequiredGroups")]
		[TestCase("ExcludedGroups")]
		[TestCase("Action")]
		[TestCase("Endpoint")]
		[TestCase("RemainingTime")]
		[TestCase("PreferLoop")]
		public void AnIneligibleContinuationCannotPruneAnEligibleMoreExpensiveClip(string rejection) {
			int requiredGroup = 0;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			var continuation = source.Clips[0];
			var eligible = source.Clips[1];
			eligible.Groups = new[] { 2 };
			var settings = new MmdSearchSettings { MinimumRemainingTime = 0.2f };
			int frameIndex = 5;
			if (rejection == "RequiredGroups") {
				requiredGroup = 2;
			} else if (rejection == "ExcludedGroups") {
				excludedGroup = 1;
			} else if (rejection == "Action") {
				continuation.ActionId = 3;
				continuation.Loop = false;
			} else if (rejection == "Endpoint") {
				frameIndex = 10;
			} else if (rejection == "RemainingTime") {
				continuation.Loop = false;
				frameIndex = 9;
			} else if (rejection == "PreferLoop") {
				continuation.Loop = false;
				settings.PreferLoop = true;
			}
			source.Clips[0] = continuation;
			source.Clips[1] = eligible;
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = 0f;
				}
				// Only the rejected continuation is a perfect match; the valid clip has a distinct foot pose.
				source.Features[frame * source.FeatureCount + 15] = frame == frameIndex ? 0f : 3f;
				if (source.Frames[frame].ClipIndex == 1) {
					source.Features[frame * source.FeatureCount + 15] = 1f;
				}
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var database = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				for (int feature = 0; feature < query.Length; feature++) {
					query[feature] = default;
				}
				// A moving request avoids the separate idle-tail policy.
				query[14] = new MmdSearchQueryElement { Value = 1f };
				int selected = MotionSearch.Find(ref database.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), frameIndex);
				Assert.That(source.Frames[selected].ClipIndex, Is.EqualTo(1));
			}
		}

		[TestCase(0f, 0)]
		[TestCase(1f, 0)]
		[TestCase(8f, 1)]
		public void PositionWeightCanPrioritizeBrakingOverAnIdenticalMovingFootPose(float weight, int expectedClip) {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			var other = source.Clips[1];
			other.Groups = new[] { 1 };
			source.Clips[1] = other;
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				bool braking = source.Frames[frame].ClipIndex == 1;
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = 0f;
				}
				for (int point = 0; point < 3; point++) {
					int offset = frame * source.FeatureCount + point * 4;
					source.Features[offset + 1] = braking ? 0.225f : 2.25f * LocomotionSchema.PredictionTimes[point];
					source.Features[offset + 3] = 1f;
				}
				source.Features[frame * source.FeatureCount + 14] = 2.25f;
				source.Features[frame * source.FeatureCount + 15] = braking ? 1f : 0f;
				source.Features[frame * source.FeatureCount + 21] = braking ? 1f : 0f;
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var database = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 2.25f), 0f);
				for (int point = 0; point < 3; point++) {
					query[point * 4 + 1] = new MmdSearchQueryElement { Value = 0.225f };
				}
				var settings = new MmdSearchSettings { TrajectoryPositionWeight = weight };
				int selected = MotionSearch.Find(ref database.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), -1);
				Assert.That(source.Frames[selected].ClipIndex, Is.EqualTo(expectedClip));
			}
		}

		[TestCase(false, true, 1)]
		[TestCase(true, true, 0)]
		[TestCase(true, false, 1)]
		public void LoopPreferenceSkipsTheMatchingOneShotButFallsBackWhenNoLoopIsEligible(bool preferLoop, bool loopEligible, int expectedClip) {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			var loop = source.Clips[0];
			loop.Groups = new[] { loopEligible ? 1 : 8 };
			source.Clips[0] = loop;
			var transition = source.Clips[1];
			transition.Groups = new[] { 1 };
			transition.Loop = false;
			source.Clips[1] = transition;
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = 0f;
				}
				source.Features[frame * source.FeatureCount + 15] = source.Frames[frame].ClipIndex == 0 ? 2f : 0f;
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var database = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 5f), 0f);
				query[15] = default;
				var settings = new MmdSearchSettings { PreferLoop = preferLoop, MinimumRemainingTime = 0.2f };
				int selected = MotionSearch.Find(ref database.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), -1);
				Assert.That(source.Frames[selected].ClipIndex, Is.EqualTo(expectedClip));
			}
		}

		[Test]
		public void InterpolatedContinuationDoesNotLoseItsFootPhaseToAnotherClipAtHalfASample() {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			var other = source.Clips[1];
			other.Groups = new[] { 1 };
			source.Clips[1] = other;
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = 0f;
				}
			}
			source.Features[5 * source.FeatureCount + 15] = 2f;
			source.Features[11 * source.FeatureCount + 15] = 1f;
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				query[15] = new MmdSearchQueryElement { Value = 1f };
				var settings = new MmdSearchSettings { SwitchPenalty = 0.1f, MinimumSwitchTime = 0.2f };
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4), Is.EqualTo(11));
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4, 1f, 0.45f), Is.EqualTo(4));
			}
		}

		[Test]
		public void TransitionHoldStillAcceptsInitialSearchAndGroupChangesImmediately() {
			int requiredGroup = 0;
			int excludedGroup = 0;
			using (var sample = new MmdSampleWorld(0.05f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var settings = manager.GetComponentData<MmdSearchSettings>(actor);
				requiredGroup = 2;
				settings.MinimumTransitionTime = 0.2f;
				SelectGroups(manager, actor, requiredGroup, excludedGroup);
				manager.SetComponentData(actor, settings);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdPlayback>(actor).ClipIndex, Is.EqualTo(1));
				uint revision = manager.GetComponentData<MmdPlayback>(actor).Revision;
				var playback = manager.GetComponentData<MmdPlayback>(actor);
				playback.SearchCountdown = 0f;
				manager.SetComponentData(actor, playback);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdPlayback>(actor).Revision, Is.EqualTo(revision));
				requiredGroup = 1;
				SelectGroups(manager, actor, requiredGroup, excludedGroup);
				manager.SetComponentData(actor, settings);
				playback = manager.GetComponentData<MmdPlayback>(actor);
				playback.SearchCountdown = 0f;
				manager.SetComponentData(actor, playback);
				sample.Tick();
				Assert.That(manager.GetComponentData<MmdPlayback>(actor).ClipIndex, Is.Zero);
			}
		}

		[Test]
		public void DirectionWeightPreservesRequestedPathEvenWhenFacingDiffers() {
			int requiredGroup = 0;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			for (int frame = 0; frame < 22; frame++) {
				bool lateral = frame < 11;
				float angle = lateral ? math.radians(20f) : 0f;
				for (int point = 0; point < 3; point++) {
					float time = LocomotionSchema.PredictionTimes[point];
					int offset = frame * source.FeatureCount + point * 4;
					source.Features[offset] = math.sin(angle) * time;
					source.Features[offset + 1] = math.cos(angle) * time;
					source.Features[offset + 2] = 0f;
					source.Features[offset + 3] = 1f;
				}
				int first = frame * source.FeatureCount;
				source.Features[first + 12] = math.sin(angle);
				source.Features[first + 14] = math.cos(angle);
				source.Features[first + 15] = lateral ? 0f : 0.4f;
				source.Features[first + 21] = lateral ? 0f : 0.4f;
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 1f), 0f);
				var settings = new MmdSearchSettings { SwitchPenalty = 0.1f };
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4), Is.EqualTo(4));
				settings.TrajectoryDirectionWeight = 4f;
				int selected = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4);
				Assert.That(source.Frames[selected].ClipIndex, Is.EqualTo(1));
				query[10] = new MmdSearchQueryElement { Value = 1f };
				query[11] = new MmdSearchQueryElement { Value = 0f };
				selected = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4);
				Assert.That(source.Frames[selected].ClipIndex, Is.EqualTo(1),
					"Turning and wall sliding must still match the resolved movement path independently of facing.");
			}
		}

		[TestCase(false, false, true)]
		[TestCase(true, false, false)]
		[TestCase(false, true, false)]
		public void SettledOneShotYieldsToIdleButRetainsAnUnfinishedRecoveryStep(bool movingFoot, bool futureStep, bool finish) {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			for (int clip = 0; clip < source.Clips.Length; clip++) {
				var value = source.Clips[clip];
				value.Loop = clip == 2;
				value.ActionId = 0;
				value.Groups = clip == 2 ? new[] { 1, 4 } : new[] { 1 };
				source.Clips[clip] = value;
			}
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = feature < 12 && feature % 4 == 3 ? 1f : 0f;
				}
				if (source.Frames[frame].ClipIndex == 2) {
					source.Features[frame * source.FeatureCount + 15] = 0.01f;
				}
			}
			if (movingFoot) {
				source.Features[4 * source.FeatureCount + 18] = 1f;
			}
			if (futureStep) {
				source.Features[8 * source.FeatureCount + 15] = 0.1f;
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, float3.zero, 0f);
				var settings = new MmdSearchSettings { SwitchPenalty = 100f, MinimumRemainingTime = 0.2f };
				int frame = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4);
				Assert.That(source.Frames[frame].ClipIndex, Is.EqualTo(finish ? 2 : 0));
				excludedGroup = 4;
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 4), Is.EqualTo(4), "A one-shot stays playable without an eligible idle loop.");
			}
		}

		[TestCase(7)]
		[TestCase(15)]
		public void StationaryStopTailCannotRestartInsteadOfEligibleLoop(int tail) {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			var stop = source.Clips[0];
			stop.Loop = false;
			source.Clips[0] = stop;
			var otherStop = source.Clips[1];
			otherStop.Loop = false;
			otherStop.Groups = new[] { 1 };
			source.Clips[1] = otherStop;
			var idle = source.Clips[2];
			idle.Loop = true;
			idle.ActionId = 0;
			idle.Groups = new[] { 1, 4 };
			source.Clips[2] = idle;
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = feature < 12 && feature % 4 == 3 ? 1f : 0f;
				}
				source.Features[frame * source.FeatureCount + 15] = 2f;
			}
			source.Features[tail * source.FeatureCount + 15] = 0f;
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				query[15] = new MmdSearchQueryElement { Value = 0f };
				LocomotionSchema.WriteTrajectory(query, float3.zero, 0f);
				var settings = new MmdSearchSettings { MinimumRemainingTime = 0.2f };
				int frame = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 9);
				Assert.That(source.Frames[frame].ClipIndex, Is.EqualTo(2));
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), tail), Is.EqualTo(tail));

				// A real new movement request can still select a non-looping transition.
				LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 1f), 0f);
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 9), Is.EqualTo(tail));
				LocomotionSchema.WriteTrajectory(query, float3.zero, 1f);
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 9), Is.EqualTo(tail));

				// A group containing only one-shots remains searchable.
				LocomotionSchema.WriteTrajectory(query, float3.zero, 0f);
				excludedGroup = 4;
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), 9), Is.EqualTo(tail));
			}
		}

		[TestCase(false, 0f)]
		[TestCase(false, -1.7f)]
		[TestCase(true, 0f)]
		[TestCase(true, -1.7f)]
		public void StationaryActionHandoffAndIdleDoNotSelectMovingStopForItsFootPose(bool leavingAction, float verticalSpeed) {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			for (int clip = 0; clip < source.Clips.Length; clip++) {
				var value = source.Clips[clip];
				value.Loop = clip == 0;
				value.ActionId = clip == 2 ? 7 : 0;
				value.Groups = new[] { 1 };
				source.Clips[clip] = value;
			}
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				for (int feature = 0; feature < source.FeatureCount; feature++) {
					source.Features[frame * source.FeatureCount + feature] = feature < 12 && feature % 4 == 3 ? 1f : 0f;
				}
				// The moving stop has the closest foot pose, but would restart movement.
				source.Features[frame * source.FeatureCount + 15] = source.Frames[frame].ClipIndex == 1 ? 0f : 10f;
				if (source.Frames[frame].ClipIndex == 1) {
					source.Features[frame * source.FeatureCount + 14] = 1f;
				}
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, float3.zero, 0f);
				query[13] = new MmdSearchQueryElement { Value = verticalSpeed };
				query[15] = default;
				var settings = new MmdSearchSettings { MinimumRemainingTime = 0.2f };
				int continuation = leavingAction ? 24 : 4;
				int selected = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), continuation);
				Assert.That(source.Frames[selected].ClipIndex, Is.Zero);
				LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 1f), 0f);
				selected = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), continuation);
				Assert.That(source.Frames[selected].ClipIndex, Is.EqualTo(1), "Renewed movement must allow a non-looping transition.");
			}
		}

		[TestCase(5, 6, -1)]
		[TestCase(9, 0, -1)]
		[TestCase(5, 6, 2)]
		public void NearbyFramesCannotRestartTheSameStepIncludingAcrossLoopSeam(int continuation, int nearby, int distant) {
			int requiredGroup = 1;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				source.Features[frame * source.FeatureCount] = 10f;
			}
			source.Features[continuation * source.FeatureCount] = 1f;
			source.Features[nearby * source.FeatureCount] = 0f;
			if (distant >= 0) {
				source.Features[distant * source.FeatureCount] = 0.5f;
			}
			using (var world = new MmdSampleWorld(0.1f)) {
				var entity = world.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(entity);
				var query = manager.GetBuffer<MmdSearchQueryElement>(entity);
				query[0] = new MmdSearchQueryElement { Value = 0f };
				var settings = new MmdSearchSettings { MinimumSwitchTime = 0.2f };
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, entity, requiredGroup, excludedGroup), continuation), Is.EqualTo(distant >= 0 ? distant : continuation));
				settings.MinimumSwitchTime = 0f;
				Assert.That(MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, entity, requiredGroup, excludedGroup), continuation), Is.EqualTo(nearby));
			}
		}

		[Test]
		public void ReducedTempoComparesCandidateTrajectoryAndVelocitiesAtActualPlaybackSpeed() {
			int requiredGroup = 2;
			int excludedGroup = 0;
			var source = MotionDatabaseFixture.Create();
			var slower = source.Clips[0];
			slower.Groups = new[] { 2 };
			source.Clips[0] = slower;
			for (int frame = 0; frame < slower.FrameCount; frame++) {
				for (int point = 0; point < 3; point++) {
					source.Features[frame * source.FeatureCount + point * 4 + 1] = 0.5f * LocomotionSchema.PredictionTimes[point];
				}
				source.Features[frame * source.FeatureCount + 14] = 0.5f;
			}
			using (var sample = new MmdSampleWorld(0.1f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				LocomotionSchema.WriteTrajectory(query, new float3(0f, 0f, 0.5f), 0f);
				var settings = new MmdSearchSettings();
				int unscaled = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), -1);
				int scaled = MotionSearch.Find(ref data.Value.Value, query, settings, SelectGroups(manager, actor, requiredGroup, excludedGroup), -1, 0.5f);
				Assert.That(source.Frames[unscaled].ClipIndex, Is.EqualTo(0));
				Assert.That(source.Frames[scaled].ClipIndex, Is.EqualTo(1));
			}
		}

		[Test]
		public void GroupsExcludeIneligibleClipAndReportNoCandidate() {
			int requiredGroup = 0;
			int excludedGroup = 0;
			using (var world = new MmdSampleWorld(0.1f)) {
				var entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				var settings = manager.GetComponentData<MmdSearchSettings>(entity);
				requiredGroup = 2;
				SelectGroups(manager, entity, requiredGroup, excludedGroup);
				manager.SetComponentData(entity, settings);
				world.Tick();
				Assert.That(manager.GetComponentData<MmdPlayback>(entity).ClipIndex, Is.EqualTo(1));
				excludedGroup = 2;
				SelectGroups(manager, entity, requiredGroup, excludedGroup);
				manager.SetComponentData(entity, settings);
				world.Tick();
				Assert.That(manager.GetComponentData<MmdFault>(entity).Code, Is.EqualTo(MmdFaultCode.NoCandidate));
			}
		}

		[Test]
		public void InvalidQueryReportsFaultAndRecoveringQueryClearsIt() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				var query = manager.GetBuffer<MmdSearchQueryElement>(entity);
				query[0] = new MmdSearchQueryElement { Value = float.NaN };
				world.Tick();
				Assert.That(manager.GetComponentData<MmdFault>(entity).Code, Is.EqualTo(MmdFaultCode.InvalidQuery));
				query = manager.GetBuffer<MmdSearchQueryElement>(entity);
				query[0] = new MmdSearchQueryElement { Value = 0f };
				world.Tick();
				Assert.That(manager.GetComponentData<MmdFault>(entity).Code, Is.EqualTo(MmdFaultCode.None));
			}
		}

		[Test]
		public void FailedActionRequestDoesNotCancelRunningAction() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.SetComponentData(entity, new MmdActionRequest { ActionId = 7, Revision = 1, PreferredClip = -1 });
				world.Tick();
				manager.SetComponentData(entity, new MmdActionRequest { ActionId = 999, Revision = 2, PreferredClip = -1 });
				world.Tick();
				Assert.That(manager.GetComponentData<MmdActionState>(entity).Rejected, Is.True);
				Assert.That(manager.GetComponentData<MmdActionState>(entity).ActionId, Is.EqualTo(7));
				Assert.That(manager.GetComponentData<MmdPlayback>(entity).Time, Is.EqualTo(0.2f).Within(0.00001f));
			}
		}

		[Test]
		public void ActionWarpReachesTargetThroughEcsPlayback() {
			using (var world = new MmdSampleWorld(0.1f)) {
				var entity = world.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), true, LocalTransform.Identity);
				var manager = world.World.EntityManager;
				manager.SetComponentData(entity, new MmdActionWarp {
					Enabled = true, RequestRevision = 1,
					TargetEnd = new RigidTransform(quaternion.identity, new float3(1f, 0.5f, 3f))
				});
				manager.SetComponentData(entity, new MmdActionRequest { ActionId = 7, Revision = 1, PreferredClip = -1 });
				for (int i = 0; i < 10; i++) {
					world.Tick();
				}
				Assert.That(math.distance(manager.GetComponentData<Unity.Transforms.LocalTransform>(entity).Position,
					new float3(1f, 0.5f, 3f)), Is.LessThan(0.0001f));
			}
		}
		private static DynamicBuffer<MmdMotionGroup> SelectGroups(EntityManager manager, Entity entity, int required, int excluded) {
			var groups = manager.GetBuffer<MmdMotionGroup>(entity);
			MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, required);
			MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Excluded, excluded);
			return groups;
		}

	}

}
