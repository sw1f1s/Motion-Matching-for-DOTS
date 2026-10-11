using System;
using UnityEngine;

namespace ColInt.MMD.Editor.Physics {
	[Serializable]
	internal struct MmdSampleWorldPhysicsFingerprintSettings {
		public Matrix4x4 Matrix;
		public float Friction;
	}
}
