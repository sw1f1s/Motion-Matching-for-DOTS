using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Presentation.Components {
	[InternalBufferCapacity(0)]
	public struct MmdModelMatrix : IBufferElementData {
		public float4x4 Value;
	}

}
