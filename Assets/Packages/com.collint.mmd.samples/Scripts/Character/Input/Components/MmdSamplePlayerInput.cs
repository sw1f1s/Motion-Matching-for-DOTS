using ColInt.MMD.Samples.Camera;
using Unity.Entities;

namespace ColInt.MMD.Samples.Character.Input.Components {
	// Attach to opt a character into local Input Actions. Button state lives in MmdSampleInput.
	public struct MmdSamplePlayerInput : IComponentData {
		public UnityObjectRef<MmdSampleThirdPersonCamera> Camera;
	}
}
