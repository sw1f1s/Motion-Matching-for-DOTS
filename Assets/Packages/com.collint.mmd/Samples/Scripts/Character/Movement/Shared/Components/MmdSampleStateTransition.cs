using System.Runtime.CompilerServices;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public struct MmdSampleStateTransition : IComponentData {
		public MmdSampleStateDescriptor Target;
		public int Priority;
		public bool Preparing;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Request<T>(int priority) where T : unmanaged, IComponentData {
			if (Preparing || Target.IsValid && priority <= Priority) {
				return;
			}
			Target = MmdSampleStateDescriptor.For<T>();
			Priority = priority;
		}
	}
}
