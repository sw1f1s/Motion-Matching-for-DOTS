using Unity.Entities;
using Unity.Mathematics;

namespace ColInt.MMD.Actions.Components {
	public struct MmdActionWarp : IComponentData {
		public RigidTransform TargetEnd;
		public uint RequestRevision;
		public bool Enabled;
	}

}
