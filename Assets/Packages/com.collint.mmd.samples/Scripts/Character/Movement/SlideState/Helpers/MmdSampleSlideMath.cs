using System.Runtime.CompilerServices;
using ColInt.MMD.Physics.Helpers;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;
using ColInt.MMD.Samples.Character.Movement.SlideState.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.SlideState.Helpers {
	public static class MmdSampleSlideMath {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GroundNormal(in CollisionWorld world, Entity entity, in PhysicsCollider collider, in LocalTransform transform, in MmdSampleMovementSettings movement) {
			var ray = new RaycastInput {
				Start = transform.Position + new float3(0f, movement.Ground.StepHeight + 0.1f, 0f),
				End = transform.Position - new float3(0f, movement.Ground.StepHeight + movement.GroundContactDistance + 0.1f, 0f),
				Filter = collider.Value.Value.GetCollisionFilter()
			};
			var collector = new MmdGroundRayCollector(entity, movement.Ground.MinimumGroundNormalY);
			return world.CastRay(ray, ref collector) ? math.normalizesafe(collector.ClosestHit.SurfaceNormal, math.up()) : math.up();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 FromPlanarVelocity(float2 velocity, float3 normal) {
			// Collision feedback already contains the horizontal travel direction.
			// Recover only its height component; projecting a flattened vector again
			// damps downhill momentum and gradually redirects it across the slope.
			return new float3(velocity.x, -math.dot(velocity, normal.xz) / normal.y, velocity.y);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 Steer(float3 velocity, float3 normal, float2 input, float angularSpeed, float deltaTime) {
			float amount = math.saturate(math.length(input));
			if (amount <= 0f || math.lengthsq(velocity) < 0.000001f) {
				return velocity;
			}
			float3 current = math.normalize(velocity);
			float3 target = math.normalizesafe(FromPlanarVelocity(input, normal));
			float angle = math.atan2(math.dot(normal, math.cross(current, target)), math.dot(current, target));
			float limit = angularSpeed * amount * deltaTime;
			return math.rotate(quaternion.AxisAngle(normal, math.clamp(angle, -limit, limit)), velocity);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 Advance(float3 velocity, float3 normal, in MmdSampleSlide settings, float gravity, float deltaTime) {
			float speed = math.length(velocity);
			float3 direction = math.normalizesafe(velocity - normal * math.dot(velocity, normal));
			float3 acceleration = new float3(0f, -gravity, 0f);
			acceleration -= normal * math.dot(acceleration, normal);
			bool downhill = normal.y < settings.MinimumDownhillNormalY && math.dot(direction, acceleration) > 0.01f;
			float3 next = direction * math.max(0f, speed - (downhill ? 0f : settings.Deceleration * deltaTime)) + acceleration * deltaTime;
			return math.normalizesafe(next) * math.min(settings.MaximumSpeed, math.length(next));
		}
	}
}
