using UnityEngine;

namespace ColInt.MMD.Samples.Character.Configurations {
	[CreateAssetMenu(fileName = "SampleCharacterConfiguration", menuName = "MMD/Samples/Character Configuration")]
	public sealed class MmdSampleCharacterConfiguration : ScriptableObject {
		[SerializeField] private MmdSampleCharacterAssets _assets = new MmdSampleCharacterAssets();
		[SerializeField] private MmdSampleLocomotionConfiguration _locomotion = new MmdSampleLocomotionConfiguration();
		[SerializeField] private MmdSampleTransitionConfiguration _transitions = new MmdSampleTransitionConfiguration();
		[SerializeField] private MmdSampleJumpConfiguration _jump = new MmdSampleJumpConfiguration();
		[SerializeField] private MmdSampleFallConfiguration _fall = new MmdSampleFallConfiguration();
		[SerializeField] private MmdSampleLandingConfiguration _landing = new MmdSampleLandingConfiguration();
		[SerializeField] private MmdSampleCrouchConfiguration _crouch = new MmdSampleCrouchConfiguration();
		[SerializeField] private MmdSampleSlideConfiguration _slide = new MmdSampleSlideConfiguration();
		[SerializeField] private MmdSampleTraversalConfiguration _traversal = new MmdSampleTraversalConfiguration();

		public MmdSampleCharacterAssets Assets => _assets;
		public MmdSampleLocomotionConfiguration Locomotion => _locomotion;
		public MmdSampleTransitionConfiguration Transitions => _transitions;
		public MmdSampleJumpConfiguration Jump => _jump;
		public MmdSampleFallConfiguration Fall => _fall;
		public MmdSampleLandingConfiguration Landing => _landing;
		public MmdSampleCrouchConfiguration Crouch => _crouch;
		public MmdSampleSlideConfiguration Slide => _slide;
		public MmdSampleTraversalConfiguration Traversal => _traversal;
	}
}
