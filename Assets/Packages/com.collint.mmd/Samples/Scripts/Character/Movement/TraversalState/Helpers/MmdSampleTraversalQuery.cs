using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using ColInt.MMD.Samples.Character.Movement.TraversalState.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Helpers {
	public static class MmdSampleTraversalQuery {
		public static bool Probe(CollisionWorld world, Entity owner, in LocalTransform transform, float3 requestedDirection,
			in MmdSampleTraversalSettings settings, bool airborne, out MmdSampleTraversalTarget target) {
			target = default;
			float3 direction = math.normalizesafe(new float3(requestedDirection.x, 0f, requestedDirection.z));
			if (math.lengthsq(direction) < 0.5f) {
				return false;
			}
			float maximumHeight = airborne ? settings.CatchReach : settings.MaximumHeight;
			// Grounded clips contain an approach. Airborne catches begin at the wall.
			float reach = airborne ? math.min(settings.Reach, settings.Radius * transform.Scale + settings.Skin + settings.CatchDistance) : settings.Reach;
			RaycastHit face = default;
			bool found = false;
			for (int level = 0; level < 3; level++) {
				float3 start = transform.Position + math.up() * (level == 0 ? 0.25f : level == 1 ? 1.1f : 1.7f);
				if (Ray(world, owner, start, start + direction * reach, out var hit) &&
					hit.RigidBodyIndex >= world.NumDynamicBodies && math.abs(hit.SurfaceNormal.y) < 0.2f &&
					math.dot(-hit.SurfaceNormal, direction) > 0.7f && (!found || hit.Fraction < face.Fraction)) {
					face = hit;
					found = true;
				}
			}
			if (!found) {
				return false;
			}
			direction = math.normalizesafe(new float3(-face.SurfaceNormal.x, 0f, -face.SurfaceNormal.z));
			float3 sample = face.Position + direction * 0.12f;
			sample.y = transform.Position.y;
			if (!Top(world, owner, sample, maximumHeight, out var top) || top.Entity != face.Entity) {
				return false;
			}
			float height = top.Position.y - transform.Position.y;
			if (height < settings.MinimumHeight || height > maximumHeight) {
				return false;
			}
			float3 lateral = math.cross(math.up(), direction) * (settings.Radius + settings.Skin);
			if (!Top(world, owner, sample + lateral, maximumHeight, out var left) || left.Entity != face.Entity ||
				!Top(world, owner, sample - lateral, maximumHeight, out var right) || right.Entity != face.Entity ||
				math.abs(left.Position.y - top.Position.y) > 0.1f || math.abs(right.Position.y - top.Position.y) > 0.1f) {
				return false;
			}
			float depth = 0.12f;
			bool across = false;
			for (float step = 0.24f; step <= settings.MaximumCrossingDepth + 0.12f; step += 0.12f) {
				float3 point = face.Position + direction * step;
				point.y = transform.Position.y;
				if (!Top(world, owner, point, maximumHeight, out var surface) || surface.Entity != face.Entity ||
					math.abs(surface.Position.y - top.Position.y) > 0.1f) {
					depth = step;
					across = step <= settings.MaximumCrossingDepth + 0.12f;
					break;
				}
				depth = step;
			}
			// Tall walls always climb onto the ledge; thin low obstacles can be crossed.
			across &= height <= settings.MantleHeight;
			float endDepth = across ? depth + settings.Radius + settings.Skin : math.min(settings.Radius + settings.Skin, depth * 0.5f);
			float3 end = face.Position + direction * endDepth;
			end.y = top.Position.y + settings.Skin;
			bool supported = true;
			if (across) {
				float3 upper = end + math.up() * 0.1f;
				float3 lower = end - math.up() * (height + settings.MaximumLandingDrop);
				if (Ray(world, owner, upper, lower, out var landing) && landing.SurfaceNormal.y > 0.8f && landing.Entity != face.Entity) {
					end.y = landing.Position.y + settings.Skin;
				} else {
					// The animation hands the character back to Fall just beyond the edge.
					end.y = math.max(transform.Position.y, top.Position.y - 0.35f);
					supported = false;
				}
			}
			float3 front = face.Position - direction * (settings.Radius + settings.Skin);
			if (math.dot(front - transform.Position, direction) < 0f) {
				front = transform.Position;
			}
			front.y = transform.Position.y;
			float3 aboveFront = front;
			aboveFront.y = top.Position.y + settings.Skin;
			float3 aboveEnd = end;
			aboveEnd.y = aboveFront.y;
			target = new MmdSampleTraversalTarget {
				Obstacle = face.Entity, ObstacleTransform = world.Bodies[face.RigidBodyIndex].WorldFromBody,
				Start = transform.Position, StartRotation = transform.Rotation, Rotation = quaternion.LookRotationSafe(direction, math.up()),
				Front = front, AboveFront = aboveFront, AboveEnd = aboveEnd, End = end, Height = height, Across = across, Supported = supported
			};
			return true;
		}

		public static void ExtendLanding(CollisionWorld world, Entity owner, ref MmdSampleTraversalTarget target, float speed) {
			if (!target.Supported || !target.Across) {
				return;
			}
			float3 end = target.End + math.forward(target.Rotation) * math.min(1.25f, speed * 0.25f);
			if (Ray(world, owner, end + math.up() * 0.2f, end - math.up() * 0.2f, out var hit) &&
				hit.SurfaceNormal.y > 0.8f && math.abs(hit.Position.y + 0.04f - target.End.y) < 0.08f) {
				end.y = hit.Position.y + 0.04f;
				target.End = end;
			}
		}

		public static bool ClearPath(ref MotionDatabase database, CollisionWorld world, Entity owner, PhysicsCollider collider,
			in MmdSampleTraversalTarget target, in MmdSampleTraversalClip clip, float scale) {
			float3 previous = target.Start;
			for (int sample = 0; sample <= 32; sample++) {
				float3 next = MmdSampleTraversalPath.Position(ref database, target, clip, math.lerp(clip.StartTime, clip.Duration, sample / 32f));
				if (!ClearSegment(world, owner, collider, previous, next, target.Rotation, scale)) {
					return false;
				}
				previous = next;
			}
			return true;
		}

		public static bool ClearSegment(CollisionWorld world, Entity owner, PhysicsCollider collider,
			float3 start, float3 end, quaternion rotation, float scale) {
			var distance = new ColliderDistanceInput(collider.Value, 0f, new RigidTransform(rotation, end), scale);
			var clearance = new MmdSampleTraversalClearanceCollector { Owner = owner };
			world.CalculateDistance(distance, ref clearance);
			if (clearance.Blocked) {
				return false;
			}
			if (math.distancesq(start, end) > 0.000001f) {
				var cast = new ColliderCastInput(collider.Value, start, end, rotation, scale);
				var collector = new MmdSampleTraversalCastCollector { Owner = owner, Displacement = end - start };
				world.CastCollider(cast, ref collector);
				return !collector.Blocked;
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TargetExists(CollisionWorld world, in MmdSampleTraversalTarget target) {
			int index = world.GetRigidBodyIndex(target.Obstacle);
			return index >= world.NumDynamicBodies && index < world.Bodies.Length &&
				math.distancesq(world.Bodies[index].WorldFromBody.pos, target.ObstacleTransform.pos) < 0.0001f &&
				math.abs(math.dot(world.Bodies[index].WorldFromBody.rot.value, target.ObstacleTransform.rot.value)) > 0.9999f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Top(CollisionWorld world, Entity owner, float3 point, float height, out RaycastHit hit) {
			return Ray(world, owner, point + math.up() * (height + 0.1f), point - math.up() * 0.1f, out hit) && hit.SurfaceNormal.y > 0.8f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Ray(CollisionWorld world, Entity owner, float3 start, float3 end, out RaycastHit hit) {
			var input = new RaycastInput { Start = start, End = end, Filter = CollisionFilter.Default };
			var collector = new MmdSampleTraversalRayCollector(owner);
			world.CastRay(input, ref collector);
			hit = collector.Hit;
			return collector.NumHits != 0;
		}
	}
}
