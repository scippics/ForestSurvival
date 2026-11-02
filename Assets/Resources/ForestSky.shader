// Небо. У горизонта оно равно цвету тумана: лес растворяется в нём без видимой границы.
Shader "Forest/Sky"
{
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ForestNoise;
            float3 _ForestSunDirection;
            float3 _ForestSunColor;
            float3 _ForestSkyZenith;
            float _ForestNight;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float up = max(direction.y, 0.0);
                float3 color = lerp(unity_FogColor.rgb, _ForestSkyZenith, smoothstep(0.03, 0.55, up));

                float2 cloudUv = direction.xz / (up + 0.25) * 0.11 + _Time.y * float2(0.0016, 0.0009);
                float cloudShape = tex2D(_ForestNoise, cloudUv).r * 0.62 + tex2D(_ForestNoise, cloudUv * 2.7 + 0.3).g * 0.38;
                float cover = smoothstep(0.4, 0.68, cloudShape) * smoothstep(0.02, 0.22, up);

                // Звёзды и луна не видны сквозь облака и днём.
                float3 starCell = floor(direction * 320.0);
                float starHash = frac(sin(dot(starCell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float stars = step(0.9965, starHash) * frac(starHash * 311.7) * smoothstep(0.02, 0.3, up);
                float moon = dot(direction, -_ForestSunDirection);
                float3 night = stars * float3(0.5, 0.55, 0.7)
                    + float3(0.75, 0.82, 1.0) * (smoothstep(0.9993, 0.99955, moon) * 1.4 + pow(saturate(moon), 300.0) * 0.1);
                color += night * _ForestNight * (1.0 - cover * 0.9);

                float3 cloudColor = lerp(_ForestSkyZenith * 0.55, unity_FogColor.rgb * 1.25, cloudShape);
                color = lerp(color, cloudColor, cover * 0.85);

                // Широкое гало, потому что солнце видно сквозь дымку, и само пятно.
                float sun = saturate(dot(direction, _ForestSunDirection));
                color += _ForestSunColor * (pow(sun, 6.0) * 0.1 + pow(sun, 60.0) * 0.32 + smoothstep(0.9988, 0.9994, sun) * 5.0 * (1.0 - cover * 0.75));

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
