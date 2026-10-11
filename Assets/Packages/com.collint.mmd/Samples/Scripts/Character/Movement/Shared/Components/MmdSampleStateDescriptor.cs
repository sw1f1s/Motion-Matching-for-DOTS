using System.Runtime.CompilerServices;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.Shared.Components {
	public readonly struct MmdSampleStateDescriptor {
		public readonly ComponentType Component;
		public readonly ComponentType Started;
		public readonly ComponentType Finished;

		public bool IsValid => Component.TypeIndex != TypeIndex.Null;

		private MmdSampleStateDescriptor(ComponentType component, ComponentType started, ComponentType finished) {
			Component = component;
			Started = started;
			Finished = finished;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MmdSampleStateDescriptor For<T>() where T : unmanaged, IComponentData {
			return new MmdSampleStateDescriptor(ComponentType.ReadWrite<MmdSampleState<T>>(),
				ComponentType.ReadWrite<MmdSampleStateStarted<T>>(), ComponentType.ReadWrite<MmdSampleStateFinished<T>>());
		}
	}
}
