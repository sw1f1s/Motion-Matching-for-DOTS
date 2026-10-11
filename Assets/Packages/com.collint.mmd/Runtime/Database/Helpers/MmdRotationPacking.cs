using System.Runtime.CompilerServices;
using ColInt.MMD.Database.Data;
using Unity.Mathematics;

namespace ColInt.MMD.Database.Helpers {
	public static class MmdRotationPacking {
		public const float DefaultMaximumErrorDegrees = 0.05f;
		private const float QuantizationScale = 46339.5358f;
		private const float InverseQuantizationScale = 1f / QuantizationScale;
		private const ulong PayloadMask = (1UL << 51) - 1;

		public static MotionPackedRotation Pack(quaternion rotation) {
			float4 value = math.normalize(rotation.value);
			int largest = 0;
			for (int axis = 1; axis < 4; axis++) {
				if (math.abs(value[axis]) > math.abs(value[largest])) {
					largest = axis;
				}
			}
			ulong packed = (ulong)largest << 48;
			if (value[largest] < 0f) {
				packed |= 1UL << 50;
			}
			int shift = 0;
			for (int axis = 0; axis < 4; axis++) {
				if (axis == largest) {
					continue;
				}
				int quantized = (int)math.round(value[axis] * QuantizationScale);
				packed |= (ulong)unchecked((ushort)(short)quantized) << shift;
				shift += 16;
			}
			return new MotionPackedRotation { Value = packed };
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static quaternion Unpack(MotionPackedRotation rotation) {
			ulong packed = rotation.Value;
			float3 components = new float3(
				unchecked((short)(ushort)packed), unchecked((short)(ushort)(packed >> 16)), unchecked((short)(ushort)(packed >> 32))) * InverseQuantizationScale;
			float missing = math.sqrt(math.max(0f, 1f - math.lengthsq(components)));
			if ((packed & (1UL << 50)) != 0) {
				missing = -missing;
			}
			switch ((int)((packed >> 48) & 3)) {
				case 0:
					return new quaternion(missing, components.x, components.y, components.z);
				case 1:
					return new quaternion(components.x, missing, components.y, components.z);
				case 2:
					return new quaternion(components.x, components.y, missing, components.z);
				default:
					return new quaternion(components.x, components.y, components.z, missing);
			}
		}

		public static bool IsValid(MotionPackedRotation rotation) {
			ulong packed = rotation.Value;
			if ((packed & ~PayloadMask) != 0 || unchecked((short)(ushort)packed) == short.MinValue ||
				unchecked((short)(ushort)(packed >> 16)) == short.MinValue || unchecked((short)(ushort)(packed >> 32)) == short.MinValue) {
				return false;
			}
			float4 value = Unpack(rotation).value;
			float largest = math.abs(value[(int)((packed >> 48) & 3)]);
			// Quantization can move a tied component just beyond the omitted component.
			return math.abs(math.lengthsq(value) - 1f) < 0.000001f && math.cmax(math.abs(value)) <= largest + 4f * InverseQuantizationScale;
		}

		public static float ErrorDegrees(quaternion original, quaternion restored) {
			float4 a = math.normalize(original.value);
			float4 b = math.normalize(restored.value);
			if (math.dot(a, b) < 0f) {
				b = -b;
			}
			// Chord length remains accurate for tiny angles, unlike acos(dot) in float precision.
			return math.degrees(4f * math.asin(math.min(1f, math.length(a - b) * 0.5f)));
		}
	}
}
