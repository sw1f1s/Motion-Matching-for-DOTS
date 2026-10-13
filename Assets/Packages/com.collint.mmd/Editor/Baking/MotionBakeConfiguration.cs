using ColInt.MMD.Database.Helpers;
using UnityEngine;

namespace ColInt.MMD.Editor.Baking {
	[CreateAssetMenu(menuName = "MMD/Motion Bake Configuration")]
	public sealed class MotionBakeConfiguration : ScriptableObject {
		[SerializeField] private GameObject _rig;
		[SerializeField] private MotionBakeGroup[] _groups = System.Array.Empty<MotionBakeGroup>();
		[SerializeField, Range(15f, 120f)] private float _sampleRate = 60f;
		[SerializeField, Range(0.0001f, MmdRotationPacking.DefaultMaximumErrorDegrees), Tooltip("Maximum allowed local bone rotation error in degrees. Baking fails before replacing the database if exceeded.")] private float _maximumRotationErrorDegrees = MmdRotationPacking.DefaultMaximumErrorDegrees;
		[SerializeField, Tooltip("Groups used to calculate search feature scales. Empty uses every clip. Fix a reference set to keep existing matching stable when adding other stances.")] private string[] _normalizationGroups = System.Array.Empty<string>();
		[SerializeField] private string _rootMotionPath = "root";
		[SerializeField] private string _sourceRootMotionPath;
		[SerializeField] private string _leftFootPath;
		[SerializeField] private string _rightFootPath;
		[SerializeField] private string _output = "Assets/MmdDatabases/Locomotion.bytes";

		public GameObject Rig => _rig;
		public MotionBakeGroup[] Groups => _groups;
		public float SampleRate => _sampleRate;
		public float MaximumRotationErrorDegrees => _maximumRotationErrorDegrees;
		public string[] NormalizationGroups => _normalizationGroups;
		public string RootMotionPath => _rootMotionPath;
		public string SourceRootMotionPath => _sourceRootMotionPath;
		public string LeftFootPath => _leftFootPath;
		public string RightFootPath => _rightFootPath;
		public string Output => _output;

		public void Configure(GameObject rig, MotionBakeGroup[] groups, string rootMotionPath, string leftFootPath, string rightFootPath) {
			_rig = rig;
			_groups = groups;
			_rootMotionPath = rootMotionPath;
			_leftFootPath = leftFootPath;
			_rightFootPath = rightFootPath;
		}
	}

}
