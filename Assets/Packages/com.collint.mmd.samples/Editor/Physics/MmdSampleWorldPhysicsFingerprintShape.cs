using System;
using UnityEngine;

namespace ColInt.MMD.Editor.Physics {
	[Serializable]
	internal struct MmdSampleWorldPhysicsFingerprintShape {
		public Vector3 Center;
		public Vector3 Size;
		public float Radius;
		public float Height;
		public int Direction;
	}
}
