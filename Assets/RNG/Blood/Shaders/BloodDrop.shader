Shader "RNG/Blood Drop"
{
    Properties
    {
        _BloodColor ("Blood Color", Color) = (0.36, 0.012, 0.01, 1)
        _Glow ("Shadow Glow", Range(0, 1)) = 0.06
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _SpecularColor ("Specular Color", Color) = (0.25, 0.25, 0.25, 1)
        _Reflections ("Environment Reflections", Range(0, 1)) = 0.35
        _Stretch ("Stretch Per Speed", Float) = 0.16
        _StretchMax ("Stretch Max", Float) = 1.8
        _Wobble ("Wobble", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-30" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Drop"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #if UNITY_VERSION >= 60010000
                #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
                #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #else
                #pragma multi_compile _ _FORWARD_PLUS
            #endif
            #pragma multi_compile_fog

            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                #undef _ADDITIONAL_LIGHTS_VERTEX
                #define _ADDITIONAL_LIGHTS 1
            #endif

            #define _SPECULAR_SETUP
            #define _SURFACE_TYPE_TRANSPARENT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BloodColor;
                half4 _SpecularColor;
                float _Smoothness;
                float _Stretch;
                float _StretchMax;
                float _Wobble;
                float _Glow;
                float _Reflections;
            CBUFFER_END

            float _BloodTime;
            float _BloodGravity;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 origin : TEXCOORD0;
                float4 velocity : TEXCOORD1;
                float4 misc : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float t = _BloodTime - input.origin.w;
                float alive = step(0.0, t) * step(t, input.velocity.w);
                float3 fall = float3(0.0, _BloodGravity, 0.0);
                float3 centre = input.origin.xyz + input.velocity.xyz * t + 0.5 * fall * t * t;
                float3 speedVector = input.velocity.xyz + fall * t;
                float speed = length(speedVector);
                float3 heading = speedVector / max(0.001, speed);

                float seed = input.misc.y;
                float ripple = sin(dot(input.positionOS, float3(12.9, 78.2, 37.7)) * 2.0 + seed * 40.0 + t * 16.0);
                float3 local = input.positionOS * (input.misc.x * (1.0 + _Wobble * ripple));
                float along = dot(input.positionOS, heading);
                float pull = min(_StretchMax, speed * _Stretch);
                local += heading * (along * input.misc.x * pull);
                local -= heading * (saturate(-along) * saturate(-along) * input.misc.x * pull * 0.6);

                float3 positionWS = centre + local * alive;
                float3 normal = normalize(input.positionOS - heading * (along * pull / (1.0 + pull)));

                output.positionWS = positionWS;
                output.normalWS = normal;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 toCamera = GetWorldSpaceViewDir(input.positionWS);
                float3 normal = normalize(input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = normalize(toCamera);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BloodColor.rgb;
                surface.specular = _SpecularColor.rgb;
                surface.smoothness = _Smoothness;
                surface.occlusion = _Reflections;
                surface.emission = _BloodColor.rgb * _Glow;
                surface.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
