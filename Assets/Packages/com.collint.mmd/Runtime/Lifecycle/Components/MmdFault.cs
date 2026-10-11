using ColInt.MMD.Lifecycle.Data;
using Unity.Entities;

namespace ColInt.MMD.Lifecycle.Components {
	// Expected data/query faults are reported as ECS state rather than managed exceptions in Burst jobs.
	public struct MmdFault : IComponentData {
		public MmdFaultCode Code;
	}

}
