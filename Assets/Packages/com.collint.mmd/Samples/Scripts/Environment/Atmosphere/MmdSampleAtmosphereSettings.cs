using UnityEngine;

namespace ColInt.MMD.Samples.Environment.Atmosphere {
	[CreateAssetMenu(menuName = "Motion Matching for DOTS/Sample/Atmosphere Settings")]
	public sealed class MmdSampleAtmosphereSettings : ScriptableObject {
		[Header("Sky and sun")]
		[SerializeField] private Color _zenithColor = new Color(.12f, .34f, .65f);
		[SerializeField] private Color _horizonColor = new Color(.57f, .72f, .83f);
		[SerializeField] private Color _sunColor = new Color(1f, .91f, .72f);
		[Range(.1f, 3f)] [SerializeField] private float _sunRadius = .65f;
		[Range(0f, 12f)] [SerializeField] private float _sunBrightness = 4f;

		[Header("Volumetric Perlin clouds")]
		[SerializeField] private Texture3D _perlinNoise;
		[Range(0f, 1f)] [SerializeField] private float _cloudCoverage = .40f;
		[Range(0f, 3f)] [SerializeField] private float _cloudDensity = 1f;
		[Min(10f)] [SerializeField] private float _cloudBase = 600f;
		[Min(10f)] [SerializeField] private float _cloudThickness = 300f;
		[Min(50f)] [SerializeField] private float _cloudScale = 2000f;
		[SerializeField] private Vector2 _wind = new Vector2(7f, 2.5f);
		[SerializeField] private Color _cloudColor = new Color(.93f, .95f, 1f);
		[SerializeField] private Color _cloudShadowColor = new Color(.28f, .38f, .51f);
		[Range(16, 64)] [SerializeField] private int _cloudSteps = 36;

		[Header("Volumetric height fog")]
		[Range(0f, .01f)] [SerializeField] private float _fogDensity = .00085f;
		[SerializeField] private Color _fogColor = new Color(.57f, .69f, .74f);
		[SerializeField] private float _fogBaseHeight = 32f;
		[Min(1f)] [SerializeField] private float _fogHeight = 75f;
		[Min(1f)] [SerializeField] private float _fogNoiseScale = 180f;
		[Range(0f, 1f)] [SerializeField] private float _fogNoiseStrength = .55f;
		[Min(1f)] [SerializeField] private float _fogDistance = 1400f;
		[Range(8, 48)] [SerializeField] private int _fogSteps = 24;
		[Range(0f, 1f)] [SerializeField] private float _fogLightScattering = .55f;

		[Header("Sun rays")]
		[SerializeField, Range(0f, 6f)] private float _sunRayStrength = 1.8f;
		[SerializeField, Range(0f, .85f)] private float _sunRayAnisotropy = .78f;
		[SerializeField, Min(20f)] private float _sunRayDistance = 650f;
		[SerializeField, Range(0f, 1f)] private float _cloudShadowStrength = 1f;

		[Header("PSX style and cost")]
		[Tooltip("Only atmosphere is raymarched at this height; scene geometry retains its resolution.")]
		[Range(120, 540)] [SerializeField] private int _pixelHeight = 270;
		[Range(8, 128)] [SerializeField] private int _colorSteps = 48;
		[Range(0f, 1f)] [SerializeField] private float _dither = .35f;
		[Tooltip("-1 animates normally. A fixed time is useful for art comparison screenshots.")]
		[SerializeField] private float _previewTime = -1f;

		public Color ZenithColor => _zenithColor;
		public Color HorizonColor => _horizonColor;
		public Color SunColor => _sunColor;
		public float SunRadius => _sunRadius;
		public float SunBrightness => _sunBrightness;
		public Texture3D PerlinNoise => _perlinNoise;
		public float CloudCoverage => _cloudCoverage;
		public float CloudDensity => _cloudDensity;
		public float CloudBase => _cloudBase;
		public float CloudThickness => _cloudThickness;
		public float CloudScale => _cloudScale;
		public Vector2 Wind => _wind;
		public Color CloudColor => _cloudColor;
		public Color CloudShadowColor => _cloudShadowColor;
		public int CloudSteps => _cloudSteps;
		public float FogDensity => _fogDensity;
		public Color FogColor => _fogColor;
		public float FogBaseHeight => _fogBaseHeight;
		public float FogHeight => _fogHeight;
		public float FogNoiseScale => _fogNoiseScale;
		public float FogNoiseStrength => _fogNoiseStrength;
		public float FogDistance => _fogDistance;
		public int FogSteps => _fogSteps;
		public float FogLightScattering => _fogLightScattering;
		public int PixelHeight => _pixelHeight;
		public int ColorSteps => _colorSteps;
		public float Dither => _dither;
		public float PreviewTime => _previewTime;

		public float SunRayStrength => _sunRayStrength;
		public float SunRayAnisotropy => _sunRayAnisotropy;
		public float SunRayDistance => _sunRayDistance;
		public float CloudShadowStrength => _cloudShadowStrength;
	}
}
