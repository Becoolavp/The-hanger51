Shader "Hanger51/Skybox/Procedural Night"
{
    Properties
    {
        _TopColor ("Zenith Color", Color) = (0.005, 0.009, 0.025, 1)
        _HorizonColor ("Horizon Color", Color) = (0.025, 0.035, 0.065, 1)
        _StarColor ("Star Color", Color) = (0.82, 0.9, 1.0, 1)
        _StarIntensity ("Star Intensity", Range(0, 8)) = 2.6
        _StarDensity ("Star Density", Range(0.001, 0.08)) = 0.018
        _MilkyWayIntensity ("Milky Way Intensity", Range(0, 2)) = 0.28
        _Exposure ("Exposure", Range(0.05, 2)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _TopColor;
            float4 _HorizonColor;
            float4 _StarColor;
            float _StarIntensity;
            float _StarDensity;
            float _MilkyWayIntensity;
            float _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float horizon = saturate(d.y * 0.9 + 0.18);
                float3 color = lerp(
                    _HorizonColor.rgb,
                    _TopColor.rgb,
                    saturate(horizon));

                const float PI = 3.14159265;
                float2 uv;
                uv.x = atan2(d.z, d.x) / (2.0 * PI) + 0.5;
                uv.y = asin(clamp(d.y, -1.0, 1.0)) / PI + 0.5;

                float2 starUv = uv * float2(1100.0, 550.0);
                float2 starCell = floor(starUv);
                float2 starLocal = frac(starUv) - 0.5;
                float randomValue = Hash21(starCell);
                float starExists = step(1.0 - _StarDensity, randomValue);
                float starRadius = lerp(0.035, 0.095, Hash21(starCell + 19.17));
                float starShape = 1.0 - smoothstep(
                    starRadius * 0.45,
                    starRadius,
                    length(starLocal));
                float twinkle = lerp(
                    0.6,
                    1.65,
                    Hash21(starCell + 71.43));

                float3 milkyAxis = normalize(float3(0.18, 0.73, 0.66));
                float milkyDistance = abs(dot(d, milkyAxis));
                float milkyBand = exp(-milkyDistance * milkyDistance * 30.0);
                float milkyNoise =
                    lerp(
                        0.45,
                        1.0,
                        Hash21(floor(uv * float2(180.0, 90.0))));
                color +=
                    _StarColor.rgb *
                    milkyBand *
                    milkyNoise *
                    _MilkyWayIntensity;

                color +=
                    _StarColor.rgb *
                    starExists *
                    starShape *
                    twinkle *
                    _StarIntensity;

                return fixed4(color * _Exposure, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
