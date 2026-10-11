using ColInt.MMD.Database.Data;
using Unity.Entities;

namespace ColInt.MMD.Database.Components {
	public struct MmdDatabase : IComponentData {
		public BlobAssetReference<MotionDatabase> Value;
	}
}
