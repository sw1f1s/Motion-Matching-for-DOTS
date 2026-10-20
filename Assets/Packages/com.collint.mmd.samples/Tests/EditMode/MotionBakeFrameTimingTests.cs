using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Editor.Baking;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ColInt.MMD.Tests {
	public sealed class MotionBakeFrameTimingTests {
		[TestCase(245, 30f)]
		[TestCase(245, 60f)]
		[TestCase(60, 30f)]
		[TestCase(1, 30f)]
		[TestCase(299, 29.97f)]
		public async Task GridAlignedDurationIncludesTheEndpointExactlyOnce(int intervals, float sampleRate) {
			await BakeAndAssertTiming(intervals / sampleRate, sampleRate, intervals + 1);
		}

		[TestCase(29, 30f, -1)]
		[TestCase(29, 30f, 1)]
		[TestCase(49, 29.97f, 1)]
		[TestCase(245, 30f, -1)]
		[TestCase(245, 30f, 1)]
		public async Task DurationAdjacentToGridKeepsDistinctSamplesAndExactEndpoint(int intervals, float sampleRate, int offset) {
			float duration = math.asfloat(math.asint(intervals / sampleRate) + offset);
			await BakeAndAssertTiming(duration, sampleRate, intervals + (offset > 0 ? 2 : 1));
		}

		[TestCase(0.001f, 60f, 2)]
		[TestCase(0.02f, 60f, 3)]
		[TestCase(0.345f, 30f, 12)]
		public async Task PartialIntervalIncludesTheEndpointExactlyOnce(float duration, float sampleRate, int frameCount) {
			await BakeAndAssertTiming(duration, sampleRate, frameCount);
		}

		[Test]
		public void ValidatorRejectsDuplicateEndpointEvenWithinTimingTolerance() {
			var source = MotionDatabaseFixture.Create();
			source.Clips[0].SampleRate = 100000f;
			source.Clips[0].Duration = 0.0001f;
			for (int index = 0; index < source.Clips[0].FrameCount; index++) {
				source.Frames[index].Time = math.min(index / source.Clips[0].SampleRate, source.Clips[0].Duration);
			}
			Assert.DoesNotThrow(() => MotionDatabaseBuilder.Validate(source));
			source.Frames[source.Clips[0].FrameCount - 2].Time = source.Clips[0].Duration;
			Assert.Throws<System.ArgumentException>(() => MotionDatabaseBuilder.Validate(source));
		}

		private static async Task BakeAndAssertTiming(float duration, float sampleRate, int frameCount) {
			const string output = "Assets/MmdBakeTemporary/TestOutputs/FrameTiming.bytes";
			Assert.That(File.Exists(output), Is.False);
			var rig = new GameObject("TimingFixture");
			var root = new GameObject("Root");
			root.transform.SetParent(rig.transform, false);
			var left = new GameObject("LeftFoot");
			left.transform.SetParent(root.transform, false);
			var right = new GameObject("RightFoot");
			right.transform.SetParent(root.transform, false);
			var clip = new AnimationClip { frameRate = sampleRate };
			clip.SetCurve("Root", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, duration, duration));
			var configuration = ScriptableObject.CreateInstance<MotionBakeConfiguration>();
			configuration.Configure(rig, new[] { new MotionBakeGroup { Name = "Locomotion", Clips = new[] { clip } } }, "Root", "Root/LeftFoot", "Root/RightFoot");
			var serialized = new SerializedObject(configuration);
			serialized.FindProperty("_output").stringValue = output;
			serialized.FindProperty("_sampleRate").floatValue = sampleRate;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			try {
				await MotionDatabaseBake.BakeAsync(configuration);
				var data = MotionDatabaseCodec.Read(File.ReadAllBytes(output));
				Assert.That(data.Frames.Length, Is.EqualTo(frameCount));
				Assert.That(data.Frames[0].Time, Is.Zero);
				Assert.That(data.Frames[data.Frames.Length - 1].Time, Is.EqualTo(clip.length));
				Assert.That(data.Frames.Count(frame => frame.Time == clip.length), Is.EqualTo(1));
				for (int index = 1; index < data.Frames.Length; index++) {
					Assert.That(data.Frames[index].Time, Is.GreaterThan(data.Frames[index - 1].Time));
				}
				Assert.That(data.Frames[data.Frames.Length - 1].Root.pos.z, Is.EqualTo(duration).Within(0.00001f));
			} finally {
				AssetDatabase.DeleteAsset(output);
				AssetDatabase.DeleteAsset(Path.ChangeExtension(output, ".mmd-debug.json"));
				Object.DestroyImmediate(configuration);
				Object.DestroyImmediate(clip);
				Object.DestroyImmediate(rig);
			}
		}
	}
}
