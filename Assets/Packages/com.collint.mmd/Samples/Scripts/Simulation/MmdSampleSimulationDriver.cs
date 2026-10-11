using System;
using ColInt.MMD.Samples.Camera;
using ColInt.MMD.Samples.Character.Input.Components;
using ColInt.MMD.Samples.Character.Input.Helpers;
using ColInt.MMD.Samples.Lifecycle;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine;

namespace ColInt.MMD.Samples.Simulation {
	// Demonstrates host ticks/input. Render systems read the character's pose directly.
	public sealed class MmdSampleSimulationDriver : MonoBehaviour {
		[SerializeField] private bool _readPlayerInput = true;
		[SerializeField, Min(1)] private int _maximumTicksPerFrame = 15;
		private MmdSampleWorld _activeWorld;
		private MmdSampleThirdPersonCamera _playerCamera;
		private float _accumulator;
		private bool _paused;
		private bool _pendingJump;

		public void Init(MmdSampleWorld sample) {
			Init(sample, null);
		}

		public void Init(MmdSampleWorld sample, MmdSampleThirdPersonCamera playerCamera) {
			if (sample == null) {
				throw new ArgumentNullException(nameof(sample));
			}
			Reset();
			_activeWorld = sample;
			_playerCamera = playerCamera;
		}

		public void Reset() {
			if (_activeWorld != null && _activeWorld.World.IsCreated) {
				_activeWorld.SetInput(new MmdSampleInput());
				_activeWorld.SetPaused(false);
			}
			_activeWorld = null;
			_playerCamera = null;
			_accumulator = 0f;
			_paused = false;
			_pendingJump = false;
		}

		private void Update() {
			if (_activeWorld == null) {
				return;
			}
			MmdSampleWorld sample = _activeWorld;
			Keyboard keyboard = Keyboard.current;
			if (Application.isFocused && keyboard != null && keyboard.pKey.wasPressedThisFrame) {
				_paused = !_paused;
				sample.SetPaused(_paused);
			}
			if (_paused || !Application.isFocused) {
				_pendingJump = false;
			}
			var input = new MmdSampleInput();
			if (_readPlayerInput && Application.isFocused && !_paused) {
				if (_playerCamera != null && _playerCamera.isActiveAndEnabled && _playerCamera.IsInitialized) {
					Vector3 forward = _playerCamera.PlanarRotation * Vector3.forward;
					input.Facing = new float2(forward.x, forward.z);
				}
			}
			if (_readPlayerInput && Application.isFocused && keyboard != null && !_paused) {
				input = MmdSampleInputReader.Read(keyboard, input.Facing);
				_pendingJump |= input.JumpPressed;
				input.JumpPressed = _pendingJump;
			}
			sample.SetInput(input);
			_accumulator = math.min(_accumulator + Time.unscaledDeltaTime, sample.Timestep * math.max(1, _maximumTicksPerFrame));
			while (_accumulator >= sample.Timestep) {
				sample.Tick();
				_pendingJump = false;
				_accumulator -= sample.Timestep;
			}
		}

		private void OnDisable() {
			Reset();
		}
	}
}
