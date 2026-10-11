using UnityEngine;
using UnityEngine.InputSystem;

namespace ColInt.MMD.Samples.Camera {
	[RequireComponent(typeof(UnityEngine.Camera))]
	public sealed class MmdSampleFreeCamera : MonoBehaviour {
		[SerializeField, Min(0.1f)] private float _speed = 15f;
		[SerializeField, Min(1f)] private float _fastMultiplier = 4f;
		[SerializeField, Min(0f)] private float _mouseSensitivity = 0.15f;
		private CursorLockMode _originalCursorLock;
		private bool _originalCursorVisible;
		private Vector3 _originalPosition;
		private Quaternion _originalRotation;
		private float _yaw;
		private float _pitch;

		public bool IsInitialized { get; private set; }

		public void Init() {
			Reset();
			_originalPosition = transform.position;
			_originalRotation = transform.rotation;
			_originalCursorLock = Cursor.lockState;
			_originalCursorVisible = Cursor.visible;
			_yaw = transform.eulerAngles.y;
			_pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
			IsInitialized = true;
			if (Application.isPlaying && Application.isFocused) {
				CaptureCursor();
			}
		}

		public void Reset() {
			if (!IsInitialized) {
				return;
			}
			Cursor.lockState = _originalCursorLock;
			Cursor.visible = _originalCursorVisible;
			transform.SetPositionAndRotation(_originalPosition, _originalRotation);
			IsInitialized = false;
		}

		private void Update() {
			if (!IsInitialized || !Application.isFocused) {
				return;
			}
			var keyboard = Keyboard.current;
			var mouse = Mouse.current;
			if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) {
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
			if (Cursor.lockState != CursorLockMode.Locked) {
				if (mouse != null && mouse.leftButton.wasPressedThisFrame) {
					CaptureCursor();
				}
				return;
			}
			if (mouse != null) {
				Vector2 delta = mouse.delta.ReadValue();
				_yaw = Mathf.Repeat(_yaw + delta.x * _mouseSensitivity, 360f);
				_pitch = Mathf.Clamp(_pitch - delta.y * _mouseSensitivity, -89f, 89f);
				transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
			}
			if (keyboard == null) {
				return;
			}
			Vector3 movement = new Vector3((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
				(keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f),
				(keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
			float speed = _speed * (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? _fastMultiplier : 1f);
			Vector3 direction = transform.right * movement.x + Vector3.up * movement.y + transform.forward * movement.z;
			transform.position += Vector3.ClampMagnitude(direction, 1f) * speed * Time.unscaledDeltaTime;
		}

		private void OnDisable() {
			Reset();
		}

		private static void CaptureCursor() {
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}
}
