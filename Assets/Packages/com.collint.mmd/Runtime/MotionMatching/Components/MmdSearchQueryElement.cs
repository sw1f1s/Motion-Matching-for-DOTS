using Unity.Entities;

namespace ColInt.MMD.MotionMatching.Components {
	/// <summary>
	/// One scalar of the character's animation-search input vector.
	/// The buffer contains pose, velocity and predicted trajectory values in database feature order.
	/// </summary>
	[InternalBufferCapacity(27)]
	public struct MmdSearchQueryElement : IBufferElementData {
		public float Value;
	}

}
