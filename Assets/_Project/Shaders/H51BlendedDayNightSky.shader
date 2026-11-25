Shader "Hanger51/Skybox/Blended Day Night"
{
    Properties
    {
        _NightTex ("Night Panorama", 2D) = "black" {}
        _HasNightTexture ("Has Night Texture", Range(0,1)) = 0
        _NightBlend ("Night Blend", Range(0,1)) = 0
        _DayZenithColor ("Day Zenith", Color) = (0.24,0.46,0.75,1)
        _DayHorizonColor ("Day Horizon", Color) = (0.68,0.78,0.88,1)
        _TwilightColor ("Twilight", Color) = (0.48,0.22,0.12,1)
        _NightFallbackColor ("Night Fallback", Color) = (0.008,0.015,0.04,1)
        _SunColor ("Sun Color", Color) = (1,0.88,0.62,1)
        _SunDirection ("Sun Direction", Vector) = (0,1,0,0)
        _SunDiscSize ("Sun Disc Size", Range(0.0005,0.02)) = 0.004
        _DayExposure ("Day Exposure", Range(0.1,2)) = 1
        _NightExposure ("Night Exposure", Range(0.05,2)) = 0.72
        _Rotation ("Night Rotation", Range(0,360)) = 155
        _StarIntensity ("Fallback Star Intensity", Range(0,8)) = 2.2
        _StarDensity ("Fallback Star Density", Range(0.001,0.08)) = 0.018
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

            sampler2D _NightTex;
            float _HasNightTexture;
            float _NightBlend;
            float4 _DayZenithColor;
            float4 _DayHorizonColor;
            float4 _TwilightColor;
            float4 _NightFallbackColor;
            float4 _SunColor;
            float4 _SunDirection;
            float _SunDiscSize;
            float _DayExposure;
            float _NightExposure;
            float _Rotation;
            float _StarIntensity;
            float _StarDensity;

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

            float2 DirectionToPanoramaUV(float3 d)
            {
                const float PI = 3.14159265359;
                float rotation = radians(_Rotation);
                float c = cos(rotation);
                float s = sin(rotation);
                d.xz = float2(
                    d.x * c - d.z * s,
                    d.x * s + d.z * c);

                float2 uv;
                uv.x = atan2(d.z, d.x) / (2.0 * PI) + 0.5;
                uv.y = asin(clamp(d.y,-1.0,1.0)) / PI + 0.5;
                return uv;
            }

            float3 FallbackNight(float3 d)
            {
                float2 uv = DirectionToPanoramaUV(d);
                float2 starUv = uv * float2(1050.0,525.0);
                float2 cell = floor(starUv);
                float2 local = frac(starUv) - 0.5;
                float randomValue = Hash21(cell);
                float exists = step(1.0 - _StarDensity,randomValue);
                float radius = lerp(0.035,0.09,Hash21(cell + 13.73));
                float star = 1.0 - smoothstep(
                    radius * 0.45,
                    radius,
                    length(local));
                float3 color = _NightFallbackColor.rgb;
                color += exists * star * _StarIntensity;
                return color;
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
                float height = saturate(d.y * 0.72 + 0.28);

                float3 day =
                    lerp(
                        _DayHorizonColor.rgb,
                        _DayZenithColor.rgb,
                        smoothstep(0.0,1.0,height));

                float twilightBand =
                    exp(-abs(d.y) * 7.0) *
                    smoothstep(0.05,0.72,_NightBlend) *
                    (1.0 - smoothstep(0.72,1.0,_NightBlend));
                day = lerp(
                    day,
                    _TwilightColor.rgb,
                    saturate(twilightBand * 0.78));

                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDot = saturate(dot(d,sunDir));
                float sunDisc =
                    smoothstep(
                        1.0 - _SunDiscSize,
                        1.0,
                        sunDot);
                day += _SunColor.rgb * sunDisc * (1.0 - _NightBlend);

                float2 nightUv = DirectionToPanoramaUV(d);
                float3 panorama =
                    tex2D(_NightTex,nightUv).rgb;
                float3 fallback = FallbackNight(d);
                float3 night =
                    lerp(
                        fallback,
                        panorama,
                        saturate(_HasNightTexture));

                float3 dayExposed = day * _DayExposure;
                float3 nightExposed = night * _NightExposure;

                float blend =
                    smoothstep(0.0,1.0,saturate(_NightBlend));
                return fixed4(
                    lerp(dayExposed,nightExposed,blend),
                    1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
