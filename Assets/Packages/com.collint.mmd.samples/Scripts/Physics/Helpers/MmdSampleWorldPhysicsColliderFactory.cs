using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityPhysicsCollider = Unity.Physics.Collider;
using UnityPhysicsMaterial = Unity.Physics.Material;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public static class MmdSampleWorldPhysicsColliderFactory {
		public static BlobAssetReference<UnityPhysicsCollider> CreateCapsule(
			float3 center,
			float radius,
			float height,
			float friction) {
			float safeRadius = math.max(radius, 0.001f);
			float safeHeight = math.max(height, safeRadius * 2f);
			float halfSegment = safeHeight * 0.5f - safeRadius;
			var geometry = new CapsuleGeometry {
				Vertex0 = center - math.up() * halfSegment,
				Vertex1 = center + math.up() * halfSegment,
				Radius = safeRadius,
			};
			return CapsuleCollider.Create(geometry, CollisionFilter.Default, CreateMaterial(friction));
		}

		public static BlobAssetReference<UnityPhysicsCollider> CreateBox(float3 center, float3 size, float friction) {
			float3 safeSize = math.max(size, new float3(0.001f));
			var geometry = new BoxGeometry {
				Center = center,
				Orientation = quaternion.identity,
				Size = safeSize,
				BevelRadius = math.min(math.cmin(safeSize) * 0.01f, 0.05f),
			};
			return BoxCollider.Create(geometry, CollisionFilter.Default, CreateMaterial(friction));
		}

		public static BlobAssetReference<UnityPhysicsCollider> CreateSphere(float3 center, float radius, float friction) {
			return SphereCollider.Create(new SphereGeometry { Center = center, Radius = math.max(radius, 0.001f) },
				CollisionFilter.Default, CreateMaterial(friction));
		}

		public static BlobAssetReference<UnityPhysicsCollider> CreateTerrain(UnityEngine.TerrainData data, float friction) {
			return CreateTerrain(data, friction, new float3(1f));
		}

		public static BlobAssetReference<UnityPhysicsCollider> CreateTerrain(UnityEngine.TerrainData data, float friction, float3 scale) {
			int resolution = data.heightmapResolution;
			float[,] sourceHeights = data.GetHeights(0, 0, resolution, resolution);
			bool[,] surface = data.GetHoles(0, 0, data.holesResolution, data.holesResolution);
			foreach (bool present in surface) {
				if (!present) {
					return CreateTerrainWithHoles(sourceHeights, surface, (float3)data.heightmapScale * scale, friction);
				}
			}
			var heights = new NativeArray<float>(resolution * resolution, Allocator.Temp);
			try {
				// TerrainData indexes [z, x]; Unity Physics expects x to vary fastest.
				for (int z = 0; z < resolution; z++) {
					for (int x = 0; x < resolution; x++) {
						heights[z * resolution + x] = sourceHeights[z, x];
					}
				}
				return TerrainCollider.Create(heights, new int2(resolution), (float3)data.heightmapScale * scale,
					TerrainCollider.CollisionMethod.Triangles, CollisionFilter.Default, CreateMaterial(friction));
			} finally {
				heights.Dispose();
			}
		}

		// Unity Physics heightfields have no hole mask. Keep heightfields for intact tiles,
		// and use a triangle surface only around holes, allowing passage below the ground.
		private static BlobAssetReference<UnityPhysicsCollider> CreateTerrainWithHoles(
			float[,] heights, bool[,] surface, float3 scale, float friction) {
			const int tileSize = 256;
			int cells = heights.GetLength(0) - 1;
			using var children = new NativeList<CompoundCollider.ColliderBlobInstance>(Allocator.Temp);
			try {
				for (int z0 = 0; z0 < cells; z0 += tileSize) {
					for (int x0 = 0; x0 < cells; x0 += tileSize) {
						int width = math.min(tileSize, cells - x0), depth = math.min(tileSize, cells - z0);
						bool hasHole = false, hasSurface = false;
						for (int z = 0; z < depth; z++) {
							for (int x = 0; x < width; x++) {
								if (surface[z0 + z, x0 + x]) {
									hasSurface = true;
								} else {
									hasHole = true;
								}
							}
						}

						if (!hasSurface) {
							continue;
						}

						BlobAssetReference<UnityPhysicsCollider> child;
						int stride = width + 1;
						if (!hasHole) {
							var tileHeights = new NativeArray<float>(stride * (depth + 1), Allocator.Temp);
							try {
								for (int z = 0; z <= depth; z++) {
									for (int x = 0; x <= width; x++) {
										tileHeights[z * stride + x] = heights[z0 + z, x0 + x];
									}
								}

								child = TerrainCollider.Create(tileHeights, new int2(stride, depth + 1), scale,
																		TerrainCollider.CollisionMethod.Triangles, CollisionFilter.Default, CreateMaterial(friction));
							} finally {
								tileHeights.Dispose();
							}
						} else {
							var vertices = new NativeArray<float3>(stride * (depth + 1), Allocator.Temp);
							try {
								using var triangles = new NativeList<int3>(Allocator.Temp);
								for (int z = 0; z <= depth; z++) {
									for (int x = 0; x <= width; x++) {
										vertices[z * stride + x] = new float3(x, heights[z0 + z, x0 + x], z) * scale;
									}
								}

								for (int z = 0; z < depth; z++) {
									for (int x = 0; x < width; x++) {
										if (!surface[z0 + z, x0 + x]) {
											continue;
										}

										int i = z * stride + x;
										triangles.Add(new int3(i, i + stride, i + 1));
										triangles.Add(new int3(i + 1, i + stride, i + stride + 1));
									}
								}

								child = MeshCollider.Create(vertices, triangles.AsArray(), CollisionFilter.Default, CreateMaterial(friction));
							} finally {
								vertices.Dispose();
							}
						}
						children.Add(new CompoundCollider.ColliderBlobInstance {
							Collider = child,
							CompoundFromChild = new RigidTransform(quaternion.identity, new float3(x0 * scale.x, 0, z0 * scale.z))
						});
					}
				}
				return children.Length == 0 ? default : CompoundCollider.Create(children.AsArray());
			} finally {
				for (int i = 0; i < children.Length; i++) {
					var child = children[i].Collider;
					child.Dispose();
				}
			}
		}

		public static BlobAssetReference<UnityPhysicsCollider> CreateMesh(UnityEngine.Mesh mesh,
			UnityEngine.Matrix4x4 colliderFromMesh, bool convex, float friction) {
			if (mesh == null || mesh.vertexCount == 0) {
				throw new System.ArgumentException("MeshCollider requires a non-empty mesh.", nameof(mesh));
			}

			if (!mesh.isReadable) {
				throw new System.InvalidOperationException($"Mesh '{mesh.name}' requires Read/Write Enabled for runtime ECS collision import.");
			}

			var sourceVertices = mesh.vertices;
			var vertices = new NativeArray<float3>(sourceVertices.Length, Allocator.Temp);
			try {
				for (int i = 0; i < sourceVertices.Length; i++) {
					vertices[i] = colliderFromMesh.MultiplyPoint3x4(sourceVertices[i]);
				}

				if (convex) {
					var parameters = ConvexHullGenerationParameters.Default;
					parameters.BevelRadius = 0f;
					return ConvexCollider.Create(vertices, parameters, CollisionFilter.Default, CreateMaterial(friction));
				}

				using var triangles = new NativeList<int3>(Allocator.Temp);
				bool mirrored = colliderFromMesh.determinant < 0f;
				for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++) {
					var topology = mesh.GetTopology(subMesh);
					if (topology != UnityEngine.MeshTopology.Triangles && topology != UnityEngine.MeshTopology.Quads) {
						continue;
					}
					// Applying baseVertex preserves meshes with multiple submeshes / 16-bit index windows.
					int[] indices = mesh.GetIndices(subMesh, true);
					int stride = topology == UnityEngine.MeshTopology.Quads ? 4 : 3;
					for (int i = 0; i + stride <= indices.Length; i += stride) {
						triangles.Add(mirrored ? new int3(indices[i], indices[i + 2], indices[i + 1]) :
							new int3(indices[i], indices[i + 1], indices[i + 2]));
						if (stride == 4) {
							triangles.Add(mirrored ? new int3(indices[i], indices[i + 3], indices[i + 2]) :
							new int3(indices[i], indices[i + 2], indices[i + 3]));
						}
					}
				}
				if (triangles.Length == 0) {
					throw new System.ArgumentException($"Mesh '{mesh.name}' has no collision triangles.", nameof(mesh));
				}

				return MeshCollider.Create(vertices, triangles.AsArray(), CollisionFilter.Default, CreateMaterial(friction));
			} finally {
				vertices.Dispose();
			}
		}

		private static UnityPhysicsMaterial CreateMaterial(float friction) {
			UnityPhysicsMaterial material = UnityPhysicsMaterial.Default;
			material.Friction = math.max(friction, 0f);
			return material;
		}
	}
}
