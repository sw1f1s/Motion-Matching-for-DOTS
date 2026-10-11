using UnityEngine;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public readonly struct MmdSampleWorldColliderInstance {
		public readonly Collider Source;
		public readonly Matrix4x4 LocalToWorld;
		public readonly bool IsTerrainTree;

		public Vector3 Center => LocalToWorld.MultiplyPoint3x4(Source switch {
			BoxCollider box => box.center,
			SphereCollider sphere => sphere.center,
			CapsuleCollider capsule => capsule.center,
			MeshCollider mesh => mesh.sharedMesh.bounds.center,
			TerrainCollider terrain => terrain.terrainData.size * 0.5f,
			_ => Vector3.zero
		});

		public MmdSampleWorldColliderInstance(Collider source, Matrix4x4 localToWorld, bool isTerrainTree) {
			Source = source;
			LocalToWorld = localToWorld;
			IsTerrainTree = isTerrainTree;
		}
	}
}
