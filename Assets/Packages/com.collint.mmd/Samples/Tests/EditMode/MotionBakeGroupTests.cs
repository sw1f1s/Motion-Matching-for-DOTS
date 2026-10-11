using System.Linq;
using System;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Baking;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MotionBakeGroupTests {
		[Test]
		public void SharedClipIsBakedOnceWithBothNamedGroupsAndUsesClipLoopSetting() {
			AnimationClip clip = CreateClip();
			try {
				var groups = new[] {
					new MotionBakeGroup { Name = "Run", Clips = new[] { clip } },
					new MotionBakeGroup { Name = "Strafe", Clips = new[] { clip } }
				};
				var result = MotionBakeGroupResolver.Resolve(groups);
				Assert.That(result.Clips.Length, Is.EqualTo(1));
				Assert.That(result.Clips[0].Tags, Is.EqualTo(3u));
				Assert.That(result.Clips[0].Loop, Is.False);
				var settings = AnimationUtility.GetAnimationClipSettings(clip);
				settings.loopTime = true;
				AnimationUtility.SetAnimationClipSettings(clip, settings);
				Assert.That(MotionBakeGroupResolver.Resolve(groups).Clips[0].Loop, Is.True);
			} finally {
				UnityEngine.Object.DestroyImmediate(clip);
			}
		}

		[TestCase("")]
		[TestCase(" Run ")]
		[TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJ")]
		public void InvalidGroupNameIsRejected(string name) {
			Assert.Throws<ArgumentException>(() => MotionBakeGroupResolver.Resolve(new[] { new MotionBakeGroup { Name = name } }));
		}

		[Test]
		public void DuplicateNamesAndMoreThan32GroupsAreRejected() {
			Assert.Throws<ArgumentException>(() => MotionBakeGroupResolver.Resolve(new[] { new MotionBakeGroup { Name = "Run" }, new MotionBakeGroup { Name = "Run" } }));
			var groups = Enumerable.Range(0, 33).Select(i => new MotionBakeGroup { Name = "Group " + i }).ToArray();
			Assert.Throws<ArgumentException>(() => MotionBakeGroupResolver.Resolve(groups));
			Assert.That(MotionBakeGroupResolver.Resolve(groups.Take(32).ToArray()).Groups.Last().Tags, Is.EqualTo(0x80000000u));
		}

		[Test]
		public void MissingClipIsRejectedWithItsGroupName() {
			var error = Assert.Throws<ArgumentException>(() => MotionBakeGroupResolver.Resolve(new[] {
				new MotionBakeGroup { Name = "Walk", Clips = new AnimationClip[] { null } }
			}));
			Assert.That(error.Message, Does.Contain("Walk"));
		}

		[Test]
		public void ActionsReceiveAutomaticIdsAndRejectLoopsOrSharedPlaybackModes() {
			AnimationClip clip = CreateClip();
			try {
				var action = new MotionBakeGroup { Name = "Vault", Playback = MotionBakeGroupPlayback.Action, Clips = new[] { clip } };
				var result = MotionBakeGroupResolver.Resolve(new[] { action });
				Assert.That(result.Clips[0].ActionId, Is.EqualTo(result.Groups[0].ActionId).And.Not.Zero);
				Assert.Throws<ArgumentException>(() => MotionBakeGroupResolver.Resolve(new[] { action, new MotionBakeGroup { Name = "Run", Clips = new[] { clip } } }));
				var settings = AnimationUtility.GetAnimationClipSettings(clip);
				settings.loopTime = true;
				AnimationUtility.SetAnimationClipSettings(clip, settings);
				Assert.Throws<ArgumentException>(() => MotionBakeGroupResolver.Resolve(new[] { action }));
			} finally {
				UnityEngine.Object.DestroyImmediate(clip);
			}
		}

		[Test]
		public void ReorderedGroupsAreResolvedByNameInsteadOfAnOldMask() {
			var source = MotionDatabaseFixture.Create();
			source.Groups = MotionBakeGroupResolver.Resolve(new[] { new MotionBakeGroup { Name = "Run" }, new MotionBakeGroup { Name = "Idle" } }).Groups;
			var database = MotionDatabaseBuilder.Build(source);
			try {
				Assert.That(MotionDatabaseGroups.GetTags(ref database.Value, new FixedString64Bytes("Idle")), Is.EqualTo(2u));
				Assert.That(MotionDatabaseGroups.GetTags(ref database.Value, new FixedString64Bytes("Run")), Is.EqualTo(1u));
				Assert.Throws<ArgumentException>(() => MotionDatabaseGroups.GetTags(ref database.Value, new FixedString64Bytes("Missing")));
				Assert.Throws<ArgumentException>(() => MotionDatabaseGroups.GetActionId(ref database.Value, new FixedString64Bytes("Run")));
			} finally {
				database.Dispose();
			}
		}

		[Test]
		public void AddingGroupPreservesExistingDataInsteadOfCloningItsClipList() {
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			AnimationClip clip = CreateClip();
			try {
				configuration.Configure(null, new[] { new MotionBakeGroup { Name = "New Group", Clips = new[] { clip } } }, "root", "left", "right");
				var serialized = new SerializedObject(configuration);
				MotionBakeConfigurationEditing.AddGroup(serialized);
				MotionBakeConfigurationEditing.AddGroup(serialized);
				Assert.That(configuration.Groups.Take(5).Select(g => g.Name), Is.EqualTo(new[] { "New Group", "New Group 2", "New Group 3" }));
				Assert.That(configuration.Groups[0].Clips, Is.EqualTo(new[] { clip }));
				Assert.That(configuration.Groups[1].Clips, Is.Empty);
				Assert.That(configuration.Groups[2].Clips, Is.Empty);
				Assert.That(MotionBakeGroupResolver.Resolve(configuration.Groups).Clips.Length, Is.EqualTo(1));
			} finally {
				UnityEngine.Object.DestroyImmediate(configuration);
				UnityEngine.Object.DestroyImmediate(clip);
			}
		}

		[Test]
		public void SampleContainsRefaceStartsAndPreservesGaitMasks() {
			var configuration = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>("Assets/Packages/com.collint.mmd/Samples/Configurations/SampleBakeConfiguration.asset");
			var selection = MotionBakeGroupResolver.Resolve(configuration.Groups);
			Assert.That(selection.Clips.Length, Is.EqualTo(507));
			Assert.That(configuration.Groups.Take(5).Select(g => g.Name), Is.EqualTo(new[] { "Idle", "Walk", "Run", "Turn", "Start" }));
			Assert.That(configuration.Groups.Take(5).Select(g => g.Clips.Length), Is.EqualTo(new[] { 41, 88, 88, 8, 58 }));
			Assert.That(selection.Clips.Count(c => c.Loop), Is.EqualTo(45));
			Assert.That(selection.Clips.Any(c => c.Clip.name.StartsWith("M_Relaxed_")), Is.False);
			Assert.That(selection.Clips.Count(c => c.ActionId != 0), Is.EqualTo(64));
			Assert.That(selection.Clips.Single(c => c.Clip.name == "M_Neutral_Jump_Loop_Fall").Loop, Is.True);
			Assert.That(selection.Clips.Count(c => c.Clip.name.Contains("Reface_Start")), Is.EqualTo(26));
			Assert.That(selection.Clips.Single(c => c.Clip.name == "M_Neutral_Walk_Reface_Start_F_R_180").Tags, Is.EqualTo(18u));
			Assert.That(selection.Clips.Where(c => c.Clip.name.Contains("Reface_Start")).All(c => !c.Loop && c.ActionId == 0), Is.True);
			Assert.That(selection.Groups.Take(5).Select(g => g.Tags), Is.EqualTo(new uint[] { 1, 2, 4, 8, 16 }));
		}

		private static AnimationClip CreateClip() {
			var clip = new AnimationClip();
			clip.SetCurve("Root", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, 1f, 1f));
			return clip;
		}
	}
}
