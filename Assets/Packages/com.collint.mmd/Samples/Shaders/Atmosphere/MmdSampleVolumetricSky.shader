Shader "Motion Matching for DOTS/Sample/Volumetric Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "MmdSampleAtmosphereCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "Skybox"
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment
            struct Attributes { float3 positionOS : POSITION; };
            struct SkyVaryings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            SkyVaryings SkyVertex(Attributes input)
            {
                SkyVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.direction = input.positionOS;
                return output;
            }
            half4 SkyFragment(SkyVaryings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float3 color = _VACloudsInSkybox > .5 ? VACloudSky(GetCameraPositionWS(), ray, .5) : VASky(ray);
                return half4(VAQuantize(color, input.positionCS.xy), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
