using System.Collections.Generic;
using UnityEngine;

namespace ColInt.MMD.Samples.Physics.Helpers {
	public static class MmdSampleTerrainTreeColliderCollector {
		public static void Collect(TerrainData data, Vector3 origin, List<MmdSampleWorldCapsuleInstance> result) {
			var instances = new List<MmdSampleWorldColliderInstance>();
			Collect(data, Matrix4x4.Translate(origin), instances);
			foreach (var instance in instances) {
				if (instance.Source is UnityEngine.CapsuleCollider capsule) {
					result.Add(new MmdSampleWorldCapsuleInstance(capsule, instance.LocalToWorld, true));
				}
			}
		}

		public static void Collect(TerrainData data, Matrix4x4 terrainTransform, List<MmdSampleWorldColliderInstance> result) {
			TreePrototype[] prototypes = data.treePrototypes;
			var colliders = new UnityEngine.Collider[prototypes.Length][];
			var relativeTransforms = new Matrix4x4[prototypes.Length][];
			for (int i = 0; i < prototypes.Length; i++) {
				GameObject prefab = prototypes[i].prefab;
				if (prefab == null || !prefab.activeSelf) {
					continue;
				}

				var enabled = new List<UnityEngine.Collider>();
				foreach (var collider in prefab.GetComponentsInChildren<UnityEngine.Collider>()) {
					if (!collider.enabled) {
						continue;
					}
					MmdSampleWorldPhysicsBuilder.ValidateCollider(collider);
					if (collider.isTrigger || collider is MeshCollider mesh && mesh.sharedMesh == null ||
						collider is TerrainCollider terrain && terrain.terrainData == null) {
						continue;
					}
					enabled.Add(collider);
				}
				colliders[i] = enabled.ToArray();
				relativeTransforms[i] = new Matrix4x4[enabled.Count];
				for (int j = 0; j < enabled.Count; j++) {
					relativeTransforms[i][j] = prefab.transform.worldToLocalMatrix * enabled[j].transform.localToWorldMatrix;
				}
			}

			Vector3 size = data.size;
			foreach (TreeInstance tree in data.treeInstances) {
				int index = tree.prototypeIndex;
				if (index < 0 || index >= prototypes.Length || colliders[index] == null || colliders[index].Length == 0 ||
					tree.widthScale <= 0f || tree.heightScale <= 0f) {
					continue;
				}

				Transform prefab = prototypes[index].prefab.transform;
				Vector3 position = Vector3.Scale(tree.position, size);
				Quaternion rotation = Quaternion.AngleAxis(tree.rotation * Mathf.Rad2Deg, Vector3.up) * prefab.localRotation;
				Vector3 scale = Vector3.Scale(new Vector3(tree.widthScale, tree.heightScale, tree.widthScale), prefab.localScale);
				Matrix4x4 treeTransform = terrainTransform * Matrix4x4.TRS(position, rotation, scale);
				for (int j = 0; j < colliders[index].Length; j++) {
					result.Add(new MmdSampleWorldColliderInstance(colliders[index][j], treeTransform * relativeTransforms[index][j], true));
				}
			}
		}
	}
}
