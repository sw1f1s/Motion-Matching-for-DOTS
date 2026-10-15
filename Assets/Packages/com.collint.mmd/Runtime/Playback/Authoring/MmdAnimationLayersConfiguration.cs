using System;
using ColInt.MMD.Playback.Data;
using UnityEngine;

namespace ColInt.MMD.Playback.Authoring {
	[CreateAssetMenu(fileName = "AnimationLayers", menuName = "MMD/Animation Layers")]
	public sealed class MmdAnimationLayersConfiguration : ScriptableObject {
		[SerializeField] private TextAsset _database;
		[SerializeField] private MmdAnimationLayerSettings[] _layers = Array.Empty<MmdAnimationLayerSettings>();

		public TextAsset Database => _database;
		public MmdAnimationLayerSettings[] Layers => _layers;

		public void Configure(TextAsset database, MmdAnimationLayerSettings[] layers) {
			if (database == null || layers == null) {
				throw new ArgumentException("A baked database and layer settings are required.");
			}
			_database = database;
			_layers = (MmdAnimationLayerSettings[])layers.Clone();
		}
	}
}
