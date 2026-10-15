using System;
using ColInt.MMD.Database.Data;
using Unity.Entities;

namespace ColInt.MMD.Playback.Data {
	// World/host-owned resource. Dispose after all attached characters and their jobs.
	public sealed class MmdAnimationLayerSet : IDisposable {
		private BlobAssetReference<MmdAnimationLayersDefinition> _definition;

		public BlobAssetReference<MotionDatabase> Database { get; }
		public BlobAssetReference<MmdAnimationLayersDefinition> Definition => _definition;

		internal MmdAnimationLayerSet(BlobAssetReference<MotionDatabase> database, BlobAssetReference<MmdAnimationLayersDefinition> definition) {
			Database = database;
			_definition = definition;
		}

		public void Dispose() {
			if (_definition.IsCreated) {
				_definition.Dispose();
				_definition = default;
			}
		}
	}
}
