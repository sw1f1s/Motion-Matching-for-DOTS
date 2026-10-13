using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public struct MmdSampleFootContact : IComponentData {
		public int LeftToe;
		public int RightToe;
	}
}
