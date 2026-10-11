using System;
using Unity.Entities.Serialization;
using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[Serializable]
	public sealed class MmdSampleCharacterAssets {
		[field: SerializeField]
		public TextAsset Database { get; set; }

		[field: SerializeField]
		public EntityPrefabReference RenderPrefab { get; set; }
	}
}
