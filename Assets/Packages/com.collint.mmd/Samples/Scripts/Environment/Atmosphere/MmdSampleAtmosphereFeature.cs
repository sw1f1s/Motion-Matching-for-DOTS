using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ColInt.MMD.Samples.Environment.Atmosphere {
	public sealed class MmdSampleAtmosphereFeature : ScriptableRendererFeature {
		private MmdSampleAtmospherePass _pass;

		public override void Create() {
			_pass = new MmdSampleAtmospherePass();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
			var atmosphere = MmdSampleAtmosphere.Active;
			var type = renderingData.cameraData.cameraType;
			if (atmosphere == null || !atmosphere.isActiveAndEnabled || atmosphere.Settings == null || atmosphere.VolumeMaterial == null || type == CameraType.Preview || type == CameraType.Reflection) {
				return;
			}
			_pass.Material = atmosphere.VolumeMaterial;
			_pass.PixelHeight = atmosphere.Settings.PixelHeight;
			renderer.EnqueuePass(_pass);
		}

		protected override void Dispose(bool disposing) {
			_pass = null;
		}
	}
}
