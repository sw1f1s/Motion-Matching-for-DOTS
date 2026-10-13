using ColInt.MMD.Database.Components;
using ColInt.MMD.Database.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using ColInt.MMD.Playback.Components;
using ColInt.MMD.Playback.Helpers;
using ColInt.MMD.Samples.Lifecycle;
using NUnit.Framework;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdPoseQueryTests {
		[Test]
		public void QueryUsesDisplayedHierarchyAndBlendVelocityWithoutChangingRoot() {
			var source = MotionDatabaseFixture.Create();
			source.LeftFootBone = 0;
			source.RightFootBone = 1;
			for (int frame = 0; frame < source.Frames.Length; frame++) {
				source.Features[frame * source.FeatureCount + 24] = 3f;
			}
			using (var sample = new MmdSampleWorld(0.05f)) {
				var actor = sample.Characters.Create(MotionDatabaseBuilder.Build(source), false, LocalTransform.Identity);
				var manager = sample.World.EntityManager;
				var data = manager.GetComponentData<MmdDatabase>(actor);
				var query = manager.GetBuffer<MmdSearchQueryElement>(actor);
				var poses = manager.GetBuffer<MmdLocalPose>(actor);
				var history = manager.GetBuffer<MmdPoseFeatureHistory>(actor);
				var playback = new MmdPlayback { Speed = 1f, Time = 0.45f };
				var root = MmdAnimationMath.PoseAt(ref data.Value.Value, 0, playback.Time, 0);
				root.Position.x = 0.2f;
				var child = MmdAnimationMath.PoseAt(ref data.Value.Value, 0, playback.Time, 1);
				poses[0] = new MmdLocalPose { Value = root };
				poses[1] = new MmdLocalPose { Value = child };
				LocomotionSchema.WritePose(ref data.Value.Value, playback, query, poses, history, 0.05f);
				Assert.That(query[15].Value, Is.EqualTo(0.2f));
				Assert.That(query[21].Value, Is.EqualTo(0.2f));
				Assert.That(query[22].Value, Is.EqualTo(1.45f).Within(0.00001f));
				playback.Time = 0.5f;
				root.Position.x = 0.25f;
				child = MmdAnimationMath.PoseAt(ref data.Value.Value, 0, playback.Time, 1);
				poses[0] = new MmdLocalPose { Value = root };
				poses[1] = new MmdLocalPose { Value = child };
				LocomotionSchema.WritePose(ref data.Value.Value, playback, query, poses, history, 0.05f);
				Assert.That(query[18].Value, Is.EqualTo(1f).Within(0.00001f));
				Assert.That(query[24].Value, Is.EqualTo(4f).Within(0.00001f));
				Assert.That(query[25].Value, Is.Zero.Within(0.00001f), "Raw sample displacement is already represented by the baked velocity.");
				Assert.That(history.Length, Is.EqualTo(2));
				Assert.That(manager.GetComponentData<LocalTransform>(actor), Is.EqualTo(LocalTransform.Identity));
			}
		}
	}
}
