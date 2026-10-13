using System;
using System.Linq;
using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Data;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Collections;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdMotionGroupTests {
		[TestCase(33)]
		[TestCase(63)]
		[TestCase(64)]
		[TestCase(65)]
		[TestCase(127)]
		[TestCase(128)]
		[TestCase(129)]
		[TestCase(1000)]
		public void HighGroupIdsRoundTripAndSelectOnlyTheirOwnClip(int id) {
			var source = Source(id);
			source.Clips[1].Groups = new[] { id };
			var restored = MotionDatabaseCodec.Read(MotionDatabaseCodec.Write(source));
			Assert.That(restored.Groups.Length, Is.EqualTo(id));
			Assert.That(restored.Clips[1].Groups, Is.EqualTo(new[] { id }));
			using (var sample = new MmdSampleWorld(0.1f)) {
				var manager = sample.World.EntityManager;
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(restored), false, LocalTransform.Identity);
				var database = manager.GetComponentData<MmdDatabase>(actor).Value;
				var groups = manager.GetBuffer<MmdMotionGroup>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, id);
				int selected = MotionSearch.Find(ref database.Value, query, default, groups, -1);
				Assert.That(database.Value.Frames[selected].ClipIndex, Is.EqualTo(1));
				sample.Tick();
				Assert.That(manager.GetComponentData<ColInt.MMD.Playback.Components.MmdPlayback>(actor).ClipIndex, Is.EqualTo(1));
				groups = manager.GetBuffer<MmdMotionGroup>(actor);
				query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				MmdMotionGroups.Add(groups, id, MmdMotionGroupUsage.Excluded);
				Assert.That(MotionSearch.Find(ref database.Value, query, default, groups, -1), Is.EqualTo(-1));
			}
		}

		[Test]
		public void ThousandMembershipsAndRequiredGroupsUseDynamicStorageAndKeepOtherUses() {
			var source = Source(1000);
			source.Clips[1].Groups = Enumerable.Range(1, 1000).ToArray();
			var restored = MotionDatabaseCodec.Read(MotionDatabaseCodec.Write(source));
			Assert.That(restored.Clips[1].Groups, Is.EqualTo(source.Clips[1].Groups));
			using (var sample = new MmdSampleWorld(0.1f)) {
				var manager = sample.World.EntityManager;
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(restored), false, LocalTransform.Identity);
				var database = manager.GetComponentData<MmdDatabase>(actor).Value;
				var groups = manager.GetBuffer<MmdMotionGroup>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				MmdMotionGroups.Add(groups, 1000, MmdMotionGroupUsage.StationaryPose);
				using (var words = new NativeArray<ulong>(16, Allocator.Temp)) {
					var mask = new BitMask(words, 1000);
					mask.Not();
					MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, mask);
					MmdMotionGroups.Add(groups, mask, MmdMotionGroupUsage.Excluded);
					Assert.That(MotionSearch.Find(ref database.Value, query, default, groups, -1), Is.EqualTo(-1));
					MmdMotionGroups.Remove(groups, mask, MmdMotionGroupUsage.Excluded);
				}
				Assert.That(groups.Length, Is.EqualTo(16));
				int selected = MotionSearch.Find(ref database.Value, query, default, groups, -1);
				Assert.That(database.Value.Frames[selected].ClipIndex, Is.EqualTo(1));
				MmdMotionGroups.Set(groups, MmdMotionGroupUsage.Required, 999, 1000);
				Assert.That(MmdMotionGroups.Is(groups, MmdMotionGroupUsage.Required, 999, 1000), Is.True);
				Assert.That(MmdMotionGroups.Any(ref database.Value, database.Value.Clips[1], groups, MmdMotionGroupUsage.StationaryPose), Is.True);
				MmdMotionGroups.Add(groups, 999, MmdMotionGroupUsage.Excluded);
				Assert.That(MotionSearch.Find(ref database.Value, query, default, groups, -1), Is.EqualTo(-1));
				MmdMotionGroups.Remove(groups, 999, MmdMotionGroupUsage.Excluded);
				Assert.That(MotionSearch.Find(ref database.Value, query, default, groups, -1), Is.GreaterThanOrEqualTo(0));
			}
		}

		[Test]
		public void InvalidMembershipsAreRejectedInsteadOfAliasingOtherGroups() {
			var source = Source(1000);
			source.Clips[1].Groups = new[] { 1001 };
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
			source.Clips[1].Groups = new[] { 1000, 1000 };
			Assert.Throws<ArgumentException>(() => MotionDatabaseCodec.Write(source));
			source.Clips[1].Groups = new[] { 1000, 999 };
			Assert.Throws<ArgumentException>(() => MotionDatabaseBuilder.Build(source));
		}

		private static MotionDatabaseSource Source(int count) {
			var source = MotionDatabaseFixture.Create();
			source.Groups = Enumerable.Range(1, count).Select(id => new MotionGroup { Name = new FixedString64Bytes("Group " + id), Id = id }).ToArray();
			return source;
		}
	}
}
