using Unity.Entities;

namespace ColInt.MMD.Actions.Components {
	public struct MmdActionState : IComponentData {
		public int ActionId;
		public uint RequestRevision;
		public uint ActiveRevision;
		public bool Completed;
		public bool Rejected;
	}

}
