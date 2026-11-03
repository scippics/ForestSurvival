// Стекло кабины: почти прозрачное в упор, отражает небо под углом и покрыто разводами пыли.
// В дождь на нём сидят и стекают капли: каждая преломляет то, что за стеклом.
Shader "Forest/Glass"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        GrabPass { "_ForestGlassGrab" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ForestNoise;
            float4x4 _ForestVehicleWorldToLocal;
            sampler2D _ForestGlassGrab;
            sampler2D _ForestWindshieldWater;
            float _ForestRain;
            float _ForestGlassRun;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                float3 local : TEXCOORD2;
                float4 grab : TEXCOORD3;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Капля, ползущая по колонке, с дорожкой мелких капель над собой. xy — сдвиг картинки, z — маска, w — блик.
            float4 Running(float2 uv, float run, float t, float scale, float visible)
            {
                float2 cell = float2(0.085, 0.26) * scale;
                float2 g = uv / cell;
                float column = floor(g.x);
                g.y += run * (0.75 + 0.5 * Hash(float2(column, 1.0))) / cell.y + Hash(float2(column, 7.0)) * 9.0;

                float2 id = floor(g);
                float2 st = frac(g) - 0.5;
                if (Hash(id) >= visible)
                    return 0.0;

                float seed = Hash(id + 3.1);
                float x = (seed - 0.5) * 0.4 + sin(uv.y * 34.0 + seed * 20.0) * 0.05;
                float y = sin(t * (0.6 + seed) + seed * 40.0) * 0.12 - 0.1;
                float stretch = cell.y / cell.x;

                float2 d = float2(st.x - x, (st.y - y) * stretch);
                d.x *= 1.0 + saturate(d.y * 3.0) * 0.9;
                float radius = 0.13 + 0.08 * seed;
                float drop = smoothstep(radius, radius * 0.72, length(d));

                float row = st.y * 9.0 + seed * 4.0;
                float above = smoothstep(y + 0.02, y + 0.1, st.y) * smoothstep(0.5, 0.12, st.y);
                float2 td = float2(st.x - x, (frac(row) - 0.5) / 9.0 * stretch);
                float trail = smoothstep(0.06, 0.03, length(td)) * above * step(0.45, Hash(float2(id.x + floor(row), id.y)));

                float shine = drop * smoothstep(0.05, 0.015, length(d - float2(-0.05, 0.06)));
                return float4(d * drop + td * trail, saturate(drop + trail), shine);
            }

            // Сидящие капли: появляются, живут и сменяются новыми от следующего удара дождя.
            float4 Beads(float2 uv, float size, float t, float visible)
            {
                float2 g = uv / size;
                float2 id = floor(g);
                float2 st = frac(g) - 0.5;
                float seed = Hash(id);

                float life = t * 0.18 + seed * 9.0;
                if (Hash(id + floor(life) * 0.37) >= visible)
                    return 0.0;

                float2 d = st - (float2(Hash(id + 1.7), Hash(id + 2.9)) - 0.5) * 0.5;
                float radius = 0.1 + 0.2 * Hash(id + 4.3);
                float bead = smoothstep(radius, radius * 0.7, length(d)) * smoothstep(1.0, 0.85, frac(life));
                float shine = bead * smoothstep(0.05, 0.015, length(d - float2(-0.03, 0.04)));
                return float4(d * bead, bead, shine);
            }

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - world;
                o.local = mul(_ForestVehicleWorldToLocal, float4(world, 1.0)).xyz;
                o.grab = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                float fresnel = pow(1.0 - facing, 3.0);
                float dust = tex2D(_ForestNoise, i.local.xy * 0.9 + i.local.z).g;
                dust = smoothstep(0.55, 0.85, dust) * 0.035;
                float3 color = unity_FogColor.rgb * 1.6;
                float alpha = 0.02 + dust + fresnel * 0.45;

                // На лобовом стекле вода своя: её стирают щётки. На остальных стёклах она просто идёт за дождём.
                bool front = i.local.z > 0.9;
                float wet = front
                    ? tex2D(_ForestWindshieldWater, float2(i.local.x / 1.68 + 0.5, (i.local.y - 0.2) / 0.66)).r
                    : _ForestRain;

                if (wet > 0.01)
                {
                    float2 uv = float2(i.local.x + i.local.z * 0.6, i.local.y);
                    float run = front ? _ForestGlassRun : _Time.y * 0.13;
                    float t = _Time.y;

                    float4 drops = Running(uv, run, t, 1.0, saturate((wet - 0.35) * 1.6) * 0.75)
                        + Running(uv + float2(0.37, 0.11), run * 0.7, t * 0.8, 0.55, saturate((wet - 0.2) * 1.6) * 0.55)
                        + Beads(uv, 0.026, t, saturate(wet * 1.3) * 0.8)
                        + Beads(uv + 0.19, 0.011, t * 1.4, saturate(wet * 1.5) * 0.6);

                    float mask = saturate(drops.z);
                    float3 through = tex2D(_ForestGlassGrab, i.grab.xy / i.grab.w + drops.xy * 0.35).rgb;
                    color = lerp(color, through * 1.08 + unity_FogColor.rgb * saturate(drops.w) * 1.2, mask);
                    alpha = max(alpha + wet * 0.02, mask);
                }

                return fixed4(color, saturate(alpha));
            }
            ENDCG
        }
    }
}
