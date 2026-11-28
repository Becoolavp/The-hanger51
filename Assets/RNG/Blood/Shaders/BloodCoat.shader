Shader "RNG/Blood Coat"
{
    Properties
    {
        _Glow ("Shadow Glow", Range(0, 1)) = 0.03
        _Smoothness ("Smoothness", Range(0, 1)) = 0.86
        _SpecularColor ("Specular Color", Color) = (0.22, 0.22, 0.22, 1)
        _Reflections ("Environment Reflections", Range(0, 1)) = 0.45
        _Edge ("Edge Noise", Range(0, 1)) = 0.7
        _Drips ("Drips", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-45" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Coat"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

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
            #define MAX_WOUNDS 32

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SpecularColor;
                float _Smoothness;
                float _Glow;
                float _Reflections;
                float _Edge;
                float _Drips;
            CBUFFER_END

            float _BloodTime;
            float4 _BloodWounds[MAX_WOUNDS];
            float4 _BloodWoundData[MAX_WOUNDS];
            float4 _BloodWoundDown[MAX_WOUNDS];
            float _BloodWoundCount;
            float4 _BloodWoundWash[MAX_WOUNDS];
            float _BloodCoatStable;
            float4 _BloodCoatColor;
            float4 _BloodCoatDry;
            float4 _BloodCoatLook;

            // uv3 and uv4 carry the rest position and normal of the skin, so wounds stay put when the body moves
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 restPosition : TEXCOORD3;
                float3 restNormal : TEXCOORD4;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float3 restPosition : TEXCOORD3;
                float3 restNormal : TEXCOORD4;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float2 Hash21(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            float Noise3(float3 p)
            {
                float3 cell = floor(p);
                float3 local = frac(p);
                float3 blend = local * local * (3.0 - 2.0 * local);
                float a = lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), blend.x);
                float b = lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), blend.x);
                float c = lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), blend.x);
                float d = lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + float3(1, 1, 1)), blend.x);
                return lerp(lerp(a, b, blend.y), lerp(c, d, blend.y), blend.z);
            }

            float SmoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            float Wound(float3 positionWS, float3 normal, float3 downward, float4 wound, float4 data, float age, out float hole)
            {
                float radius = max(0.001, wound.w);
                float3 delta = (positionWS - wound.xyz) / radius;
                float seed = data.y;
                float grow = saturate(age / max(0.01, data.w));
                float settle = 1.0 - (1.0 - grow) * (1.0 - grow);
                float splat = saturate(age * 12.0);
                float size = lerp(0.55, 1.0, 1.0 - (1.0 - splat) * (1.0 - splat));
                float3 side = cross(-downward, normal);
                side = dot(side, side) > 0.0001 ? normalize(side) : normalize(cross(normal, float3(0.31, 0.53, 0.79)));
                float3 rise = cross(normal, side);
                float lateral = dot(delta, side);
                float down = dot(delta, downward);
                float2 flat = float2(lateral, dot(delta, rise));
                float angle = atan2(flat.y, flat.x);
                float wobble = 0.16 * sin(angle * 3.0 + seed) + 0.11 * sin(angle * 5.0 + seed * 2.3) + 0.07 * sin(angle * 9.0 + seed * 3.7);
                float rough = Noise3(positionWS / radius * 2.2 + seed) * 0.65 + Noise3(positionWS / radius * 6.0 + seed * 1.7) * 0.35;
                float dist = length(delta) - size * (0.85 + wobble) + (rough - 0.5) * _Edge;
                hole = saturate(1.0 - length(flat) * 3.2);

                [unroll]
                for (int j = 0; j < 6; j++)
                {
                    float2 h = Hash21(float2(seed * 2.3 + j * 5.7, seed * 0.7 + j * 1.9));
                    float turn = h.x * 6.2831853;
                    float2 at = float2(cos(turn), sin(turn)) * (1.05 + 0.8 * h.y) * size;
                    float speck = length(float3(flat - at, dot(delta, normal) * 0.5)) - (0.05 + 0.1 * (1.0 - h.y)) * size;
                    dist = min(dist, speck);
                }
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    float2 h = Hash21(float2(seed * 1.7 + k * 3.1, seed + k * 7.3));
                    if (k > 0 && h.x > 0.62) continue;

                    float offset = (h.x - 0.5) * 1.2;
                    float width = (0.12 + 0.11 * h.y) * size;
                    float reach = (0.5 + 3.2 * h.y) * settle * _Drips;
                    float t = saturate(down / max(0.01, reach));
                    float2 along = float2(lateral - offset, down - clamp(down, 0.0, reach));
                    float column = length(along) - width * (1.0 - 0.4 * t) * (0.85 + 0.3 * rough);
                    float bead = length(float2(lateral - offset, (down - reach) * 0.75)) - width * 1.35;
                    if (reach > 0.05) dist = SmoothMin(dist, min(column, bead), 0.12);
                }

                return dist;
            }

            // blood soaked in where the body lay in a pool: everything within the radius up to a ragged level above the contact
            float Soak(float3 place, float3 downward, float4 wound, float depth, float seed)
            {
                float radius = max(0.001, wound.w);
                float3 delta = (place - wound.xyz) / radius;
                float up = -dot(delta, downward);
                float rough = Noise3(place / radius * 2.5 + seed) * 0.6 + Noise3(place / radius * 7.0 + seed * 1.3) * 0.4;
                float sphere = length(delta) - 1.0 + (rough - 0.5) * 0.6 * _Edge;
                float level = up - depth / radius + (Noise3(place * 30.0 + seed) - 0.5) * 0.6 + (rough - 0.5) * 0.4;
                return max(sphere, level);
            }

            // a blade that went into a body: coated from the tip back to the depth it went in. The line is where the body closed on the
            // blade, a little ragged, with a few short tongues past it; just under the line the blood sits thickest, further down the
            // flesh wiped it into streaks on the way out. thin is how much of the blood the streaks leave at this point.
            float Stab(float3 place, float3 tipward, float4 wound, float depth, float seed, out float thin)
            {
                float radius = max(0.001, wound.w);
                float3 delta = place - wound.xyz;
                float up = -dot(delta, tipward);
                float3 side = delta + tipward * up;
                float jag = (Noise3(side * 45.0 + seed) - 0.5) * 0.03 + (Noise3(side * 140.0 + seed * 1.9) - 0.5) * 0.008;
                float tongue = saturate(Noise3(side * 22.0 + seed * 3.1) * 2.4 - 1.2) * 0.045;
                float level = up - depth - jag - tongue;
                float past = -up - 0.03;
                float streak = Noise3(side * 160.0 + tipward * (up * 5.0) + seed * 0.7);
                float collar = smoothstep(0.035, 0.005, depth - up);
                thin = saturate(lerp(-0.1, 1.3, streak) + collar * 1.2);
                return max(level, past) / radius;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.restPosition = input.restPosition;
                output.restNormal = input.restNormal;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                bool stable = _BloodCoatStable > 0.5;
                float3 place = stable ? input.restPosition : input.positionWS;
                float3 skinNormal = stable ? normalize(input.restNormal) : normal;
                float clear = 1.0;
                float weight = 0.0;
                float dryness = 0.0;
                float darkness = 0.0;
                float washed = 0.0;
                int count = (int)_BloodWoundCount;
                // the size of a pixel on the skin, taken once outside the loop: a derivative inside it is undefined once pixels
                // skip wounds they are far from. A wound's distance is in its own radii, so its edge is this size over its radius.
                float pixel = length(fwidth(place));
                [loop]
                for (int i = 0; i < count; i++)
                {
                    float4 data = _BloodWoundData[i];
                    float age = _BloodTime - data.x;
                    if (age < 0.0 || age > data.z) continue;

                    float3 downward = stable ? _BloodWoundDown[i].xyz : float3(0.0, -1.0, 0.0);
                    // a wound only reaches about two of its radii around it, plus its runs below: everything further is skipped
                    // before the noise, so a body can carry many wounds and each pixel pays only for the few that touch it
                    float3 offset = (place - _BloodWounds[i].xyz) / max(0.001, _BloodWounds[i].w);
                    float below = dot(offset, downward);
                    float reachOut = 2.3 + 0.5 * abs(_Edge);
                    if (below < -reachOut || below > reachOut + 3.7 * _Drips || dot(offset, offset) - below * below > reachOut * reachOut) continue;

                    float hole = 0.0;
                    float thin = 1.0;
                    float depth = _BloodWoundDown[i].w;
                    // y of the wash slot is the shape: 0 a wound or soak, 1 a stabbed blade, 2 a splat on a blunt weapon (a wound with no hole)
                    float shape = _BloodWoundWash[i].y;
                    float dist;
                    if (shape > 0.5 && shape < 1.5) dist = Stab(place, downward, _BloodWounds[i], depth, data.y, thin);
                    else if (depth > 0.0) dist = Soak(place, downward, _BloodWounds[i], depth, data.y);
                    else dist = Wound(place, skinNormal, downward, _BloodWounds[i], data, age, hole);
                    if (shape > 1.5) hole = 0.0;
                    float edge = max(pixel / max(0.001, _BloodWounds[i].w), 0.01);
                    float life = saturate(age / max(0.01, data.z));
                    float cover = smoothstep(edge, -edge, dist) * thin * (1.0 - smoothstep(_BloodCoatDry.z, 1.0, life)) * (1.0 - _BloodWoundWash[i].x);
                    washed = max(washed, _BloodWoundWash[i].x * smoothstep(edge, -edge, dist));
                    float dry = smoothstep(_BloodCoatDry.x, max(_BloodCoatDry.x + 0.001, _BloodCoatDry.y), life);
                    clear *= 1.0 - cover;
                    weight += cover;
                    darkness = max(darkness, cover * hole * (1.0 - dry));
                    dryness += cover * dry;
                }

                float alpha = 1.0 - clear;
                clip(alpha - 0.004);
                float dry = dryness / max(0.0001, weight);

                float3 colour = _BloodCoatColor.rgb;
                float strongest = max(colour.r, max(colour.g, colour.b));
                float3 dried = lerp(colour, float3(1.0, 0.45, 0.25) * strongest, _BloodCoatLook.y) * _BloodCoatLook.x;
                float3 toCamera = GetWorldSpaceViewDir(input.positionWS);

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
                surface.albedo = lerp(colour, dried, dry) * lerp(1.0, 0.3, darkness);
                surface.albedo = lerp(surface.albedo, surface.albedo * 1.6 + float3(0.06, 0.015, 0.02), washed * 0.8);
                surface.specular = _SpecularColor.rgb * lerp(1.0, 0.15, sqrt(dry));
                surface.smoothness = lerp(_Smoothness, _BloodCoatLook.z, dry);
                surface.occlusion = lerp(_Reflections, 1.0, dry);
                surface.emission = colour * _Glow * (1.0 - dry);
                surface.alpha = alpha;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, alpha * _BloodCoatColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
