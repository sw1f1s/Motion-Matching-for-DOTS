using System.Runtime.CompilerServices;
using ColInt.MMD.Samples.Character.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Movement.Shared.Components;

namespace ColInt.MMD.Samples.Character.Movement.LocomotionState.Helpers {
	public static class MmdSampleGait {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Speed(in MmdSampleCharacterControl control, in MmdSampleInput input) {
			return input.Sprint ? control.SprintSpeed : input.Run ? control.RunSpeed : control.WalkSpeed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Speed(in MmdSampleMovementSettings settings, in MmdSampleInput input) {
			return input.Sprint ? settings.SprintSpeed : input.Run ? settings.RunSpeed : settings.WalkSpeed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Tags(in MmdSampleCharacterControl control, in MmdSampleInput input) {
			return input.Sprint ? control.SprintTags : input.Run ? control.RunTags : control.WalkTags;
		}
	}
}
