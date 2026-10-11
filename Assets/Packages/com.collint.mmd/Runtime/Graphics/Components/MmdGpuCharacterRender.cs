using ColInt.MMD.Graphics.Data;
using Unity.Entities.Serialization;
using Unity.Entities;

namespace ColInt.MMD.Graphics.Components {
	public struct MmdGpuCharacterRender : ICleanupComponentData {
		public EntityPrefabReference Prefab;
		public MmdGpuRenderStatus Status;
		public Entity VisualRoot;
	}
}
