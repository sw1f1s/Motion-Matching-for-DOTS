using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.LandingState.Components {
	public struct MmdSampleLanding : IComponentData {
		public int StandLightAction;
		public int WalkLightAction;
		public int RunLightAction;
		public int StandHeavyAction;
		public int WalkHeavyAction;
		public int RunHeavyAction;
	}
}
