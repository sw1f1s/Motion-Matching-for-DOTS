using ColInt.MMD.Playback.Components;
using Unity.Entities;
using Unity.Physics;

namespace ColInt.MMD.Samples.Character.Movement.TraversalState.Components {
	public struct MmdSampleTraversalProgress : IComponentData {
		public MmdSampleTraversalClip Clip;
		public PhysicsMassOverride PreviousMassOverride;
		public PhysicsGravityFactor PreviousGravity;
		public MmdInertialization PreviousSmoothing;
	}
}
