using UnityEngine;
using UnityEngine.Rendering;

namespace ColInt.MMD.Samples.Environment.Atmosphere {
	[ExecuteAlways, DisallowMultipleComponent]
	public sealed class MmdSampleAtmosphere : MonoBehaviour {
		[SerializeField] private MmdSampleAtmosphereSettings _settings;
		[SerializeField] private Material _atmosphereMaterial;
		[SerializeField] private Material _volumeMaterial;
		[SerializeField] private Light _sun;
		private Material _previousSky;
		private bool _previousFog;

		public MmdSampleAtmosphereSettings Settings => _settings;
		public Material AtmosphereMaterial => _atmosphereMaterial;
		public Material VolumeMaterial => _volumeMaterial;
		public Light Sun => _sun;
		public static MmdSampleAtmosphere Active { get; private set; }

		public void Apply() {
			if (Settings == null || AtmosphereMaterial == null || Settings.PerlinNoise == null) {
				return;
			}
			Active = this;
			RenderSettings.skybox = AtmosphereMaterial;
			RenderSettings.fog = false; // Avoid applying both URP distance fog and volume integration.
			var s = Settings;
			var sun = Sun != null ? Sun : RenderSettings.sun;
			Shader.SetGlobalFloat("_VAEnabled", 1);
			Shader.SetGlobalTexture("_VANoise", s.PerlinNoise);
			Shader.SetGlobalVector("_VASunDirection", sun != null ? -sun.transform.forward : Vector3.up);
			Shader.SetGlobalColor("_VASunColor", s.SunColor.linear);
			Shader.SetGlobalColor("_VAZenith", s.ZenithColor.linear);
			Shader.SetGlobalColor("_VAHorizon", s.HorizonColor.linear);
			Shader.SetGlobalColor("_VACloudColor", s.CloudColor.linear);
			Shader.SetGlobalColor("_VACloudShadow", s.CloudShadowColor.linear);
			Shader.SetGlobalColor("_VAFogColor", s.FogColor.linear);
			Shader.SetGlobalVector("_VACloudShape", new Vector4(s.CloudCoverage, s.CloudDensity, s.CloudBase, Mathf.Max(10, s.CloudThickness)));
			Shader.SetGlobalVector("_VACloudMotion", new Vector4(1 / Mathf.Max(50, s.CloudScale), s.Wind.x, s.Wind.y, s.PreviewTime >= 0 ? s.PreviewTime : (float)Time.realtimeSinceStartupAsDouble));
			Shader.SetGlobalVector("_VAFogShape", new Vector4(s.FogDensity, s.FogBaseHeight, Mathf.Max(1, s.FogHeight), s.FogDistance));
			Shader.SetGlobalVector("_VAFogDetail", new Vector4(1 / Mathf.Max(1, s.FogNoiseScale), s.FogNoiseStrength, s.FogLightScattering, s.FogSteps));
			Shader.SetGlobalVector("_VAStyle", new Vector4(s.CloudSteps, s.ColorSteps, s.Dither, s.PixelHeight));
			Shader.SetGlobalVector("_VASunRays", new Vector4(s.SunRayStrength, s.SunRayAnisotropy, s.SunRayDistance, s.CloudShadowStrength));
			Shader.SetGlobalVector("_VACloudShadowRegion", Vector4.zero);
			float sunElevation = sun != null ? -sun.transform.forward.y : 1;
			float sunIntensity = sun != null && sun.isActiveAndEnabled ? sun.intensity : 0;
			Shader.SetGlobalFloat("_VASunRayRadiance", sunIntensity * Mathf.SmoothStep(0, 1, Mathf.Clamp01(sunElevation / .12f)));
			Shader.SetGlobalVector("_VASunShape", new Vector4(Mathf.Cos(s.SunRadius * Mathf.Deg2Rad), s.SunBrightness, 0, 0));
		}

		private void OnEnable() {
			Active = this;
			_previousSky = RenderSettings.skybox;
			_previousFog = RenderSettings.fog;
			RenderPipelineManager.beginCameraRendering += BeginCamera;
			RenderPipelineManager.endCameraRendering += EndCamera;
			Apply();
		}

		private void OnDisable() {
			RenderPipelineManager.beginCameraRendering -= BeginCamera;
			RenderPipelineManager.endCameraRendering -= EndCamera;
			if (Active != this) {
				return;
			}
			Active = null;
			Shader.SetGlobalFloat("_VAEnabled", 0);
			Shader.SetGlobalVector("_VACloudShadowRegion", Vector4.zero);
			Shader.SetGlobalFloat("_VACloudsInSkybox", 1);
			if (RenderSettings.skybox == AtmosphereMaterial) {
				RenderSettings.skybox = _previousSky;
			}
			RenderSettings.fog = _previousFog;
		}

		private void Update() {
			Apply();
		}

		private void BeginCamera(ScriptableRenderContext context, Camera camera) {
			Apply();
			// Reflections have no screen-space pass; their skybox integrates clouds itself.
			Shader.SetGlobalFloat("_VACloudsInSkybox", camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview ? 1 : 0);
		}

		private void EndCamera(ScriptableRenderContext context, Camera camera) {
			Shader.SetGlobalFloat("_VACloudsInSkybox", 1);
		}
	}
}
