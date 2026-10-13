using ColInt.MMD.Lifecycle.Components;
using ColInt.MMD.Samples.Character.Input.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColInt.MMD.Samples.Character.Input.Systems {
	// Runs once per rendered frame before the host driver; presses persist until a simulation tick consumes them.
	[DisableAutoCreation]
	[UpdateInGroup(typeof(MmdSamplePlayerInputGroup), OrderLast = true)]
	public partial class MmdSamplePlayerInputSystem : SystemBase {
		private EntityQuery _players;
		private InputSystemActions _actions;
		private bool _jumpPressed;
		private bool _crouchPressed;
		private bool _runPressed;
		private bool _pausePressed;
		private bool _releaseCursorPressed;
		private bool _captureCursorPressed;

		protected override void OnCreate() {
			_players = new EntityQueryBuilder(Allocator.Temp).WithAll<MmdSamplePlayerInput, MmdSampleInput>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(this);
			RequireForUpdate(_players);
			RequireForUpdate<MmdWorldState>();
		}

		protected override void OnStartRunning() {
			if (_actions == null) {
				_actions = new InputSystemActions();
				_actions.Player.Jump.performed += OnJump;
				_actions.Player.Crouch.performed += OnCrouch;
				_actions.Player.ToggleRun.performed += OnToggleRun;
				_actions.Player.Pause.performed += OnPause;
				_actions.Player.ReleaseCursor.performed += OnReleaseCursor;
				_actions.Player.CaptureCursor.performed += OnCaptureCursor;
			}
			_actions.Player.Enable();
		}

		protected override void OnStopRunning() {
			if (_actions != null) {
				_actions.Player.Disable();
			}
			ClearPresses();
		}

		protected override void OnUpdate() {
			bool focused = Application.isFocused || InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;
			var worldState = SystemAPI.GetSingletonRW<MmdWorldState>();
			if (focused && _pausePressed) {
				worldState.ValueRW.IsPaused = !worldState.ValueRO.IsPaused;
			}
			bool paused = worldState.ValueRO.IsPaused;
			float2 localMovement = math.normalizesafe((float2)_actions.Player.Move.ReadValue<Vector2>());
			float2 look = _actions.Player.Look.ReadValue<Vector2>();
			bool sprint = _actions.Player.Sprint.IsPressed();
			var disabled = SystemAPI.GetComponentLookup<Disabled>(true);
			var initialized = SystemAPI.GetComponentLookup<MmdSamplePlayerInputInitialized>(true);
			using var commands = new EntityCommandBuffer(Allocator.Temp);
			foreach (var (player, input, entity) in SystemAPI.Query<RefRO<MmdSamplePlayerInput>, RefRW<MmdSampleInput>>()
				.WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess()) {
				bool connected = initialized.HasComponent(entity);
				bool releaseCursor = connected && _releaseCursorPressed;
				bool captureCursor = connected && _captureCursorPressed;
				if (!connected) {
					commands.AddComponent<MmdSamplePlayerInputInitialized>(entity);
				}
				bool active = focused && !disabled.HasComponent(entity);
				var camera = player.ValueRO.Camera.Value;
				float2 facing = float2.zero;
				if (active && camera != null && camera.isActiveAndEnabled && camera.IsInitialized) {
					camera.ApplyInput(look, releaseCursor, captureCursor);
					Vector3 forward = camera.PlanarRotation * Vector3.forward;
					facing = new float2(forward.x, forward.z);
				}
				if (!active || paused) {
					input.ValueRW.Movement = float2.zero;
					input.ValueRW.Sprint = false;
					input.ValueRW.JumpPressed = false;
					input.ValueRW.CrouchPressed = false;
					continue;
				}
				input.ValueRW.Facing = facing;
				float2 movementForward = math.normalizesafe(facing, new float2(0f, 1f));
				input.ValueRW.Movement = new float2(movementForward.y, -movementForward.x) * localMovement.x + movementForward * localMovement.y;
				input.ValueRW.Run ^= connected && _runPressed;
				input.ValueRW.Sprint = sprint;
				input.ValueRW.Crouch = !sprint && (connected && _crouchPressed ? !input.ValueRO.Crouch : input.ValueRO.Crouch);
				input.ValueRW.CrouchPressed |= connected && _crouchPressed;
				input.ValueRW.JumpPressed |= connected && _jumpPressed;
			}
			if (!commands.IsEmpty) {
				commands.Playback(EntityManager);
			}
			ClearPresses();
		}

		protected override void OnDestroy() {
			if (_actions == null) {
				return;
			}
			_actions.Player.Disable();
			_actions.Player.Jump.performed -= OnJump;
			_actions.Player.Crouch.performed -= OnCrouch;
			_actions.Player.ToggleRun.performed -= OnToggleRun;
			_actions.Player.Pause.performed -= OnPause;
			_actions.Player.ReleaseCursor.performed -= OnReleaseCursor;
			_actions.Player.CaptureCursor.performed -= OnCaptureCursor;
#if UNITY_EDITOR
			if (!Application.isPlaying) {
				Object.DestroyImmediate(_actions.asset);
			} else {
				_actions.Dispose();
			}
#else
			_actions.Dispose();
#endif
			_actions = null;
		}

		private void ClearPresses() {
			_jumpPressed = false;
			_crouchPressed = false;
			_runPressed = false;
			_pausePressed = false;
			_releaseCursorPressed = false;
			_captureCursorPressed = false;
		}

		private void OnJump(InputAction.CallbackContext context) {
			_jumpPressed = true;
		}

		private void OnCrouch(InputAction.CallbackContext context) {
			_crouchPressed = true;
		}

		private void OnToggleRun(InputAction.CallbackContext context) {
			_runPressed = true;
		}

		private void OnPause(InputAction.CallbackContext context) {
			_pausePressed = true;
		}

		private void OnReleaseCursor(InputAction.CallbackContext context) {
			_releaseCursorPressed = true;
		}

		private void OnCaptureCursor(InputAction.CallbackContext context) {
			_captureCursorPressed = true;
		}
	}
}
