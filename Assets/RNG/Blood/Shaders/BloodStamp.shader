Shader "Hidden/RNG/Blood Stamp"
{
    Properties
    {
        _SplatSeconds ("Splat Seconds", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _SplatSeconds;
        CBUFFER_END

        float4 _StampShape;
        float4 _StampValue;
        float4 _ShiftOffset;
        TEXTURE2D(_BloodMapSource);
        SAMPLER(sampler_BloodMapSource);

        #include "BloodShapes.hlsl"
        ENDHLSL

        Pass
        {
            Name "Stamp"
            Blend One One
            BlendOp Max
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.local = input.positionOS.xy;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float seed = _StampShape.x;
                float stretch = _StampShape.y;
                float4 life = float4(0.0, 1.0, 0.0, _StampShape.z);
                float2 spots[9];
                float sizes[9];
                float tail;
                Specks(seed, stretch, spots, sizes, tail);
                Kit kit;
                kit.phase = Phase(seed, 0.0);
                [unroll]
                for (int i = 0; i < 9; i++)
                {
                    kit.spots[i] = spots[i];
                    kit.sizes[i] = sizes[i];
                }

                kit.tail = tail;
                float dist = Shape(input.local, life, seed, 10.0, stretch, kit, float4(0.0, 1.0, 1.0, 0.0));
                float edge = max(fwidth(dist), 0.003);
                float cover = smoothstep(edge, -edge, dist);
                clip(cover - 0.01);
                return float4(cover, _StampValue.x, _StampValue.y, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Shift"
            Blend Off
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.positionOS.xy * 0.5 + 0.5;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv + _ShiftOffset.xy;
                if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;

                return SAMPLE_TEXTURE2D_LOD(_BloodMapSource, sampler_BloodMapSource, uv, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Liquid"
            Blend Off
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _LiquidFeeds[16];
            float4 _LiquidFeedHeights[16];
            float _LiquidFeedCount;
            float4 _LiquidStep;
            float4 _LiquidFrame;
            float4 _LiquidLife;
            TEXTURE2D(_BloodLiquidSource);
            SAMPLER(sampler_BloodLiquidSource);
            TEXTURE2D(_LiquidChannels);
            SAMPLER(sampler_LiquidChannels);
            float4 _LiquidChannelFrame;
            float _LiquidChannelDepth;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.positionOS.xy * 0.5 + 0.5;
                return output;
            }

            float4 Read(float2 uv)
            {
                if (any(uv < 0.0) || any(uv > 1.0)) return float4(0.0, 0.0, 0.0, 0.0);
                return SAMPLE_TEXTURE2D_LOD(_BloodLiquidSource, sampler_BloodLiquidSource, uv, 0);
            }

            #define FloorShare 1.75
            #define GrooveShare 0.44

            // 1 inside a groove cut into the floor, 0 on the open floor
            float Channel(float2 uv)
            {
                if (_LiquidChannelDepth <= 0.0) return 0.0;

                float2 world = float2(_LiquidFrame.x + uv.x * _LiquidFrame.z, _LiquidFrame.y - uv.y * _LiquidFrame.z);
                float2 c = float2((world.x - _LiquidChannelFrame.x) / _LiquidChannelFrame.z, (world.y - (_LiquidChannelFrame.y - _LiquidChannelFrame.w)) / _LiquidChannelFrame.w);
                if (any(c < 0.0) || any(c > 1.0)) return 0.0;
                return SAMPLE_TEXTURE2D_LOD(_LiquidChannels, sampler_LiquidChannels, c, 0).r;
            }

            float Film(float2 uv)
            {
                float2 world = float2(_LiquidFrame.x + uv.x * _LiquidFrame.z, _LiquidFrame.y - uv.y * _LiquidFrame.z);
                float rough = Noise2(world * 6.0) * 0.65 + Noise2(world * 23.0 + 4.3) * 0.35;
                // a groove holds almost no film: what runs into it keeps running
                return _LiquidStep.y * (0.62 + 0.76 * rough) * (1.0 - 0.92 * Channel(uv));
            }

            float Speed(float2 uv)
            {
                float2 world = float2(_LiquidFrame.x + uv.x * _LiquidFrame.z, _LiquidFrame.y - uv.y * _LiquidFrame.z);
                return (0.3 + 1.35 * (Noise2(world * 3.3 + 7.1) * 0.7 + Noise2(world * 11.0 + 2.9) * 0.3)) * (1.0 + 9.0 * Channel(uv));
            }

            float Excess(float4 texel, float film)
            {
                return max(texel.r - film, 0.0);
            }

            void Exchange(float4 centre, float centreFilm, float centreSpeed, float centreFloor, float4 other, float otherFilm, float otherSpeed, float otherFloor, float share, inout float flow, inout float height, inout float wetTime, inout float heaviest)
            {
                bool otherWet = other.r > 0.0001 || other.a > 0.001;
                bool centreWet = centre.r > 0.0001 || centre.a > 0.001;
                if (otherWet && centreWet && abs(other.b - centre.b) > 0.08) return;

                // liquid levels over the floor, a groove sits lower. What moves is limited to a share of what the giving texel holds,
                // small enough that all its neighbours together never take more than it has: otherwise liquid would be made from
                // nothing. A dry texel next to a groove gives nothing, and both texels of a pair agree on the amount.
                float centreExcess = Excess(centre, centreFilm);
                float otherExcess = Excess(other, otherFilm);
                float give = ((otherExcess + otherFloor) - (centreExcess + centreFloor)) * 0.5 * (centreSpeed + otherSpeed);
                give = clamp(give, -centreExcess * share, otherExcess * share);
                flow += give;
                if (give > 0.0 && other.r > heaviest)
                {
                    heaviest = other.r;
                    height = other.b;
                    wetTime = max(wetTime, other.g);
                }
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _LiquidFrame.w;
                float4 centre = Read(uv);
                float film = Film(uv);
                float flow = 0.0;
                float height = centre.b;
                float wetTime = centre.g;
                float heaviest = 0.0;
                float speed = Speed(uv);
                float ground = -Channel(uv) * _LiquidChannelDepth;
                float2 offsets[4] = { float2(t, 0.0), float2(-t, 0.0), float2(0.0, t), float2(0.0, -t) };
                [unroll]
                for (int n = 0; n < 4; n++)
                {
                    float2 at = uv + offsets[n];
                    float otherGround = -Channel(at) * _LiquidChannelDepth;
                    // a texel in a groove trades with up to sixteen others (four next to it, twelve down the fast lanes), one on the open
                    // floor with four: 0.14 of the flow is applied, so the shares keep the sum of what a texel gives under what it holds
                    bool grooved = ground < -0.5 * _LiquidChannelDepth || otherGround < -0.5 * _LiquidChannelDepth;
                    Exchange(centre, film, speed, ground, Read(at), Film(at), Speed(at), otherGround, grooved ? GrooveShare : FloorShare, flow, height, wetTime, heaviest);
                }

                if (ground < -0.5 * _LiquidChannelDepth)
                {
                    // lanes 4, 12 and 36 texels long: liquid runs metres down a groove in a second instead of seeping. A lane only
                    // links texels joined by groove all the way (checked at its quarters), so it never jumps across the floor into
                    // a separate drain.
                    [unroll]
                    for (int lane = 0; lane < 12; lane++)
                    {
                        float reach = lane < 4 ? 4.0 : lane < 8 ? 12.0 : 36.0;
                        float2 step = offsets[lane % 4] * reach;
                        float2 at = uv + step;
                        float otherGround = -Channel(at) * _LiquidChannelDepth;
                        if (otherGround > -0.5 * _LiquidChannelDepth) continue;
                        if (Channel(uv + step * 0.25) < 0.5 || Channel(uv + step * 0.5) < 0.5 || Channel(uv + step * 0.75) < 0.5) continue;

                        Exchange(centre, film, speed, ground, Read(at), Film(at), Speed(at), otherGround, GrooveShare, flow, height, wetTime, heaviest);
                    }
                }

                float dt = _LiquidStep.x;
                float now = _LiquidStep.w;
                float volume = max(0.0, centre.r + 0.14 * flow);
                bool wasWet = centre.r > 0.0001 || centre.a > 0.001;
                if (!wasWet && volume > 0.0)
                {
                    centre.b = height;
                    centre.g = wetTime;
                }

                float inflow = 0.0;
                for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_LiquidFeedCount) break;

                    float4 feed = _LiquidFeeds[i];
                    float2 offset = (uv - feed.xy) / max(feed.z, 0.00001);
                    float q = saturate(1.0 - dot(offset, offset));
                    if (q <= 0.0) continue;

                    inflow += feed.w * q * q;
                    if (!wasWet && volume <= 0.0) centre.b = _LiquidFeedHeights[i].x;
                }

                if (inflow > 0.0)
                {
                    volume += inflow * dt;
                    centre.g = now;
                }

                float age = now - centre.g;
                if (age > _LiquidLife.x) volume = max(0.0, volume - _LiquidStep.z * dt);
                float residue = max(centre.a, saturate((volume - film * 0.7) / max(0.0001, film * 0.5)));
                if (age > _LiquidLife.y) residue = max(0.0, residue - dt / _LiquidLife.z);
                if (residue <= 0.0 && volume <= 0.0) return float4(0.0, 0.0, 0.0, 0.0);

                return float4(volume, centre.g, centre.b, residue);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Drag"
            Blend Off
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _LiquidDrags[8];
            float4 _LiquidDragSizes[8];
            float _LiquidDragCount;
            float4 _LiquidFrame;
            TEXTURE2D(_BloodLiquidSource);
            SAMPLER(sampler_BloodLiquidSource);

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.positionOS.xy * 0.5 + 0.5;
                return output;
            }

            float4 Read(float2 uv)
            {
                if (any(uv < 0.0) || any(uv > 1.0)) return float4(0.0, 0.0, 0.0, 0.0);
                return SAMPLE_TEXTURE2D_LOD(_BloodLiquidSource, sampler_BloodLiquidSource, uv, 0);
            }

            // liquid under a dragged object moves with it: each texel inside the swept band takes what lay behind it, more in streaks
            // along the drag than between them, so the pool is wiped into lines and thins where the object passed
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float4 centre = Read(uv);
                float2 shift = 0.0;
                float wipe = 0.0;
                for (int i = 0; i < 8; i++)
                {
                    if (i >= (int)_LiquidDragCount) break;

                    float2 a = _LiquidDrags[i].xy;
                    float2 b = _LiquidDrags[i].zw;
                    float2 ab = b - a;
                    float len = max(length(ab), 1e-5);
                    float2 dir = ab / len;
                    float along = dot(uv - a, dir);
                    float across = dot(uv - a, float2(-dir.y, dir.x));
                    float half = _LiquidDragSizes[i].x;
                    float seed = _LiquidDragSizes[i].w;
                    // measured along the floor, not from this frame's start, so the streaks and the ragged sides stay where they are
                    float track = dot(uv, dir) / half;
                    half *= 0.7 + 0.45 * Noise2(float2(track * 1.3, (across > 0.0 ? 3.0 : 7.0) + seed));
                    if (along < -half || along > len + half || abs(across) > half) continue;

                    float edge = saturate(1.0 - abs(across) / half);
                    float2 world = float2(across / _LiquidDragSizes[i].x * 7.0 + seed, track * 0.4);
                    float streak = Noise2(world * float2(1.0, 0.3)) * 0.75 + Noise2(world * float2(3.1, 0.6) + 5.3) * 0.25;
                    float k = _LiquidDragSizes[i].y * smoothstep(0.0, 0.35, edge) * lerp(0.35, 1.0, streak);
                    shift += ab * k;
                    wipe = max(wipe, saturate(k * 1.6) * smoothstep(0.35, 0.75, 1.0 - streak));
                }

                if (dot(shift, shift) <= 0.0) return centre;

                float4 moved = Read(uv - shift);
                // the pool closes in behind what was pushed through it, except along the streaks it wiped
                float4 result = moved;
                result.r = max(moved.r, centre.r * 0.8) * (1.0 - 0.85 * wipe);
                if (centre.r > 0.0001 || centre.a > 0.001) result.b = centre.b;
                result.a = max(centre.a, moved.a);
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Drain"
            Blend Off
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _BloodDrainA[16];
            float4 _BloodDrainB[16];
            float _BloodDrainCount;
            TEXTURE2D(_BloodLiquidSource);
            SAMPLER(sampler_BloodLiquidSource);

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.positionOS.xy * 0.5 + 0.5;
                return output;
            }

            // water washing over the floor takes liquid and painted blood with it: each area keeps the share it was given
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float4 texel = SAMPLE_TEXTURE2D_LOD(_BloodLiquidSource, sampler_BloodLiquidSource, uv, 0);
                float keep = 1.0;
                for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_BloodDrainCount) break;

                    float2 a = _BloodDrainA[i].xy;
                    float2 b = _BloodDrainA[i].zw;
                    float2 ab = b - a;
                    float t = saturate(dot(uv - a, ab) / max(1e-8, dot(ab, ab)));
                    float r = _BloodDrainB[i].x;
                    float d = length(uv - a - ab * t) + (Noise2(uv * 180.0) - 0.5) * r * 0.25;
                    float inside = 1.0 - smoothstep(r * 0.75, r, d);
                    keep = min(keep, lerp(1.0, _BloodDrainB[i].y, inside));
                }

                texel.r *= keep;
                texel.a *= keep;
                return texel;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
