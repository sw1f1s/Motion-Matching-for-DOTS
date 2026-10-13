using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Baking;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public class MotionBakeTests {
		[Test]
		public async Task RealUeStopHasNoSourceSceneOffsetInHumanoidPose() {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/UeStopBake.bytes";
			var sample = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleBakeConfiguration.asset"));
			var stop = sample.Groups.SelectMany(g => g.Clips).First(c => c.name == "M_Neutral_Run_Stop_F_Lfoot");
			var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(stop));
			Assert.That(importer.clipAnimations.All(c => !c.keepOriginalPositionXZ && !c.lockRootPositionXZ), Is.True);
			Assert.That(File.Exists(output), Is.False);
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			configuration.Configure(sample.Rig, new[] { new MotionBakeGroup { Name = "Locomotion", Clips = new[] { stop } } }, sample.RootMotionPath, sample.LeftFootPath, sample.RightFootPath);
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_output").stringValue = output;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				var source = MotionDatabaseCodec.Read(File.ReadAllBytes(output));
				int hips = System.Array.IndexOf(source.BoneNameHashes, BonePathHash.Calculate(sample.RootMotionPath));
				Assert.That(hips, Is.GreaterThanOrEqualTo(0));
				for (int frame = 0; frame < source.Frames.Length; frame++) {
					var pose = source.Poses[frame * source.Parents.Length + hips];
					Assert.That(Unity.Mathematics.math.length(pose.Position.xz), Is.LessThan(0.3f), "The stop pose must remain near the actor instead of retaining its source scene offset.");
				}
				Assert.That(source.Frames[source.Frames.Length - 1].Root.pos.z, Is.GreaterThan(3f), "Root motion must remain available to ECS.");
				Assert.That(sample.Rig.GetComponent<Animator>().enabled, Is.False);
			} finally {
				AssetDatabase.DeleteAsset(output);
				Object.DestroyImmediate(configuration);
			}
		}

		[Test]
		public async Task GenericClipBakesRootMotionAndStripsItFromPose() {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/GenericBake.bytes";
			Assert.That(File.Exists(output), Is.False, "The fixture output is owned by this test and must not pre-exist.");
			var rig = new GameObject("Bake Fixture");
			var root = new GameObject("Root");
			root.transform.SetParent(rig.transform, false);
			var left = new GameObject("LeftFoot");
			left.transform.SetParent(root.transform, false);
			left.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
			var right = new GameObject("RightFoot");
			right.transform.SetParent(root.transform, false);
			right.transform.localPosition = new Vector3(0.2f, 0f, 0f);
			var clip = new AnimationClip();
			clip.SetCurve("Root", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, 1f, 1f));
			var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
			clipSettings.loopTime = true;
			AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			configuration.Configure(rig, new[] { new MotionBakeGroup { Name = "Locomotion", Clips = new[] { clip } } }, "Root", "Root/LeftFoot", "Root/RightFoot");
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_output").stringValue = output;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				MotionDatabaseSource data = MotionDatabaseCodec.Read(File.ReadAllBytes(output));
				Assert.That(data.Frames[data.Frames.Length - 1].Root.pos.z, Is.EqualTo(1f).Within(0.00001f));
				Assert.That(data.Poses[data.Poses.Length - 3].Position.z, Is.Zero.Within(0.00001f));
				Assert.That(data.Features[14], Is.EqualTo(1f).Within(0.0001f));
				Assert.That(data.FeatureScales.Length, Is.EqualTo(27));
				Assert.That(data.Clips[0].Loop, Is.True, "Baking must read Loop Time from the AnimationClip itself.");
				Assert.That(data.Groups[0].Name.ToString(), Is.EqualTo("Locomotion"));
			} finally {
				AssetDatabase.DeleteAsset(output);
				Object.DestroyImmediate(configuration);
				Object.DestroyImmediate(clip);
				Object.DestroyImmediate(rig);
			}
		}
		[Test]
		public async Task HumanoidRigBakesToDataUsableWithoutAnimator() {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/HumanoidBake.bytes";
			string prefabPath = MmdSampleTestAssets.GetPath("Prefabs/SampleCharacter.prefab");
			Assert.That(File.Exists(output), Is.False);
			var rig = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			Assert.That(rig, Is.Not.Null);
			Assert.That(rig.GetComponent<Animator>().enabled, Is.False);
			var sample = AssetDatabase.LoadAssetAtPath<MotionBakeConfiguration>(MmdSampleTestAssets.GetPath("Configurations/SampleBakeConfiguration.asset"));
			var clip = sample.Groups.SelectMany(g => g.Clips).First(c => c.name == "M_Neutral_Run_Loop_F");
			Assert.That(clip.isHumanMotion, Is.True);
			var animator = rig.GetComponent<Animator>();
			Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
			Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			configuration.Configure(rig, new[] { new MotionBakeGroup { Name = "Locomotion", Clips = new[] { clip } } }, string.Empty,
				AnimationUtility.CalculateTransformPath(left, rig.transform), AnimationUtility.CalculateTransformPath(right, rig.transform));
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_output").stringValue = output;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				Assert.That(rig.GetComponent<Animator>().enabled, Is.False, "Offline baking must not enable the shared prefab Animator.");
				var database = MotionDatabaseBuilder.Build(MotionDatabaseCodec.Read(File.ReadAllBytes(output)));
				using (var world = new MmdSampleWorld(1f / 60f)) {
					var entity = world.Characters.Create(database, true, LocalTransform.Identity);
					world.Tick();
					Assert.That(world.World.EntityManager.GetBuffer<MmdLocalPose>(entity).Length, Is.GreaterThanOrEqualTo(52));
				}
			} finally {
				AssetDatabase.DeleteAsset(output);
				Object.DestroyImmediate(configuration);
			}
		}

	}

}
