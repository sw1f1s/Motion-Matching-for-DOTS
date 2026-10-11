using System.Collections.Generic;
using System;
using Collider = UnityEngine.Collider;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public static class MmdSampleWorldPhysicsBuilder {
		public static void ValidateCollider(Collider collider) {
			if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider || collider is MeshCollider || collider is TerrainCollider) {
				return;
			}
			throw new NotSupportedException($"Sample world physics cannot bake {collider.GetType().Name} on '{collider.name}'. No approximate shape was generated.");
		}

		public static IEnumerable<MmdSampleWorldPhysicsBody> BuildBodies(MmdSampleWorldPhysicsGeometry source) {
			var instances = new List<MmdSampleWorldColliderInstance>();
			foreach (Collider collider in source.GetComponentsInChildren<Collider>()) {
				if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.GetComponentInParent<MmdSampleWorldPhysicsGeometry>() != source) {
					continue;
				}
				ValidateCollider(collider);
				if (collider.isTrigger) {
					continue;
				}
				if (collider.attachedRigidbody != null) {
					throw new NotSupportedException($"'{collider.name}' is dynamic authoring; this cache contains only static world geometry.");
				}
				if (collider is TerrainCollider terrain && terrain.terrainData != null) {
					MmdSampleTerrainTreeColliderCollector.Collect(terrain.terrainData, terrain.transform.localToWorldMatrix, instances);
				}
				instances.Add(new MmdSampleWorldColliderInstance(collider, collider.transform.localToWorldMatrix, false));
			}
			foreach (var instance in instances) {
				var body = CreateBody(instance, source.Friction);
				if (body.Collider.IsCreated) {
					yield return body;
				}
			}
		}

		private static MmdSampleWorldPhysicsBody CreateBody(in MmdSampleWorldColliderInstance instance, float friction) {
			Matrix4x4 matrix = instance.LocalToWorld;
			float3 scale = math.abs((float3)matrix.lossyScale);
			RigidTransform transform = new RigidTransform(matrix.rotation, instance.Center);
			BlobAssetReference<Unity.Physics.Collider> blob;
			switch (instance.Source) {
				case BoxCollider box:
					blob = MmdSampleWorldPhysicsColliderFactory.CreateBox(float3.zero, (float3)box.size * scale, friction);
					break;
				case SphereCollider sphere:
					blob = MmdSampleWorldPhysicsColliderFactory.CreateSphere(float3.zero, sphere.radius * math.cmax(scale), friction);
					break;
				case CapsuleCollider capsule:
					var shape = new MmdSampleWorldCapsuleInstance(capsule, matrix, instance.IsTerrainTree);
					blob = MmdSampleWorldPhysicsColliderFactory.CreateCapsule(float3.zero, shape.Radius, shape.Height, friction);
					transform = new RigidTransform(shape.Rotation, shape.Center);
					break;
				case MeshCollider mesh:
					if (mesh.sharedMesh == null) {
						throw new InvalidOperationException($"'{mesh.name}' has no collision mesh.");
					}
					blob = MmdSampleWorldPhysicsColliderFactory.CreateMesh(mesh.sharedMesh, Matrix4x4.Translate(-(Vector3)transform.pos) * matrix, mesh.convex, friction);
					transform.rot = quaternion.identity;
					break;
				case TerrainCollider terrain:
					if (terrain.terrainData == null || matrix.determinant <= 0f) {
						throw new InvalidOperationException($"'{terrain.name}' requires terrain data and a non-mirrored, non-zero transform.");
					}
					blob = MmdSampleWorldPhysicsColliderFactory.CreateTerrain(terrain.terrainData, friction, scale);
					transform.pos = matrix.MultiplyPoint3x4(Vector3.zero);
					break;
				default:
					throw new NotSupportedException(instance.Source.GetType().Name);
			}
			return new MmdSampleWorldPhysicsBody(instance.Source.name, transform, blob);
		}
	}
}
