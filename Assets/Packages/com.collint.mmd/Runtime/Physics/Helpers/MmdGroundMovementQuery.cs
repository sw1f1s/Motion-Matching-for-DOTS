using System.Runtime.CompilerServices;
using ColInt.MMD.Physics.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Physics.Helpers {
	public static class MmdGroundMovementQuery {
		// Resolve the same small segments for prediction and root consumption. Never writes transforms.
		public static float3 Move(in CollisionWorld world, Entity owner, in PhysicsCollider collider, in LocalTransform transform,
			in MmdGroundMovement settings, float3 position, float3 displacement, out bool supported) {
			float3 target = position + new float3(displacement.x, 0f, displacement.z);
			supported = Ground(world, owner, collider, transform, settings, target, out var targetGround);
			if (supported) {
				target = targetGround;
			}
			float3 travel = target - position;
			if (math.lengthsq(travel) < 0.00000001f) {
				return target;
			}
			var cast = new ColliderCastInput(collider.Value, position, target, transform.Rotation, transform.Scale);
			var collector = new MmdGroundCastCollector(owner, settings.MinimumGroundNormalY, travel, false);
			if (!world.CastCollider(cast, ref collector)) {
				return target;
			}
			// Confirm a walkable top before lifting past a low vertical riser. A capsule's
			// rounded leading edge otherwise reports an unwalkable contact at the step corner.
			// Starting support is needed only to attempt a step after hitting an obstacle.
			if (settings.StepHeight > 0f && Ground(world, owner, collider, transform, settings, position, out _)) {
				float3 direction = math.normalizesafe(new float3(displacement.x, 0f, displacement.z));
				var bounds = collider.Value.Value.CalculateAabb(new RigidTransform(transform.Rotation, position), transform.Scale);
				float reach = math.dot(math.abs(direction), bounds.Extents * 0.5f);
				float3 probe = target + direction * reach;
				var ray = new RaycastInput {
					Start = probe + new float3(0f, settings.StepHeight + settings.Skin, 0f),
					End = probe - new float3(0f, settings.GroundProbeDistance, 0f),
					Filter = collider.Value.Value.GetCollisionFilter()
				};
				var top = new MmdGroundRayCollector(owner, settings.MinimumGroundNormalY);
				if (world.CastRay(ray, ref top) && top.ClosestHit.Position.y > position.y + settings.Skin &&
					top.ClosestHit.Position.y - position.y <= settings.StepHeight) {
					float3 raisedStart = position + new float3(0f, settings.StepHeight, 0f);
					float3 raisedTarget = new float3(target.x, raisedStart.y, target.z);
					var edgeSettings = settings;
					edgeSettings.MinimumGroundNormalY = 0.01f;
					if (Clear(world, owner, collider, transform, settings, position, raisedStart) &&
						Clear(world, owner, collider, transform, settings, raisedStart, raisedTarget) &&
						Ground(world, owner, collider, transform, edgeSettings, raisedTarget, out var stepGround) &&
						stepGround.y - position.y <= settings.StepHeight + settings.Skin) {
						supported = true;
						return stepGround;
					}
				}
			}
			float fraction = math.max(0f, collector.ClosestHit.Fraction - settings.Skin / math.max(settings.Skin, math.length(travel)));
			float3 stopped = position + travel * fraction;
			float3 remaining = travel * (1f - fraction);
			float3 normal = math.normalizesafe(new float3(collector.ClosestHit.SurfaceNormal.x, 0f, collector.ClosestHit.SurfaceNormal.z));
			float3 slide = remaining - normal * math.min(0f, math.dot(remaining, normal));
			float3 slidingTarget = stopped + new float3(slide.x, 0f, slide.z);
			supported = Ground(world, owner, collider, transform, settings, slidingTarget, out var slideGround);
			if (supported) {
				slidingTarget = slideGround;
			}
			cast = new ColliderCastInput(collider.Value, stopped, slidingTarget, transform.Rotation, transform.Scale);
			collector = new MmdGroundCastCollector(owner, settings.MinimumGroundNormalY, slidingTarget - stopped, false);
			if (world.CastCollider(cast, ref collector)) {
				float length = math.length(slidingTarget - stopped);
				fraction = math.max(0f, collector.ClosestHit.Fraction - settings.Skin / math.max(settings.Skin, length));
				float3 resolved = math.lerp(stopped, slidingTarget, fraction);
				supported = Ground(world, owner, collider, transform, settings, resolved, out _);
				return resolved;
			}
			return slidingTarget;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Ground(in CollisionWorld world, Entity owner, in PhysicsCollider collider, in LocalTransform transform,
			in MmdGroundMovement settings, float3 position, out float3 ground) {
			float3 start = position + new float3(0f, settings.StepHeight + settings.Skin, 0f);
			float3 end = position - new float3(0f, settings.GroundProbeDistance, 0f);
			var cast = new ColliderCastInput(collider.Value, start, end, transform.Rotation, transform.Scale);
			var collector = new MmdGroundCastCollector(owner, settings.MinimumGroundNormalY, end - start, true);
			if (world.CastCollider(cast, ref collector)) {
				ground = math.lerp(start, end, collector.ClosestHit.Fraction) + new float3(0f, settings.Skin, 0f);
				return true;
			}
			ground = position;
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Clear(in CollisionWorld world, Entity owner, in PhysicsCollider collider, in LocalTransform transform,
			in MmdGroundMovement settings, float3 start, float3 end) {
			var cast = new ColliderCastInput(collider.Value, start, end, transform.Rotation, transform.Scale);
			var collector = new MmdGroundCastCollector(owner, settings.MinimumGroundNormalY, end - start, false);
			return !world.CastCollider(cast, ref collector);
		}
	}
}
