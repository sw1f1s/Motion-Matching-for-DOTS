using System.Runtime.CompilerServices;
using ColInt.MMD.Samples.Character.Input.Components;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace ColInt.MMD.Samples.Character.Input.Helpers {
	public static class MmdSampleInputReader {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MmdSampleInput Read(Keyboard keyboard, float2 facing, bool crouching, bool running) {
			float2 movement = math.normalizesafe(new float2(
				(keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
				(keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)));
			if (math.lengthsq(facing) > 0f) {
				float2 forward = math.normalizesafe(facing);
				movement = new float2(forward.y, -forward.x) * movement.x + forward * movement.y;
			}
			bool crouchPressed = keyboard.leftCtrlKey.wasPressedThisFrame;
			bool sprintPressed = keyboard.leftShiftKey.wasPressedThisFrame;
			return new MmdSampleInput {
				Movement = movement,
				Facing = facing,
				Run = keyboard.xKey.wasPressedThisFrame ? !running : running,
				Sprint = keyboard.leftShiftKey.isPressed,
				CrouchPressed = crouchPressed,
				Crouch = crouchPressed ? !crouching : !sprintPressed && crouching,
				JumpPressed = keyboard.spaceKey.wasPressedThisFrame
			};
		}
	}
}
