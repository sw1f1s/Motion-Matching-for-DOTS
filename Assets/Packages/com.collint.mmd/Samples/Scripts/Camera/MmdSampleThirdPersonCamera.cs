using System.Runtime.CompilerServices;
using System;
using ColInt.MMD.Samples.Character.Spawning;
using ColInt.MMD.Samples.Lifecycle;
using Unity.Transforms;
using UnityEngine.InputSystem;
using UnityEngine;

namespace ColInt.MMD.Samples.Camera {
	[RequireComponent(typeof(UnityEngine.Camera))]
	[DefaultExecutionOrder(-100)]
	public sealed class MmdSampleThirdPersonCamera : MonoBehaviour {
		[SerializeField, Min(0.1f)] private float _distance = 4f;
		[SerializeField] private float _height = 1.4f;
		[SerializeField, Min(0f)] private float _mouseSensitivity = 0.15f;
		[SerializeField, Range(-20f, 70f)] private float _initialPitch = 20f;
		private MmdSampleWorld _sample;
		private MmdSampleCharacterSpawner _player;
		private float _yaw;
		private float _pitch;
		private Vector3 _originalPosition;
		private Quaternion _originalRotation;
		private CursorLockMode _originalCursorLock;
		private bool _originalCursorVisible;

		public Quaternion PlanarRotation => Quaternion.Euler(0f, _yaw, 0f);
		public bool IsInitialized => _sample != null;

		public void Init(MmdSampleWorld sample, MmdSampleCharacterSpawner player) {
			if (sample == null) {
				throw new ArgumentNullException(nameof(sample));
			}
			if (player == null || !player.IsPlayerControlled) {
				throw new ArgumentException("The camera requires a player-controlled spawner.", nameof(player));
			}
			Reset();
			_originalPosition = transform.position;
			_originalRotation = transform.rotation;
			_originalCursorLock = Cursor.lockState;
			_originalCursorVisible = Cursor.visible;
			_sample = sample;
			_player = player;
			LocalTransform pose = sample.World.EntityManager.GetComponentData<LocalTransform>(player.CharacterEntity);
			_yaw = ((Quaternion)pose.Rotation).eulerAngles.y;
			_pitch = _initialPitch;
			Follow(pose);
			if (Application.isPlaying && Application.isFocused) {
				CaptureCursor();
			}
		}

		public void Reset() {
			if (_sample == null) {
				return;
			}
			Cursor.lockState = _originalCursorLock;
			Cursor.visible = _originalCursorVisible;
			transform.SetPositionAndRotation(_originalPosition, _originalRotation);
			_sample = null;
			_player = null;
			_yaw = 0f;
			_pitch = 0f;
		}

		private void Update() {
			if (_sample == null) {
				return;
			}
			Mouse mouse = Mouse.current;
			Keyboard keyboard = Keyboard.current;
			if (Application.isFocused && mouse != null) {
				if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) {
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				} else if (Cursor.lockState != CursorLockMode.Locked) {
					if (mouse.leftButton.wasPressedThisFrame) {
						CaptureCursor();
					}
				} else {
					Vector2 delta = mouse.delta.ReadValue();
					_yaw = Mathf.Repeat(_yaw + delta.x * _mouseSensitivity, 360f);
					_pitch = Mathf.Clamp(_pitch - delta.y * _mouseSensitivity, -20f, 70f);
				}
			}
		}

		private void LateUpdate() {
			if (_sample == null) {
				return;
			}
			if (!_sample.World.IsCreated || _player == null || !_player.IsPlayerControlled || !_sample.World.EntityManager.Exists(_player.CharacterEntity)) {
				Reset();
				return;
			}
			Follow(_sample.World.EntityManager.GetComponentData<LocalTransform>(_player.CharacterEntity));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Follow(in LocalTransform pose) {
			Vector3 pivot = (Vector3)pose.Position + Vector3.up * _height;
			Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
			transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * _distance, rotation);
		}

		private void OnDisable() {
			Reset();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void CaptureCursor() {
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}
}
