using Unity.Mathematics;
using UnityEngine;

namespace ColInt.MMD.Samples.Physics.Helpers {
	// A world-space capsule, shared by scene prefabs and Terrain tree instances.
	public readonly struct MmdSampleWorldCapsuleInstance {
		public readonly float3 Center;
		public readonly quaternion Rotation;
		public readonly float Radius;
		public readonly float Height;
		public readonly bool IsTerrainTree;

		public MmdSampleWorldCapsuleInstance(UnityEngine.CapsuleCollider collider, Matrix4x4 localToWorld, bool isTerrainTree) {
			Vector3 axis = collider.direction == 0 ? Vector3.right : collider.direction == 2 ? Vector3.forward : Vector3.up;
			Vector3 perpendicularA = collider.direction == 0 ? Vector3.up : Vector3.right;
			Vector3 perpendicularB = collider.direction == 2 ? Vector3.up : Vector3.forward;
			Vector3 worldAxis = localToWorld.MultiplyVector(axis);
			float radiusScale = Mathf.Max(localToWorld.MultiplyVector(perpendicularA).magnitude,
				localToWorld.MultiplyVector(perpendicularB).magnitude);
			Center = localToWorld.MultiplyPoint3x4(collider.center);
			Radius = math.max(collider.radius * radiusScale, 0.001f);
			Height = math.max(collider.height * worldAxis.magnitude, Radius * 2f);
			Rotation = worldAxis.sqrMagnitude > 0.000001f ? Quaternion.FromToRotation(Vector3.up, worldAxis.normalized) : Quaternion.identity;
			IsTerrainTree = isTerrainTree;
		}
	}
}
