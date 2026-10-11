using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public struct MmdSampleStateMachine : IComponentData {
		public MmdSampleStateDescriptor Current;
		public uint Revision;
	}
}
