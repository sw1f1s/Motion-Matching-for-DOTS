using System.Collections.Generic;
using System;
using ColInt.MMD.Samples.Physics.Components;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public static class MmdSampleWorldPhysicsLoader {
		public static Entity[] Load(World world, MmdSampleWorldPhysicsGeometry source) {
			if (!source.IsCacheCompatible) {
				throw new InvalidOperationException($"Bake the world physics cache on '{source.name}' before loading the sample.");
			}
			var entities = new List<Entity>();
			try {
				foreach (var cached in source.Bodies) {
					var body = cached.Load();
					entities.Add(CreateBody(world.EntityManager, body));
				}
				return entities.ToArray();
			} catch {
				foreach (Entity entity in entities) {
					world.EntityManager.DestroyEntity(entity);
				}
				throw;
			}
		}

		// Takes ownership of the collider; releases it on failure or through ECS cleanup.
		public static Entity CreateBody(EntityManager manager, in MmdSampleWorldPhysicsBody body) {
			Entity entity = Entity.Null;
			bool owned = false;
			try {
				entity = manager.CreateEntity();
				manager.SetName(entity, "MMD World Collider: " + body.Name);
				manager.AddComponentData(entity, new MmdSamplePhysicsColliderOwner(body.Collider));
				owned = true;
				var transform = LocalTransform.FromPositionRotation(body.Transform.pos, body.Transform.rot);
				manager.AddComponentData(entity, transform);
				manager.AddComponentData(entity, new LocalToWorld { Value = transform.ToMatrix() });
				manager.AddComponentData(entity, new PhysicsCollider { Value = body.Collider });
				manager.AddSharedComponent(entity, new PhysicsWorldIndex());
				return entity;
			} catch {
				if (!owned) {
					var collider = body.Collider;
					collider.Dispose();
				}
				if (entity != Entity.Null && manager.Exists(entity)) {
					manager.DestroyEntity(entity);
				}
				throw;
			}
		}
	}
}
