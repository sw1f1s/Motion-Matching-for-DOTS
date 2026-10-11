using Unity.Collections;

namespace ColInt.MMD.Database.Data {
	public struct MotionGroup {
		public FixedString64Bytes Name;
		public uint Tags;
		public int ActionId;
	}
}
