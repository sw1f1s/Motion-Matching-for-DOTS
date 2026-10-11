using ColInt.MMD.Debugging.Helpers;
using ColInt.MMD.MotionMatching.Components;
using ColInt.MMD.MotionMatching.Helpers;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ColInt.MMD.Tests {
	public class MmdTrajectoryDebugGeometryTests {
		[Test]
		public void PointsReadQueryInWorldSpaceWithoutScalingOrMutatingInput() {
			using (var world = new World("MMD Trajectory Geometry")) {
				var manager = world.EntityManager;
				Entity character = manager.CreateEntity();
				var query = manager.AddBuffer<MmdSearchQueryElement>(character);
				query.ResizeUninitialized(LocomotionSchema.Dimension);
				LocomotionSchema.WriteTrajectory(query, new float3(2f, 0f, 1f), 1f);
				var transform = LocalTransform.FromPositionRotationScale(new float3(4f, 0f, 5f), quaternion.RotateY(math.PI * 0.5f), 3f);
				using (var before = query.ToNativeArray(Allocator.Temp)) {
					Assert.That(MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsNativeArray().AsReadOnly(), transform, 2, 0.06f, out var point), Is.True);
					Assert.That(math.distance(point.Position, new float3(4.6f, 0.06f, 3.8f)), Is.LessThan(0.00001f));
					float3 expectedForward = math.rotate(math.mul(transform.Rotation, quaternion.RotateY(0.6f)), new float3(0f, 0f, 1f));
					Assert.That(math.distance(point.Forward, expectedForward), Is.LessThan(0.00001f));
					for (int feature = 0; feature < 15; feature++) {
						Assert.That(query[feature].Value, Is.EqualTo(before[feature].Value));
					}
				}
			}
		}

		[Test]
		public void TruncatedAndNonFiniteInputDoesNotProduceTrajectoryPoints() {
			using (var query = new NativeArray<MmdSearchQueryElement>(12, Allocator.Temp)) {
				var values = query;
				values[0] = new MmdSearchQueryElement { Value = float.NaN };
				Assert.That(MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsReadOnly(), LocalTransform.Identity, 0, 0f, out _), Is.False);
				Assert.That(MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsReadOnly(), LocalTransform.Identity, 3, 0f, out _), Is.False);
				Assert.That(MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsReadOnly(), LocalTransform.Identity, 1, float.NaN, out _), Is.False);
			}
			using (var query = new NativeArray<MmdSearchQueryElement>(8, Allocator.Temp)) {
				Assert.That(MmdTrajectoryDebugGeometry.TryReadQueryPoint(query.AsReadOnly(), LocalTransform.Identity, 0, 0f, out _), Is.False);
			}
		}
	}
}
