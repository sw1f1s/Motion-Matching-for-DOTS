using System.Linq;
using ColInt.MMD.Actions.Components;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Diagnostics;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdDebugDiagnosticsTests {
		[Test]
		public void ReaderWaitsForExclusiveLoadingTransactionAndPreservesClipHistory() {
			using (var sample = new MmdSampleWorld(0.1f)) {
				using (var reader = new MmdDebugSnapshotReader()) {
					var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
					var manager = sample.World.EntityManager;
					reader.Read();
					manager.BeginExclusiveEntityTransaction();
					try {
						Assert.DoesNotThrow(reader.Read);
						Assert.That(manager.CanBeginExclusiveEntityTransaction(), Is.False, "Diagnostics must not end the loading transaction.");
					} finally {
						manager.EndExclusiveEntityTransaction();
					}
					var playback = manager.GetComponentData<MmdPlayback>(actor);
					playback.ClipIndex = 1;
					playback.Revision++;
					manager.SetComponentData(actor, playback);
					reader.Read();
					var snapshot = reader.Entities.Single(item => item.World == sample.World && item.Entity == actor);
					Assert.That(snapshot.ClipHistory.Length, Is.EqualTo(2));
					Assert.That(snapshot.HasObservedTransition, Is.True);
				}
			}
		}

		[Test]
		public void ReaderReportsSeparateSmoothingProgressAndBypassesRootForActions() {
			using (var sample = new MmdSampleWorld(0.1f)) {
				using (var reader = new MmdDebugSnapshotReader()) {
					var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
					var manager = sample.World.EntityManager;
					manager.AddComponentData(actor, new MmdInertialization { PoseDuration = 0.2f, StationaryPoseDuration = 0.4f, RootDuration = 0.2f });
					manager.AddComponentData(actor, new MmdInertializationState { Revision = 1, Elapsed = 0.1f, PoseDuration = 0.4f });
					var playback = manager.GetComponentData<MmdPlayback>(actor);
					playback.Revision = 1;
					manager.SetComponentData(actor, playback);
					reader.Read();
					var snapshot = reader.Entities.Single(item => item.World == sample.World && item.Entity == actor);
					Assert.That(snapshot.TransitionActive, Is.True);
					Assert.That(snapshot.PoseTransitionProgress, Is.EqualTo(0.25f));
					Assert.That(snapshot.RootTransitionProgress, Is.EqualTo(0.5f));
					manager.SetComponentData(actor, new MmdActionState { ActionId = 7 });
					reader.Read();
					snapshot = reader.Entities.Single(item => item.World == sample.World && item.Entity == actor);
					Assert.That(snapshot.RootTransitionDuration, Is.Zero);
					Assert.That(snapshot.RootTransitionProgress, Is.EqualTo(1f));
					manager.SetComponentData(actor, new MmdInertializationState { Revision = 1, Elapsed = 0.4f, PoseDuration = 0.4f });
					reader.Read();
					snapshot = reader.Entities.Single(item => item.World == sample.World && item.Entity == actor);
					Assert.That(snapshot.TransitionActive, Is.False);
					Assert.That(snapshot.PoseTransitionProgress, Is.EqualTo(1f));
				}
			}
		}

		[Test]
		public void ReaderIncludesDisabledAndPrefabActorsInMultipleWorldsAndDropsDisposedWorld() {
			using (var first = new MmdSampleWorld(0.1f)) {
				using (var reader = new MmdDebugSnapshotReader()) {
					var second = new MmdSampleWorld(0.1f);
					try {
						var firstActor = first.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
						var secondActor = second.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
						first.World.EntityManager.AddComponent<Disabled>(firstActor);
						second.World.EntityManager.AddComponent<Prefab>(secondActor);
						reader.Read();
						var disabled = reader.Entities.Single(item => item.World == first.World && item.Entity == firstActor);
						var prefab = reader.Entities.Single(item => item.World == second.World && item.Entity == secondActor);
						Assert.That(disabled.Disabled, Is.True);
						Assert.That(disabled.Status, Is.EqualTo("Disabled"));
						Assert.That(disabled.ValidClip, Is.True);
						Assert.That(prefab.Prefab, Is.True);
						Assert.That(prefab.Status, Is.EqualTo("Prefab"));
						Assert.That(disabled.WorldLabel, Is.Not.EqualTo(prefab.WorldLabel));
						first.World.EntityManager.DestroyEntity(firstActor);
						second.Dispose();
						second = null;
						reader.Read();
						Assert.That(reader.Entities.Any(item => item.Entity == firstActor && item.World == first.World), Is.False);
						Assert.That(reader.Entities.Any(item => item.World == prefab.World), Is.False);
						Assert.DoesNotThrow(reader.Read);
					} finally {
						if (second != null) {
							second.Dispose();
						}
					}
				}
			}
		}

		[Test]
		public void ReaderReportsInvalidPlaybackAndMissingDatabaseWithoutMutatingActor() {
			using (var sample = new MmdSampleWorld(0.1f)) {
				using (var reader = new MmdDebugSnapshotReader()) {
					var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create()), false, LocalTransform.Identity);
					var manager = sample.World.EntityManager;
					var playback = manager.GetComponentData<MmdPlayback>(actor);
					playback.ClipIndex = 999;
					playback.Time = 0.25f;
					manager.SetComponentData(actor, playback);
					reader.Read();
					var invalid = reader.Entities.Single(item => item.World == sample.World && item.Entity == actor);
					Assert.That(invalid.ValidClip, Is.False);
					Assert.That(invalid.Status, Is.EqualTo("Fault"));
					Assert.That(manager.GetComponentData<MmdPlayback>(actor).ClipIndex, Is.EqualTo(999));
					Assert.That(manager.GetComponentData<MmdPlayback>(actor).Time, Is.EqualTo(0.25f));
					manager.SetComponentData(actor, default(MmdDatabase));
					reader.Read();
					var missing = reader.Entities.Single(item => item.World == sample.World && item.Entity == actor);
					Assert.That(missing.Problem, Is.EqualTo("Database is not loaded"));
				}
			}
		}

		[Test]
		public void CatalogSignatureIsStableAcrossBlobCopiesAndDetectsChangedGroupsAndSampledPoses() {
			var source = MotionDatabaseFixture.Create();
			using (var first = MotionDatabaseBuilder.Build(source)) {
				using (var copy = MotionDatabaseBuilder.Build(source)) {
					string expected = MmdDebugCatalogStore.Fingerprint(ref first.Value);
					Assert.That(MmdDebugCatalogStore.Fingerprint(ref copy.Value), Is.EqualTo(expected));
					var pose = source.Poses[0];
					pose.Position.x += 1f;
					source.Poses[0] = pose;
					using (var changed = MotionDatabaseBuilder.Build(source)) {
						Assert.That(MmdDebugCatalogStore.Fingerprint(ref changed.Value), Is.Not.EqualTo(expected));
					}
					source.Poses[0] = ColInt.MMD.Playback.Helpers.MmdAnimationMath.PoseSample(ref first.Value, 0, 0);
					source.Groups = new[] { new MotionGroup { Name = new Unity.Collections.FixedString64Bytes("Idle"), Tags = 1 } };
					using (var renamed = MotionDatabaseBuilder.Build(source)) {
						Assert.That(MmdDebugCatalogStore.Fingerprint(ref renamed.Value), Is.Not.EqualTo(expected));
					}
				}
			}
		}
	}
}
