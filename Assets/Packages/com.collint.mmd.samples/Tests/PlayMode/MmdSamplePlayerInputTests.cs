using System.Reflection;
using ColInt.MMD.Samples.Camera;
using ColInt.MMD.Samples.Character.Configurations;
using ColInt.MMD.Samples.Character.Spawning;
using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Input.Systems;
using ColInt.MMD.Samples.Lifecycle;
using ColInt.MMD.Samples.Simulation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace ColInt.MMD.Tests {
	public class MmdSamplePlayerInputTests {
		private MmdSampleWorld _sample;
		private Entity _player;
		private Keyboard _keyboard;
		private Keyboard _previousKeyboard;
		private InputSettings.BackgroundBehavior _previousBackgroundBehavior;
		private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInputBehavior;
		private MmdSamplePlayerInputGroup _inputGroup;

		[SetUp]
		public void SetUp() {
			_previousKeyboard = Keyboard.current;
			_previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
			_previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
			InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
			InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
			_keyboard = InputSystem.AddDevice<Keyboard>();
			_sample = new MmdSampleWorld(1f / 60f);
			_player = _sample.World.EntityManager.CreateEntity(typeof(MmdSamplePlayerInput));
			_inputGroup = _sample.World.GetExistingSystemManaged<MmdSamplePlayerInputGroup>();
			_inputGroup.Update();
		}

		[TearDown]
		public void TearDown() {
			_sample.Dispose();
			InputSystem.RemoveDevice(_keyboard);
			InputSystem.settings.backgroundBehavior = _previousBackgroundBehavior;
			InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInputBehavior;
			if (_previousKeyboard != null && _previousKeyboard.added) {
				_previousKeyboard.MakeCurrent();
			}
		}

		[Test]
		public void MarkerCreatesEntityInputAndActionsDriveNormalizedMovementAndGaitToggle() {
			Assert.That(Read().Run, Is.True);
			Assert.That(Read().Facing, Is.EqualTo(float2.zero), "Without a camera, idle Traversal uses the character heading.");
			Send(Key.W, Key.D);
			Assert.That(math.distance(Read().Movement, math.normalize(new float2(1f, 1f))), Is.LessThan(0.00001f));
			Send(Key.W, Key.X);
			Assert.That(Read().Run, Is.False);
			_inputGroup.Update();
			Assert.That(Read().Run, Is.False, "Repeated capture must not replay the same action press.");
			Send(Key.W, Key.LeftShift);
			Assert.That(Read().Sprint, Is.True);
			Assert.That(Read().Run, Is.False);
			Send(Key.W);
			Assert.That(Read().Sprint, Is.False);
			Send(Key.W, Key.X);
			Assert.That(Read().Run, Is.True);
		}

		[Test]
		public void PressesSurviveFramesWithoutSimulationAndAreConsumedOnlyOnce() {
			Send(Key.Space, Key.LeftCtrl);
			Assert.That(Read().JumpPressed, Is.True);
			Assert.That(Read().CrouchPressed, Is.True);
			Assert.That(Read().Crouch, Is.True);
			Send();
			_inputGroup.Update();
			Assert.That(Read().JumpPressed, Is.True);
			Assert.That(Read().CrouchPressed, Is.True);
			_sample.Tick();
			Assert.That(Read().JumpPressed, Is.False);
			Assert.That(Read().CrouchPressed, Is.False);
			_inputGroup.Update();
			Assert.That(Read().JumpPressed, Is.False);
			Assert.That(Read().Crouch, Is.True);
		}

		[Test]
		public void HeldShiftKeepsStandingIntentButPreservesTheSlidePress() {
			Send(Key.LeftCtrl);
			Assert.That(Read().Crouch, Is.True);
			Send(Key.LeftShift);
			Assert.That(Read().Crouch, Is.False);
			_sample.Tick();
			Send(Key.LeftShift, Key.LeftCtrl);
			Assert.That(Read().Crouch, Is.False);
			Assert.That(Read().CrouchPressed, Is.True);
			Assert.That(Read().Sprint, Is.True);
			_sample.Tick();
			Send(Key.LeftShift, Key.LeftCtrl);
			Assert.That(Read().CrouchPressed, Is.False);
			Send();
			Assert.That(Read().Crouch, Is.False);
			Send(Key.LeftCtrl);
			Assert.That(Read().Crouch, Is.True);
		}

		[Test]
		public void EachPlayerKeepsItsOwnToggleAndBotsKeepTheirOwnInput() {
			var manager = _sample.World.EntityManager;
			Entity otherPlayer = manager.CreateEntity(typeof(MmdSamplePlayerInput), typeof(MmdSampleInput));
			Entity bot = manager.CreateEntity(typeof(MmdSampleInput));
			var botInput = new MmdSampleInput { Movement = new float2(-1f, 0f), Run = true };
			manager.SetComponentData(bot, botInput);
			_inputGroup.Update();
			Send(Key.X, Key.LeftCtrl);
			Assert.That(Read().Run, Is.False);
			Assert.That(manager.GetComponentData<MmdSampleInput>(otherPlayer).Run, Is.True);
			Assert.That(manager.GetComponentData<MmdSampleInput>(otherPlayer).Crouch, Is.True);
			var input = Read();
			input.Crouch = false;
			manager.SetComponentData(_player, input);
			_inputGroup.Update();
			Assert.That(Read().Crouch, Is.False, "State logic can consume its entity's toggle without a driver cache restoring it.");
			Assert.That(manager.GetComponentData<MmdSampleInput>(otherPlayer).Crouch, Is.True);
			Assert.That(manager.GetComponentData<MmdSampleInput>(bot), Is.EqualTo(botInput));
		}

		[Test]
		public void PauseAndDisabledDiscardPressesWithoutReplayingThemOnResume() {
			Send(Key.P, Key.Space, Key.W);
			using (var state = _sample.World.EntityManager.CreateEntityQuery(typeof(MmdWorldState))) {
				Assert.That(state.GetSingleton<MmdWorldState>().IsPaused, Is.True);
			}
			Assert.That(Read().JumpPressed, Is.False);
			Assert.That(Read().Movement, Is.EqualTo(float2.zero));
			Send();
			Send(Key.P);
			Assert.That(Read().JumpPressed, Is.False);
			_sample.World.EntityManager.AddComponent<Disabled>(_player);
			Send(Key.W, Key.Space, Key.LeftCtrl);
			Assert.That(Read().JumpPressed, Is.False);
			Assert.That(Read().CrouchPressed, Is.False);
			_sample.World.EntityManager.RemoveComponent<Disabled>(_player);
			_inputGroup.Update();
			Assert.That(Read().JumpPressed, Is.False);
			Assert.That(Read().Crouch, Is.False);
		}

		[Test]
		public void NewPlayerDoesNotReceivePressesMadeBeforeItsControlWasAttached() {
			Send(Key.Space, Key.LeftCtrl);
			Entity newPlayer = _sample.World.EntityManager.CreateEntity(typeof(MmdSamplePlayerInput));
			_inputGroup.Update();
			var input = _sample.World.EntityManager.GetComponentData<MmdSampleInput>(newPlayer);
			Assert.That(input.JumpPressed, Is.False);
			Assert.That(input.CrouchPressed, Is.False);
			Assert.That(input.Crouch, Is.False);
			Assert.That(input.Run, Is.True);
		}

		[Test]
		public void SimulationDriverInitAndResetDoNotModifyCharacterInput() {
			var input = new MmdSampleInput { Crouch = true, Run = false, JumpPressed = true };
			_sample.World.EntityManager.SetComponentData(_player, input);
			var owner = new GameObject("Sample simulation driver");
			try {
				var driver = owner.AddComponent<MmdSampleSimulationDriver>();
				driver.Init(_sample);
				Assert.That(Read(), Is.EqualTo(input));
				driver.Reset();
				Assert.That(Read(), Is.EqualTo(input));
			} finally {
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void ActionsTransformMovementByTheAttachedCamerasPlanarHeading() {
			var configuration = ScriptableObject.CreateInstance<MmdSampleCharacterConfiguration>();
			var owner = new GameObject("Camera-relative input");
			try {
				owner.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
				var spawner = owner.AddComponent<MmdSampleCharacterSpawner>();
				typeof(MmdSampleCharacterSpawner).GetField("_configuration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawner, configuration);
				spawner.Init(_sample);
				var camera = owner.AddComponent<MmdSampleThirdPersonCamera>();
				camera.Init(_sample, spawner);
				_sample.World.EntityManager.DestroyEntity(_player);
				_player = spawner.CharacterEntity;
				_sample.World.EntityManager.SetComponentData(_player, new MmdSamplePlayerInput { Camera = camera });
				_inputGroup.Update();
				Send(Key.W);
				Assert.That(math.distance(Read().Movement, new float2(1f, 0f)), Is.LessThan(0.00001f));
				Send(Key.D);
				Assert.That(math.distance(Read().Movement, new float2(0f, -1f)), Is.LessThan(0.00001f));
				camera.Reset();
			} finally {
				Object.DestroyImmediate(owner);
				Object.DestroyImmediate(configuration);
			}
		}

		[Test]
		public void WorldDisposalDisablesTheActionsOwnedByItsInputSystem() {
			int enabled = InputSystem.ListEnabledActions().Count;
			_sample.Dispose();
			Assert.That(InputSystem.ListEnabledActions().Count, Is.LessThan(enabled));
			InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space, Key.LeftCtrl));
			InputSystem.Update();
		}

		[Test]
		public void RemovingTheControlMarkerDisablesItsActionsAndPreservesNewBotInput() {
			Send(Key.W);
			_sample.World.EntityManager.RemoveComponent<MmdSamplePlayerInput>(_player);
			var input = new MmdSampleInput { Movement = new float2(1f, 0f) };
			_sample.World.EntityManager.SetComponentData(_player, input);
			_inputGroup.Update();
			Send(Key.S);
			Assert.That(Read(), Is.EqualTo(input));
		}

		private MmdSampleInput Read() {
			return _sample.World.EntityManager.GetComponentData<MmdSampleInput>(_player);
		}

		private void Send(params Key[] keys) {
			InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
			InputSystem.Update();
			_inputGroup.Update();
		}
	}
}
