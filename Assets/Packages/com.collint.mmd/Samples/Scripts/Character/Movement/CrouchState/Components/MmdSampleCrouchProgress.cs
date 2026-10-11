using ColInt.MMD.Playback.Components;
using ColInt.MMD.Samples.Character.Components;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Movement.CrouchState.Components {
	public struct MmdSampleCrouchProgress : IComponentData {
		public MmdSampleCharacterControl StandingControl;
		public MmdInertialization StandingInertialization;
		public float Duration;
	}
}
