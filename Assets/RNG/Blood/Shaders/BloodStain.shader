Shader "RNG/Blood Stain"
{
    Properties
    {
        _BloodColor ("Blood Color", Color) = (0.36, 0.012, 0.01, 1)
        _Glow ("Shadow Glow", Range(0, 1)) = 0.03
        _Smoothness ("Smoothness", Range(0, 1)) = 0.84
        _SpecularColor ("Specular Color", Color) = (0.2, 0.2, 0.2, 1)
        _Reflections ("Environment Reflections", Range(0, 1)) = 0.45
        _Thickness ("Projection Thickness", Float) = 0.18
        _Rise ("Splat Rise", Range(0.01, 0.2)) = 0.2
        _LiquidReach ("Liquid Floor Reach", Range(0.02, 0.5)) = 0.08
        _Bevel ("Edge Bevel", Float) = 0.9
        _BevelWidth ("Edge Bevel Width", Float) = 0.12
        _SplatSeconds ("Splat Seconds", Float) = 0.12
        _DryStart ("Dry Start", Range(0, 1)) = 0.25
        _DryEnd ("Dry End", Range(0, 1)) = 0.65
        _FadeStart ("Fade Start", Range(0, 1)) = 0.75
        _DryBrightness ("Dry Brightness", Range(0, 1)) = 0.3
        _DryBrown ("Dry Brown", Range(0, 1)) = 0.3
        _DryGloss ("Dry Smoothness", Range(0, 1)) = 0.15
        _PoolFadeSeconds ("Pool Fade Seconds", Float) = 6
        _PoolFlatten ("Pool Flatten When Spread", Range(0, 1)) = 0.45
        [HideInInspector] _BloodZTest ("ZTest", Float) = 8
        [HideInInspector] _BloodMapLife ("Map Lifetime", Float) = 30
        [HideInInspector] _BloodMapTexel ("Map Texel", Float) = 0.0005
        [NoScaleOffset] _BloodMap ("Map", 2D) = "black" {}
        [HideInInspector] _BloodLiquidFilm ("Liquid Film", Float) = 1.5
        [HideInInspector] _BloodLiquidTexel ("Liquid Texel", Float) = 0.001
        [NoScaleOffset] _BloodLiquid ("Liquid", 2D) = "black" {}
        [NoScaleOffset] _LiquidChannels ("Liquid Channels", 2D) = "black" {}
        [HideInInspector] _LiquidChannelFrame ("Liquid Channel Frame", Vector) = (0, 0, 1, 1)
        [HideInInspector] _LiquidChannelDepth ("Liquid Channel Depth", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-40" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Stain"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_BloodZTest]
            Cull Front

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
            #pragma dynamic_branch_local _ _BLOOD_MEDIUM _BLOOD_LOW

            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                #undef _ADDITIONAL_LIGHTS_VERTEX
                #define _ADDITIONAL_LIGHTS 1
            #endif

            #define _SPECULAR_SETUP
            #define _SURFACE_TYPE_TRANSPARENT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BloodColor;
                half4 _SpecularColor;
                float _Smoothness;
                float _Thickness;
                float _Rise;
                float _LiquidReach;
                float _Bevel;
                float _BevelWidth;
                float _SplatSeconds;
                float _DryStart;
                float _DryEnd;
                float _FadeStart;
                float _DryBrightness;
                float _DryBrown;
                float _DryGloss;
                float _PoolFadeSeconds;
                float _PoolFlatten;
                float _Glow;
                float _Reflections;
                float _BloodMapLife;
                float _BloodMapTexel;
                float _BloodLiquidFilm;
                float _BloodLiquidTexel;
                float4 _LiquidChannelFrame;
                float _LiquidChannelDepth;
            CBUFFER_END

            TEXTURE2D(_BloodMap);
            SAMPLER(sampler_BloodMap);
            TEXTURE2D(_BloodLiquid);
            SAMPLER(sampler_BloodLiquid);
            float4 _BloodWashA[16];
            float4 _BloodWashB[16];
            float4 _BloodWashC[16];
            float _BloodWashCount;

            TEXTURE2D(_LiquidChannels);
            SAMPLER(sampler_LiquidChannels);

            float _BloodTime;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 centre : TEXCOORD0;
                float4 axisU : TEXCOORD1;
                float4 axisV : TEXCOORD2;
                float4 life : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float3 up : TEXCOORD0;
                nointerpolation float4 centre : TEXCOORD1;
                nointerpolation float4 axisU : TEXCOORD2;
                nointerpolation float4 axisV : TEXCOORD3;
                nointerpolation float4 life : TEXCOORD4;
                float3 positionWS : TEXCOORD5;
                nointerpolation float4 phase : TEXCOORD6;
                nointerpolation float4 k0 : TEXCOORD7;
                nointerpolation float4 k1 : TEXCOORD8;
                nointerpolation float4 k2 : TEXCOORD9;
                nointerpolation float4 k3 : TEXCOORD10;
                nointerpolation float4 k4 : TEXCOORD11;
                nointerpolation float4 k5 : TEXCOORD12;
                nointerpolation float4 k6 : TEXCOORD13;
            };

            #include "BloodShapes.hlsl"

            // how much of a stain laid at birth the water washing over this point has taken: blood laid before the water came goes as the
            // water goes on, blood laid while it ran goes from when it landed
            float Washed(float3 positionWS, float birth)
            {
                float washed = 0.0;
                int count = (int)_BloodWashCount;
                for (int i = 0; i < count; i++)
                {
                    float last = _BloodWashC[i].y;
                    if (birth > last) continue;

                    float3 a = _BloodWashA[i].xyz;
                    float3 ab = _BloodWashB[i].xyz - a;
                    float r = _BloodWashA[i].w;
                    float t = saturate(dot(positionWS - a, ab) / max(1e-6, dot(ab, ab)));
                    float d = length(positionWS - a - ab * t) + (Noise2(positionWS.xz * 3.0) - 0.5) * r * 0.25;
                    float start = _BloodWashB[i].w;
                    float share = birth <= start ? 1.0 : saturate((last - birth) / max(0.01, last - start));
                    washed = max(washed, _BloodWashC[i].x * share * (1.0 - smoothstep(r * 0.75, r, d)));
                }

                return washed;
            }


            Kit Unpack(Varyings input)
            {
                Kit kit;
                kit.phase = input.phase;
                kit.spots[0] = input.k0.xy;
                kit.spots[1] = input.k0.zw;
                kit.spots[2] = input.k1.xy;
                kit.spots[3] = input.k1.zw;
                kit.spots[4] = input.k2.xy;
                kit.spots[5] = input.k2.zw;
                kit.spots[6] = input.k3.xy;
                kit.spots[7] = input.k3.zw;
                kit.spots[8] = input.k4.xy;
                kit.sizes[0] = input.k4.z;
                kit.sizes[1] = input.k4.w;
                kit.sizes[2] = input.k5.x;
                kit.sizes[3] = input.k5.y;
                kit.sizes[4] = input.k5.z;
                kit.sizes[5] = input.k5.w;
                kit.sizes[6] = input.k6.x;
                kit.sizes[7] = input.k6.y;
                kit.sizes[8] = input.k6.z;
                kit.tail = input.k6.w;
                return kit;
            }

            float Fade(float4 life, float age, float span)
            {
                if (life.z > 0.5 && life.z < 1.5) return saturate((age - (life.y - _PoolFadeSeconds)) / max(0.01, _PoolFadeSeconds));
                return smoothstep(life.z < 0.5 && life.w > 4.5 ? min(_FadeStart, 0.4) : _FadeStart, 1.0, saturate(age / max(0.01, span)));
            }

            float3 Dried(float3 colour)
            {
                float strongest = max(colour.r, max(colour.g, colour.b));
                return lerp(colour, float3(1.0, 0.45, 0.25) * strongest, _DryBrown) * _DryBrightness;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float age = _BloodTime - input.life.x;
                output.positionCS = age < 0.0 || age > input.life.y ? float4(0.0, 0.0, 0.0, 1.0) : TransformWorldToHClip(input.positionOS);
                output.up = input.normalOS;
                output.centre = input.centre;
                output.axisU = input.axisU;
                output.axisV = input.axisV;
                output.life = input.life;
                output.positionWS = input.positionOS;

                float seed = abs(input.centre.w);
                float2 spots[9];
                float sizes[9];
                float tail = 0.0;
                float drift = 0.0;
                if (input.life.z > 1.5)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        spots[i] = 0.0;
                        sizes[i] = 0.0;
                    }
                }
                else if (input.life.z > 0.5)
                {
                    Lobes(seed, Grown(input.life, age), spots, sizes);
                    drift = min(age, input.life.y * _DryStart) * 0.03;
                }
                else
                {
                    float stretch = input.life.w > 3.5 ? 1.0 : input.axisV.w / max(0.0001, input.axisU.w);
                    Specks(seed, stretch, spots, sizes, tail);
                }

                output.phase = Phase(seed, drift);
                output.k0 = float4(spots[0], spots[1]);
                output.k1 = float4(spots[2], spots[3]);
                output.k2 = float4(spots[4], spots[5]);
                output.k3 = float4(spots[6], spots[7]);
                output.k4 = float4(spots[8], sizes[0], sizes[1]);
                output.k5 = float4(sizes[2], sizes[3], sizes[4], sizes[5]);
                output.k6 = float4(sizes[6], sizes[7], sizes[8], tail);
                if (input.life.z < 0.5 && input.life.w > 4.5)
                {
                    output.up = normalize(cross(input.axisU.xyz, input.axisV.xyz));
                    output.k0 = float4(input.normalOS, 0.0);
                }
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float age = _BloodTime - input.life.x;
                clip(age);
                clip(input.life.y - age);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 up = normalize(input.up);
                bool onGlass = input.centre.w < 0.0;
                bool flat = onGlass || _BLOOD_LOW;
                float3 positionWS = input.positionWS;
                if (flat)
                {
                    float3 eye = GetCameraPositionWS();
                    float3 sight = input.positionWS - eye;
                    float facing = dot(sight, up);
                    clip(abs(facing) - 0.00001);
                    float travel = dot(input.centre.xyz - eye, up) / facing;
                    clip(travel);
                    positionWS = eye + sight * travel;
                }

                if (!_BLOOD_LOW)
                {
                    #if UNITY_REVERSED_Z
                        float rawDepth = SampleSceneDepth(screenUV);
                    #else
                        float rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(screenUV));
                    #endif
                    float3 sceneWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    if (onGlass)
                    {
                        float3 eye = GetCameraPositionWS();
                        clip(distance(sceneWS, eye) - distance(positionWS, eye) + 0.02);
                    }
                    else
                    {
                        positionWS = sceneWS;
                    }
                }

                bool map = input.life.z > 2.5;
                float3 toCamera = GetWorldSpaceViewDir(positionWS);
                float3 surfaceNormal = up;
                if (!flat)
                {
                    float3 across = ddx(positionWS);
                    float3 down = ddy(positionWS);
                    surfaceNormal = normalize(cross(down, across));
                    if (dot(surfaceNormal, toCamera) < 0.0) surfaceNormal = -surfaceNormal;
                    if (max(dot(across, across), dot(down, down)) > 0.04) surfaceNormal = up;
                }

                float3 delta = positionWS - input.centre.xyz;
                float lift = dot(delta, up);
                if (!map) clip(_Thickness - abs(lift));
                // a splat paints only so high above the surface it landed on, so a body or a prop lying in it stays clean; a run on a wall
                // still turns onto the floor under it
                bool runs = input.life.z < 0.5 && input.life.w > 3.5 && input.life.w < 4.5;
                if (input.life.z < 0.5 && !runs) clip(_Rise - lift);
                // a hand, knee, hoof or tyre still pressed on the print must not wear it: only the surface right under it takes the print
                if (input.life.z > 1.5 && input.life.z < 2.5)
                {
                    float kind = PrintKind(input.life.w);
                    if (kind > 0.5) clip((kind > 3.5 ? 0.04 : 0.008) - lift);
                }
                float facing = dot(surfaceNormal, up);
                bool folded = facing < 0.35;
                if (folded)
                {
                    bool liquid = map || (input.life.z > 0.5 && input.life.z < 1.5);
                    bool onWall = abs(up.y) < 0.5;
                    bool running = input.life.z < 0.5 && input.life.w > 3.5 && input.life.w < 4.5;
                    clip(0.4 - abs(facing));
                    clip(input.life.z > 1.5 && input.life.z < 2.5 ? -1.0 : 1.0);
                    float rise = abs(lift);
                    float reach = liquid ? 0.03 : onWall ? (running ? 0.16 : 0.035) : 0.1;
                    clip(reach - rise);
                    float3 outward = normalize(surfaceNormal - up * facing);
                    float3 crease = delta - up * lift;
                    foldCrease = dot(crease, input.axisU.xyz) * input.axisU.w;
                    foldRise = max(rise, 0.0001);
                    delta = crease - outward * rise;
                }

                float2 p = float2(dot(delta, input.axisU.xyz) * input.axisU.w, dot(delta, input.axisV.xyz) * input.axisV.w);
                if (input.life.z < 0.5 && input.life.w > 4.5)
                {
                    float spanU = 1.0 / max(0.0001, input.axisU.w);
                    float along = p.x * spanU;
                    float across = p.y / max(0.0001, input.axisV.w);
                    clip(along + spanU - across * input.k0.x);
                    clip(spanU - across * input.k0.y - along);
                    clip(1.0 - abs(p.y));
                }
                else
                {
                    clip(1.0 - max(abs(p.x), abs(p.y)));
                }

                clip(dot(up, toCamera));

                float seed = abs(input.centre.w);
                float stretch = input.axisV.w / max(0.0001, input.axisU.w);
                Kit kit = Unpack(input);
                bool smearing = input.life.z < 0.5 && input.life.w > 4.5;
                float look = smearing ? SmearLook(input.k0.z, p) : 0.0;
                float wetSmear = saturate(-look);
                float4 frame = float4(smearing ? abs(input.centre.w) : dot(input.centre.xyz, input.axisU.xyz), input.axisU.w, input.axisV.w, smearing ? input.k0.z : 0.0);
                float span = input.life.y;
                smearPixel = max(length(ddx(positionWS)), length(ddy(positionWS)));
                float dist;
                float2 mapSlope = 0.0;
                bool liquidLayer = map && input.life.w > 0.5;
                float residue = 0.0;
                if (liquidLayer)
                {
                    float2 uv = p * 0.5 + 0.5;
                    float4 texel = SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv, 0);
                    clip(max(texel.r, texel.a) - 0.005);
                    // the liquid hugs the floor it was poured on: a body lying in the pool is wet only where it touches it, not painted over
                    clip((_LiquidChannelDepth > 0.0 ? min(0.08, _LiquidReach) : _LiquidReach) - abs(positionWS.y - texel.b));
                    age = _BloodTime - texel.g;
                    span = _BloodMapLife;
                    clip(span + 4.0 - age);
                    float film = _BloodLiquidFilm;
                    if (_LiquidChannelDepth > 0.0)
                    {
                        float2 c = float2((positionWS.x - _LiquidChannelFrame.x) / _LiquidChannelFrame.z, (positionWS.z - (_LiquidChannelFrame.y - _LiquidChannelFrame.w)) / _LiquidChannelFrame.w);
                        float groove = all(c >= 0.0) && all(c <= 1.0) ? SAMPLE_TEXTURE2D_LOD(_LiquidChannels, sampler_LiquidChannels, c, 0).r : 0.0;
                        film *= 1.0 - 0.92 * groove;
                        // liquid running down a groove sits in every other texel while it moves: draw the groove as full where any
                        // texel next to it holds some
                        if (groove > 0.5)
                        {
                            float g = _BloodLiquidTexel;
                            texel.r = max(texel.r, max(max(SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv + float2(g, 0.0), 0).r, SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv - float2(g, 0.0), 0).r),
                                max(SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv + float2(0.0, g), 0).r, SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv - float2(0.0, g), 0).r)));
                        }
                    }

                    float grain = (Noise2(positionWS.xz * 31.0) - 0.5) * 0.16 + (Noise2(positionWS.xz * 97.0 + 3.1) - 0.5) * 0.07;
                    dist = ((film * 0.5 - texel.r) / film + grain) * 0.3;
                    residue = texel.a;
                    if (dist > 0.0 && residue > 0.03) dist = min(dist, ((0.55 - residue) + grain) * 0.3 + 0.02);
                    float t = _BloodLiquidTexel;
                    mapSlope.x = (SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv + float2(t, 0.0), 0).r - SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv - float2(t, 0.0), 0).r) / _BloodLiquidFilm * 0.35;
                    mapSlope.y = (SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv + float2(0.0, t), 0).r - SAMPLE_TEXTURE2D_LOD(_BloodLiquid, sampler_BloodLiquid, uv - float2(0.0, t), 0).r) / _BloodLiquidFilm * 0.35;
                    residue = texel.r < film * 0.45 ? residue : 0.0;
                }
                else if (map)
                {
                    float2 uv = p * 0.5 + 0.5;
                    float4 texel = SAMPLE_TEXTURE2D_LOD(_BloodMap, sampler_BloodMap, uv, 0);
                    clip(texel.r - 0.01);
                    clip(0.4 - abs(positionWS.y - texel.b));
                    age = _BloodTime - texel.g;
                    span = _BloodMapLife;
                    clip(span - age);
                    dist = (0.5 - texel.r) * 0.6;
                    float t = _BloodMapTexel;
                    mapSlope.x = SAMPLE_TEXTURE2D_LOD(_BloodMap, sampler_BloodMap, uv + float2(t, 0.0), 0).r - SAMPLE_TEXTURE2D_LOD(_BloodMap, sampler_BloodMap, uv - float2(t, 0.0), 0).r;
                    mapSlope.y = SAMPLE_TEXTURE2D_LOD(_BloodMap, sampler_BloodMap, uv + float2(0.0, t), 0).r - SAMPLE_TEXTURE2D_LOD(_BloodMap, sampler_BloodMap, uv - float2(0.0, t), 0).r;
                }
                else
                {
                    dist = Shape(p, input.life, seed, age, stretch, kit, frame);
                }

                float fading = Fade(input.life, age, span);
                float pathAt = fmod(abs(input.centre.w), 100.0) + p.x / max(0.0001, input.axisU.w);
                if (smearing) dist += fading * (0.2 + 0.7 * Noise2(float2(pathAt * 2.1, p.y * 2.7 + 1.9)));
                float edge = max(fwidth(dist), 0.003);
                float alpha = smoothstep(edge, -edge, dist) * (1.0 - fading);
                float washed = map ? 0.0 : Washed(positionWS, input.life.x);
                alpha *= 1.0 - washed;
                if (input.life.z > 1.5 && input.life.z < 2.5) alpha *= 0.35 + 0.65 * PrintStrength(input.life.w);
                clip(alpha - 0.004);

                float dry = max(smoothstep(_DryStart, max(_DryStart + 0.001, _DryEnd), saturate(age / max(0.01, span))), saturate(look));
                if (residue > 0.0)
                {
                    dry = max(dry, 0.85);
                    alpha *= 0.6;
                }
                dry *= 1.0 - washed;
                float volume = (1.0 - dry) * (folded ? 0.35 : 1.0);
                if (input.life.z < 0.5 && input.life.w > 4.5)
                {
                    volume *= lerp(0.4, 1.0, wetSmear);
                    float code = (input.life.w - 5.0) * 4.0;
                    float smudge = Noise2(float2(pathAt * 1.3, p.y * 1.2 + 4.1)) * 0.65 + Noise2(float2(pathAt * 5.0, p.y * 4.5 + 9.3)) * 0.35;
                    alpha *= lerp(lerp(0.75, 1.0, saturate((code - floor(code)) / 0.96)) * (0.78 + 0.22 * smudge) * (0.72 + 0.28 * Noise2(float2(p.y * 15.0, pathAt * 2.5 + 3.0))), 1.0, wetSmear);
                    // dried blood is dark and dense where it lies, not a faint wash
                    alpha = min(1.0, alpha * lerp(1.0, 1.45, saturate(look)));
                }
                if (input.life.z > 0.5 && input.life.z < 1.5)
                {
                    float unfilled = 1.0 - saturate(age / max(0.01, input.life.w));
                    volume *= 1.0 - _PoolFlatten * (1.0 - unfilled * unfilled * unfilled);
                }

                float rim = 1.0 - saturate(-dist / max(0.001, _BevelWidth * (0.5 + 0.5 * volume)));
                float bevel = _BLOOD_MEDIUM || _BLOOD_LOW || map ? 0.0 : rim * _Bevel * volume;
                float3 normal = normalize(surfaceNormal);
                if (map && !_BLOOD_MEDIUM) normal = normalize(surfaceNormal - (input.axisU.xyz * mapSlope.x + input.axisV.xyz * mapSlope.y) * (_Bevel * 0.6 * volume));
                [branch]
                if (bevel != 0.0)
                {
                    float nudge = 0.02;
                    float distU = Shape(p + float2(nudge, 0.0), input.life, seed, age, stretch, kit, frame);
                    float distV = Shape(p + float2(0.0, nudge), input.life, seed, age, stretch, kit, frame);
                    float3 slope = input.axisU.xyz * ((distU - dist) / nudge) + input.axisV.xyz * ((distV - dist) / nudge);
                    normal = normalize(surfaceNormal + slope * bevel);
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = normalize(toCamera);
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = ComputeFogFactor(TransformWorldToHClip(positionWS).z);
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_BloodColor.rgb, Dried(_BloodColor.rgb), dry) * (smearing ? lerp(lerp(0.58, 0.8, saturate(-dist * 5.0)), lerp(1.0, 0.74, saturate(-dist * 2.5)), wetSmear) : lerp(1.0, 0.74, saturate(-dist * 2.5) * (1.0 - 0.5 * dry)));
                surface.albedo *= lerp(1.0, 0.5, dry * (1.0 - saturate(-dist / 0.05)));
                // washed blood runs thin and pink
                surface.albedo = lerp(surface.albedo, surface.albedo * 1.6 + float3(0.06, 0.015, 0.02), washed * 0.8);
                surface.specular = _SpecularColor.rgb * lerp(1.0, 0.15, sqrt(dry));
                surface.smoothness = lerp(_Smoothness, _DryGloss, dry);
                surface.occlusion = lerp(_Reflections, 1.0, dry);
                surface.emission = _BloodColor.rgb * _Glow * (1.0 - dry);
                surface.alpha = alpha;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, alpha * _BloodColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
