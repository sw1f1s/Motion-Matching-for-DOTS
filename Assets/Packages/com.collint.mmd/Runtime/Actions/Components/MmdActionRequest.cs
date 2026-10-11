using Unity.Entities;

namespace ColInt.MMD.Actions.Components {
	// ActionId is authored in the database; zero means locomotion/cancel.
	public struct MmdActionRequest : IComponentData {
		public int ActionId;
		public uint Revision;
		public int PreferredClip;
	}

}
