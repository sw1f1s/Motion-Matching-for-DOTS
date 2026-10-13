using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Baking;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class AuthoredRootMotionBakeTests {
		[TestCase("M_Neutral_Walk_Reface_Start_F_R_090")]
		[TestCase("M_Neutral_Walk_Reface_Start_B_L_180")]
		public async Task RefaceBakeUsesOneInitialHeadingForPoseAndRoot(string clipName) {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/RefaceHeadingBake.bytes";
			Assert.That(File.Exists(output), Is.False);
			var sample = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleBakeConfiguration.asset"));
			var idle = sample.Groups.SelectMany(g => g.Clips).First(c => c.name == "M_Neutral_Stand_Idle_Loop");
			var reface = sample.Groups.SelectMany(g => g.Clips).First(c => c.name == clipName);
			var configuration = UnityEngine.Object.Instantiate(sample);
			configuration.Configure(sample.Rig, new[] { new MotionBakeGroup { Name = "Locomotion", Clips = new[] { idle, reface } } },
				sample.RootMotionPath, sample.LeftFootPath, sample.RightFootPath);
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_normalizationGroups").arraySize = 0;
			serialized.FindProperty("_output").stringValue = output;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				var source = MotionDatabaseCodec.Read(File.ReadAllBytes(output));
				int hips = Array.IndexOf(source.BoneNameHashes, BonePathHash.Calculate("root/pelvis"));
				quaternion reference = ModelRotation(source, 0, hips);
				quaternion start = ModelRotation(source, source.Clips[1].FirstFrame, hips);
				Assert.That(MmdRotationPacking.ErrorDegrees(reference, start), Is.LessThan(0.01f),
					"Initial authored yaw must not remain as a 90/180 degree skeletal offset.");
				var instance = UnityEngine.Object.Instantiate(sample.Rig);
				try {
					using (var authored = MotionSourceRoot.Create(reface, sample.SourceRootMotionPath, instance.GetComponent<Animator>())) {
						RigidTransform expected = authored.Sample(reface.length);
						RigidTransform baked = source.Frames.Last().Root;
						Assert.That(math.distance(expected.pos, baked.pos), Is.LessThan(0.00001f));
						Assert.That(MmdRotationPacking.ErrorDegrees(expected.rot, baked.rot), Is.LessThan(0.001f));
						Assert.That(math.abs(math.degrees(math.atan2(expected.pos.x, expected.pos.z))),
							Is.EqualTo(clipName.EndsWith("090") ? 90f : 0f).Within(1f));
					}
				} finally {
					UnityEngine.Object.DestroyImmediate(instance);
				}
			} finally {
				AssetDatabase.DeleteAsset(output);
				AssetDatabase.DeleteAsset(Path.ChangeExtension(output, "mmd-debug.json"));
				UnityEngine.Object.DestroyImmediate(configuration);
			}
		}

		[TestCase("M_Neutral_Traversal_Catch_Mantle_low_stand")]
		[TestCase("M_Neutral_Traversal_Catch_Vault_med")]
		[TestCase("M_Neutral_Traversal_Catch_Hurdle_high_walk")]
		[TestCase("M_Neutral_Traversal_Catch_Cliff_high_stand")]
		public async Task TrimmedCatchBakeRemovesHumanoidBodyOriginOffset(string clipName) {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/CatchOriginBake.bytes";
			var sample = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleBakeConfiguration.asset"));
			var clip = sample.Groups.SelectMany(g => g.Clips).First(c => c.name == clipName);
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			configuration.Configure(sample.Rig, new[] { new MotionBakeGroup { Name = "Catch", Playback = MotionBakeGroupPlayback.Action, Clips = new[] { clip } } },
				sample.RootMotionPath, sample.LeftFootPath, sample.RightFootPath);
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_sourceRootMotionPath").stringValue = sample.SourceRootMotionPath;
			serialized.FindProperty("_output").stringValue = output;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				using (var database = MotionDatabaseCodec.Load(File.ReadAllBytes(output))) {
					for (int frame = 0; frame < database.Value.Frames.Length; frame++) {
						var time = database.Value.Frames[frame].Time;
						var model = float4x4.identity;
						for (int bone = database.Value.LeftFootBone; bone >= 0; bone = database.Value.Parents[bone]) {
							var pose = ColInt.MMD.Playback.Helpers.MmdAnimationMath.PoseAt(ref database.Value, 0, time, bone);
							model = math.mul(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), model);
						}
						Assert.That(model.c3.y, Is.InRange(-1.5f, 1.5f), "Trimmed source world height must not survive as a skeletal origin offset.");
					}
				}
			} finally {
				AssetDatabase.DeleteAsset(output);
				AssetDatabase.DeleteAsset(Path.ChangeExtension(output, "mmd-debug.json"));
				UnityEngine.Object.DestroyImmediate(configuration);
			}
		}

		[Test]
		public async Task StraightUeRunKeepsAuthoredHeadingAndBodyHeightInPose() {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/AuthoredRootBake.bytes";
			Assert.That(File.Exists(output), Is.False);
			var sample = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleBakeConfiguration.asset"));
			var run = sample.Groups.SelectMany(g => g.Clips).First(c => c.name == "M_Neutral_Run_Loop_F");
			var before = AssetDatabase.GetSubFolders("Assets/MmdBakeTemporary");
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			configuration.Configure(sample.Rig, new[] { new MotionBakeGroup { Name = "Locomotion", Clips = new[] { run } } }, sample.RootMotionPath, sample.LeftFootPath, sample.RightFootPath);
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_sourceRootMotionPath").stringValue = "root";
			serialized.FindProperty("_normalizationGroups").arraySize = 0;
			serialized.FindProperty("_output").stringValue = output;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				var source = MotionDatabaseCodec.Read(File.ReadAllBytes(output));
				foreach (var frame in source.Frames) {
					Assert.That(math.length(frame.Root.pos.xy), Is.LessThan(0.001f), "A straight authored run must not acquire body sway or vertical root motion.");
					Assert.That(math.abs(frame.Root.rot.value.w), Is.GreaterThan(0.99999f), "Humanoid body twisting must stay in the pose.");
				}
				var target = sample.Rig.GetComponent<Animator>();
				var sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>(MmdSampleTestAssets.GetPath("Creatures/Mannequin/Mannequin.fbx"));
				float expectedDistance = run.length * 5f * target.humanScale / sourceRig.GetComponent<Animator>().humanScale;
				Assert.That(source.Frames.Last().Root.pos.z, Is.EqualTo(expectedDistance).Within(0.001f));
				string hipsPath = AnimationUtility.CalculateTransformPath(target.GetBoneTransform(HumanBodyBones.Hips), sample.Rig.transform);
				int hips = Array.IndexOf(source.BoneNameHashes, BonePathHash.Calculate(hipsPath));
				float minimum = float.PositiveInfinity;
				float maximum = float.NegativeInfinity;
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					float4x4 model = float4x4.identity;
					for (int bone = hips; bone >= 0; bone = source.Parents[bone]) {
						JointPose pose = source.Poses[frame * source.Parents.Length + bone];
						model = math.mul(float4x4.TRS(pose.Position, pose.Rotation, pose.Scale), model);
					}
					Assert.That(math.length(model.c3.xz), Is.LessThan(0.35f));
					minimum = math.min(minimum, model.c3.y);
					maximum = math.max(maximum, model.c3.y);
				}
				Assert.That(maximum - minimum, Is.GreaterThan(0.02f));
				Assert.That(AssetDatabase.GetSubFolders("Assets/MmdBakeTemporary"), Is.EquivalentTo(before));
				Assert.That(sample.Rig.GetComponent<Animator>().enabled, Is.False);
			} finally {
				AssetDatabase.DeleteAsset(output);
				UnityEngine.Object.DestroyImmediate(configuration);
			}
		}

		private static quaternion ModelRotation(MotionDatabaseSource source, int frame, int bone) {
			quaternion rotation = quaternion.identity;
			for (int current = bone; current >= 0; current = source.Parents[current]) {
				rotation = math.mul(source.Poses[frame * source.Parents.Length + current].Rotation, rotation);
			}
			return rotation;
		}
	}
}
