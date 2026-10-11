#ifndef VILLAGE_ATMOSPHERE_COMMON
#define VILLAGE_ATMOSPHERE_COMMON

TEXTURE3D(_VANoise); SAMPLER(sampler_VANoise);
TEXTURE2D(_VACloudShadowTexture); SAMPLER(sampler_VACloudShadowTexture);
float4 _VACloudShadowRegion, _VASunRays;
float _VASunRayRadiance;
float _VAEnabled, _VACloudsInSkybox;
float4 _VAZenith, _VAHorizon, _VASunColor, _VACloudColor, _VACloudShadow, _VAFogColor;
float4 _VASunDirection, _VACloudShape, _VACloudMotion, _VAFogShape, _VAFogDetail, _VAStyle, _VASunShape;

float4 VANoise(float3 p) { return SAMPLE_TEXTURE3D_LOD(_VANoise, sampler_VANoise, p, 0); }
float VABayer(float2 pixel)
{
    uint2 p = uint2(pixel) & 3;
    uint a = ((p.x & 1) ^ (p.y & 1)) * 2 + (p.y & 1);
    uint b = (((p.x >> 1) & 1) ^ ((p.y >> 1) & 1)) * 2 + ((p.y >> 1) & 1);
    return (a * 4 + b + .5) / 16.0;
}
float3 VAQuantize(float3 color, float2 pixel)
{
    float levels = max(_VAStyle.y, 8);
    // Quantize display-like values, then return to linear HDR for ACES.
    float3 perceptual = sqrt(max(color, 0));
    return pow(max(0, floor(perceptual * levels + .5 + (VABayer(pixel) - .5) * _VAStyle.z) / levels), 2);
}
float3 VASky(float3 ray)
{
    float h = pow(saturate(ray.y), .45);
    float3 sky = lerp(_VAHorizon.rgb, _VAZenith.rgb, h);
    float sunDot = dot(ray, normalize(_VASunDirection.xyz));
    float disc = smoothstep(_VASunShape.x - .00007, _VASunShape.x + .00002, sunDot);
    float glow = pow(saturate(sunDot), 160) * .12;
    return sky + _VASunColor.rgb * (disc * _VASunShape.y + glow);
}
float VACloudDensity(float3 world)
{
    if (_VACloudShape.x <= .001 || _VACloudShape.y <= 0) return 0;
    float height = (world.y - _VACloudShape.z) / _VACloudShape.w;
    float vertical = smoothstep(0, .15, height) * (1 - smoothstep(.55, 1, height));
    float3 drift = float3(_VACloudMotion.y, 0, _VACloudMotion.z) * _VACloudMotion.w;
    float3 p = (world - drift) * _VACloudMotion.x;
    float4 n = VANoise(p);
    float shape = dot(n.rgb, float3(.62, .26, .12));
    // A second, incommensurate spatial scale groups puffs into weather banks and hides tiling.
    float weather = VANoise(p * .217 + float3(.31, .17, .73)).r;
    float threshold = lerp(.68, .25, _VACloudShape.x) + (weather - .5) * .45;
    float body = saturate((shape - threshold) / .24);
    float erosion = VANoise(p * 2.7 + .13).b * .11;
    return max(0, body * vertical - erosion * (1 - body)) * _VACloudShape.y;
}
// Integrated along the same sun direction and density field as the visible clouds.
// A small world-space map amortizes this march across all fog samples.
float VACloudLightTransmission(float2 cloudBaseXZ)
{
    float3 sun = normalize(_VASunDirection.xyz);
    if (sun.y <= .01 || _VACloudShape.x <= .001 || _VACloudShape.y <= 0) return 1;
    float segment = _VACloudShape.w / max(sun.y, .05) / 6.0;
    float opticalDepth = 0;
    [unroll] for (int i = 0; i < 6; i++)
    {
        float3 p = float3(cloudBaseXZ.x, _VACloudShape.z, cloudBaseXZ.y)
            + sun * ((i + .5) * segment);
        opticalDepth += VACloudDensity(p) * segment;
    }
    return exp(-opticalDepth * .038);
}
float VACloudSunVisibility(float3 world)
{
    if (_VACloudShadowRegion.w < .5 || _VASunRays.w <= 0) return 1;
    float3 sun = normalize(_VASunDirection.xyz);
    float2 baseXZ = world.xz + sun.xz * ((_VACloudShape.z - world.y) / max(sun.y, .05));
    float2 uv = (baseXZ - _VACloudShadowRegion.xy) * _VACloudShadowRegion.z + .5;
    float edge = smoothstep(0, .08, min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y)));
    float visibility = SAMPLE_TEXTURE2D_LOD(_VACloudShadowTexture, sampler_VACloudShadowTexture, saturate(uv), 0).r;
    return lerp(1, visibility, edge * _VASunRays.w);
}
float3 VACloudSky(float3 origin, float3 ray, float jitter)
{
    float3 sky = VASky(ray);
    if (abs(ray.y) < .001 || _VACloudShape.y <= 0 || _VACloudShape.x <= .001) return sky;
    float t0 = (_VACloudShape.z - origin.y) / ray.y;
    float t1 = (_VACloudShape.z + _VACloudShape.w - origin.y) / ray.y;
    float start = max(0, min(t0, t1));
    float end = min(max(t0, t1), 9000);
    if (end <= start) return sky;
    int steps = clamp((int)_VAStyle.x, 16, 64);
    float stepSize = (end - start) / steps;
    float transmission = 1;
    float3 scattered = 0;
    float3 sun = normalize(_VASunDirection.xyz);
    float forward = pow(saturate(dot(ray, sun)), 10);
    [loop] for (int i = 0; i < steps; i++)
    {
        float3 p = origin + ray * (start + (i + jitter) * stepSize);
        float density = VACloudDensity(p);
        if (density > .001)
        {
            float lightDepth = VACloudDensity(p + sun * 45) * 55;
            lightDepth += VACloudDensity(p + sun * 115) * 100;
            lightDepth += VACloudDensity(p + sun * 240) * 160;
            float lightTransmission = exp(-lightDepth * .065);
            float height = saturate((p.y - _VACloudShape.z) / _VACloudShape.w);
            float3 lighting = _VACloudShadow.rgb * lerp(.85, 1.25, height);
            lighting += _VACloudColor.rgb * _VASunColor.rgb * (lightTransmission * 1.5 + .12 + forward * lightTransmission * .6);
            float alpha = 1 - exp(-density * stepSize * .038);
            scattered += transmission * alpha * lighting;
            transmission *= 1 - alpha;
            if (transmission < .015) break;
        }
    }
    // Atmospheric perspective softly merges far cloud banks into the horizon.
    float distanceHaze = smoothstep(1800, 8000, start);
    return lerp(scattered + sky * transmission, sky, distanceHaze);
}
float VAFogDensity(float3 p)
{
    float height = exp(-max(p.y - _VAFogShape.y, 0) / max(_VAFogShape.z, 1));
    float3 drift = float3(_VACloudMotion.y, 0, _VACloudMotion.z) * _VACloudMotion.w * .16;
    float n = VANoise((p - drift) * _VAFogDetail.x).r;
    float modulation = lerp(1, smoothstep(.2, .75, n) * 1.7, _VAFogDetail.y);
    return _VAFogShape.x * height * modulation;
}
float4 VAFog(float3 origin, float3 ray, float distanceToSurface, float jitter, int steps)
{
    float maximum = min(distanceToSurface, _VAFogShape.w);
    if (_VAEnabled < .5 || _VAFogShape.x <= 0 || maximum <= 0) return 0;
    float transmission = 1;
    float3 scatter = 0;
    float sunDot = dot(ray, normalize(_VASunDirection.xyz));
    float phase = .25 + pow(saturate(sunDot), 8) * 1.5;
    float anisotropy = clamp(_VASunRays.y, 0, .85);
    float rayPhase = (1 - anisotropy * anisotropy)
        / (12.56637 * pow(max(.02, 1 + anisotropy * anisotropy - 2 * anisotropy * sunDot), 1.5));
    rayPhase = min(6, .04 + rayPhase * 2);
    float nearRange = min(maximum, 160);
    int nearSteps = maximum > nearRange ? max(4, (int)(steps * .75)) : steps;
    [loop] for (int i = 0; i < steps; i++)
    {
        // Spend most samples around the player, resolving gaps between branches.
        float start, end;
        if (i < nearSteps)
        {
            float a = (float)i / nearSteps;
            float b = (float)(i + 1) / nearSteps;
            start = a * a * nearRange;
            end = b * b * nearRange;
        }
        else
        {
            float farStep = (maximum - nearRange) / max(steps - nearSteps, 1);
            start = nearRange + (i - nearSteps) * farStep;
            end = start + farStep;
        }
        float segment = end - start;
        float t = lerp(start, end, jitter);
        float3 p = origin + ray * t;
        // Directional shadows do not divide by w. Keep it valid for compiler
        // variants that still inspect the perspective branch in URP.
        float4 shadowCoord = TransformWorldToShadowCoord(p);
        shadowCoord.w = 1;
        float shadow = MainLightRealtimeShadow(shadowCoord);
        // Remove the artistic shadow-strength floor for direct volumetric light.
        // Fully blocked sunlight should not create a bright shaft through a wall.
        float directShadow = saturate((shadow - (1 - _MainLightShadowParams.x)) / max(_MainLightShadowParams.x, .001));
        directShadow = lerp(directShadow, 1, GetMainLightShadowFade(p));
        float sunVisibility = directShadow * VACloudSunVisibility(p);
        float3 lighting = _VAFogColor.rgb * (.65 + .25 * sunVisibility);
        lighting += _VASunColor.rgb * phase * sunVisibility * _VAFogDetail.z * .2;
        float rayFade = 1 - smoothstep(_VASunRays.z * .7, _VASunRays.z, t);
        lighting += _VASunColor.rgb * rayPhase * sunVisibility * _VASunRays.x * _VASunRayRadiance * rayFade;
        float alpha = 1 - exp(-VAFogDensity(p) * segment);
        scatter += transmission * alpha * lighting;
        transmission *= 1 - alpha;
    }
    return float4(scatter, 1 - transmission);
}
float3 VAApplyWaterFog(float3 color, float3 world)
{
    if (_VAEnabled < .5) return color;
    float3 delta = world - GetCameraPositionWS();
    float distanceToSurface = length(delta);
    float4 fog = VAFog(GetCameraPositionWS(), delta / max(distanceToSurface, .001), distanceToSurface, .5, 6);
    return color * (1 - fog.a) + fog.rgb;
}
#endif
