using ColInt.MMD.Database.Helpers;
using ColInt.MMD.Playback.Helpers;
using NUnit.Framework;
using Unity.Mathematics;

namespace ColInt.MMD.Tests {
	public class MotionSamplingTests {
		[Test]
		public void RootMotionCrossesLoopWithoutJumpingBack() {
			var blob = MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create());
			try {
				ref var data = ref blob.Value;
				RigidTransform delta = math.mul(math.inverse(MmdAnimationMath.RootAt(ref data, 1, 0.95f)), MmdAnimationMath.RootAt(ref data, 1, 1.05f));
				Assert.That(delta.pos.z, Is.EqualTo(0.1f).Within(0.00001f));
				Assert.That(MmdAnimationMath.RootAt(ref data, 1, 3.25f).pos.z, Is.EqualTo(3.25f).Within(0.00001f));
			} finally {
				blob.Dispose();
			}
		}

		[Test]
		public void RootCyclesComposeRotationAndTranslation() {
			var source = MotionDatabaseFixture.Create();
			for (int i = 0; i < 11; i++) {
				source.Frames[11 + i].Root.rot = quaternion.RotateY(math.PI * 0.5f * i / 10f);
			}
			var blob = MotionDatabaseBuilder.Build(source);
			try {
				ref var data = ref blob.Value;
				RigidTransform end = MmdAnimationMath.RootAt(ref data, 1, 2f);
				Assert.That(end.pos.x, Is.EqualTo(1f).Within(0.0001f));
				Assert.That(end.pos.z, Is.EqualTo(1f).Within(0.0001f));
				Assert.That(MmdAnimationMath.RootAt(ref data, 1, -1f).pos.x, Is.EqualTo(1f).Within(0.0001f));
			} finally {
				blob.Dispose();
			}
		}

		[Test]
		public void PoseInterpolatesAndNonLoopClampsToExactEndpoint() {
			var blob = MotionDatabaseBuilder.Build(MotionDatabaseFixture.Create());
			try {
				ref var data = ref blob.Value;
				Assert.That(MmdAnimationMath.PoseAt(ref data, 1, 0.15f, 1).Position.y, Is.EqualTo(1.15f).Within(0.00001f));
				Assert.That(MmdAnimationMath.RootAt(ref data, 2, 4f).pos.z, Is.EqualTo(2f).Within(0.00001f));
				Assert.That(MmdAnimationMath.PoseAt(ref data, 2, 4f, 1).Position.y, Is.EqualTo(2f).Within(0.00001f));
			} finally {
				blob.Dispose();
			}
		}

		[Test]
		public void WarpReachesValidatedTargetExactly() {
			RigidTransform start = RigidTransform.identity;
			RigidTransform end = new RigidTransform(quaternion.identity, new float3(0f, 0f, 2f));
			RigidTransform target = new RigidTransform(quaternion.RotateY(0.2f), new float3(1f, 0.5f, 3f));
			RigidTransform delta = MmdAnimationMath.WarpDelta(start, end, end, target, 0f, 1f);
			Assert.That(math.distance(delta.pos, target.pos), Is.LessThan(0.00001f));
			Assert.That(math.abs(math.dot(delta.rot.value, target.rot.value)), Is.EqualTo(1f).Within(0.00001f));
		}
	}

}
