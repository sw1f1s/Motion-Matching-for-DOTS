using ColInt.MMD.Samples.Character.Input.Helpers;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Simulation;
using Unity.Entities;
using UnityEngine;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ColInt.MMD.Tests {
	public class MmdSampleInputReaderTests {
		[Test]
		public void WalkRunTogglePersistsAndSprintOnlyOverridesWhileShiftIsHeld() {
			var previous = Keyboard.current;
			var keyboard = InputSystem.AddDevice<Keyboard>();
			try {
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X));
				InputSystem.Update();
				var input = MmdSampleInputReader.Read(keyboard, float2.zero, false, false);
				Assert.That(input.Run, Is.True);
				Assert.That(input.Sprint, Is.False);
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, false, input.Run);
				Assert.That(input.Run, Is.True);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, false, input.Run);
				Assert.That(input.Run, Is.True);
				Assert.That(input.Sprint, Is.True);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState());
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, false, input.Run);
				Assert.That(input.Run, Is.True);
				Assert.That(input.Sprint, Is.False);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X, Key.LeftShift));
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, false, input.Run);
				Assert.That(input.Run, Is.False);
				Assert.That(input.Sprint, Is.True);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState());
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, false, input.Run);
				Assert.That(input.Run, Is.False);
				Assert.That(input.Sprint, Is.False);
			} finally {
				InputSystem.RemoveDevice(keyboard);
				if (previous != null && previous.added) {
					previous.MakeCurrent();
				}
			}
		}

		[Test]
		public void CrouchKeyTogglesOnPressAndKeepsItsIntentWhenHeldOrReleased() {
			var previous = Keyboard.current;
			var keyboard = InputSystem.AddDevice<Keyboard>();
			try {
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
				InputSystem.Update();
				var input = MmdSampleInputReader.Read(keyboard, float2.zero, false, false);
				Assert.That(input.Crouch, Is.True);
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, false);
				Assert.That(input.Crouch, Is.True, "Holding the key must not toggle again.");
				InputSystem.QueueStateEvent(keyboard, new KeyboardState());
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, false);
				Assert.That(input.Crouch, Is.True, "Releasing the key must keep crouch enabled.");
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, false);
				Assert.That(input.Crouch, Is.False, "The next press must request standing.");
			} finally {
				InputSystem.RemoveDevice(keyboard);
				if (previous != null && previous.added) {
					previous.MakeCurrent();
				}
			}
		}

		[Test]
		public void DriverPublishesRunByDefaultAndRestoresItAfterReset() {
			using (var sample = new MmdSampleWorld(1f / 60f)) {
				var owner = new GameObject("Sample input lifecycle");
				try {
					var driver = owner.AddComponent<MmdSampleSimulationDriver>();
					using (var query = sample.World.EntityManager.CreateEntityQuery(typeof(MmdSampleInput))) {
						driver.Init(sample);
						Assert.That(query.GetSingleton<MmdSampleInput>().Run, Is.True);
						sample.SetInput(new MmdSampleInput { Crouch = true });
						driver.Reset();
						Assert.That(query.GetSingleton<MmdSampleInput>().Crouch, Is.False);
						driver.Init(sample);
						Assert.That(query.GetSingleton<MmdSampleInput>().Run, Is.True);
						driver.Reset();
					}
				} finally {
					Object.DestroyImmediate(owner);
				}
			}
		}

		[Test]
		public void ShiftRequestsStandingButHoldingItDoesNotCancelANewSlidePress() {
			var previous = Keyboard.current;
			var keyboard = InputSystem.AddDevice<Keyboard>();
			try {
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
				InputSystem.Update();
				var input = MmdSampleInputReader.Read(keyboard, float2.zero, true, true);
				Assert.That(input.Crouch, Is.False);
				Assert.That(input.Sprint, Is.True);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.LeftCtrl));
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, input.Run);
				Assert.That(input.Crouch, Is.True);
				Assert.That(input.CrouchPressed, Is.True);
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, input.Run);
				Assert.That(input.Crouch, Is.True);
				Assert.That(input.CrouchPressed, Is.False);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState());
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, input.Run);
				Assert.That(input.Crouch, Is.True);
				InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X));
				InputSystem.Update();
				input = MmdSampleInputReader.Read(keyboard, float2.zero, input.Crouch, input.Run);
				Assert.That(input.Run, Is.False, "The first X press switches the default run mode to walk.");
			} finally {
				InputSystem.RemoveDevice(keyboard);
				if (previous != null && previous.added) {
					previous.MakeCurrent();
				}
			}
		}

	}
}
