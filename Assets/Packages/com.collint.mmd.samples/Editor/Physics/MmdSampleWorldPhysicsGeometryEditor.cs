using System.Threading;
using System;
using ColInt.MMD.Samples.Physics;
using System.Threading.Tasks;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace ColInt.MMD.Editor.Physics {
	[CustomEditor(typeof(MmdSampleWorldPhysicsGeometry))]
	public sealed class MmdSampleWorldPhysicsGeometryEditor : UnityEditor.Editor {
		private CancellationTokenSource _lifetime;
		private Label _status;

		public override VisualElement CreateInspectorGUI() {
			var root = new VisualElement();
			InspectorElement.FillDefaultInspector(root, serializedObject, this);
			_status = new Label();
			root.Add(_status);
			root.Add(new Button(StartBake) { text = "Bake World Physics" });
			RefreshStatus();
			return root;
		}

		private void OnEnable() {
			_lifetime = new CancellationTokenSource();
		}

		private void OnDisable() {
			_lifetime.Cancel();
			_lifetime.Dispose();
		}

		private void StartBake() {
			_ = RunBakeAsync();
		}

		private async Task RunBakeAsync() {
			try {
				await MmdSampleWorldPhysicsBaker.BakeAsync((MmdSampleWorldPhysicsGeometry)target, _lifetime.Token);
				RefreshStatus();
			} catch (OperationCanceledException) {
			} catch (Exception exception) {
				Debug.LogException(exception, target);
			}
		}

		private void RefreshStatus() {
			var source = (MmdSampleWorldPhysicsGeometry)target;
			_status.text = source.HasCache ? "Cached physics bodies: " + source.Bodies.Count : "Bake the world colliders before entering Play Mode.";
		}
	}
}
