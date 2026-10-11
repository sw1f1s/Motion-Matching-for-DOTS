using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Graphics.Components {
	public struct MmdSkinSource : IComponentData {
		public Entity PoseEntity;
		public float4x4 ModelToMesh;
	}

}
