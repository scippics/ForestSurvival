// Небо. У горизонта оно равно цвету тумана: лес растворяется в нём без видимой границы.
// Облака плывут по ветру и густеют к дождю. В грозу тучи подсвечиваются вспышками изнутри,
// а близкая молния рисуется прямо на небе ломаной линией с отростком.
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

            // Погода: доля неба под облаками, сдвиг облаков ветром, сила вспышки и молния.
            float _ForestCloudCover;
            float2 _ForestCloudOffset;
            float _ForestFlash;
            float4 _ForestBolt;      // xz — направление на удар, y — высота основания туч, w — яркость канала
            float4 _ForestBoltShape; // x — зерно, y — наклон, z — толщина, w — наклон отростка

            static const float3 FlashColor = float3(0.78, 0.84, 1.0);

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

            float Hash(float p)
            {
                return frac(sin(p * 127.1 + _ForestBoltShape.x * 311.7) * 43758.5453);
            }

            float Wiggle(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i), Hash(i + 1.0), f) * 2.0 - 1.0;
            }

            // Ломаная канала: крупные изгибы и мелкие зазубрины по высоте.
            float BoltPath(float e)
            {
                return Wiggle(e * 14.0) * 0.03 + Wiggle(e * 46.0 + 7.0) * 0.011 + Wiggle(e * 140.0 + 3.0) * 0.0035;
            }

            float BoltLine(float offset, float width)
            {
                float d = abs(offset);
                return saturate(1.0 - d / width) + exp(-d / (width * 5.0)) * 0.3;
            }

            float Bolt(float azimuth, float elevation)
            {
                float top = _ForestBolt.y;
                float width = _ForestBoltShape.z;
                float tilt = _ForestBoltShape.y;
                float path = BoltPath(elevation) + (top - elevation) * tilt;
                float body = BoltLine(azimuth - path, width) * smoothstep(-0.06, -0.01, elevation) * smoothstep(top + 0.01, top - 0.01, elevation);

                // Отросток уходит вбок от трети высоты и тает к концу.
                float start = top * 0.62;
                float end = top * 0.22;
                float along = saturate((start - elevation) / (start - end));
                float branchPath = BoltPath(start) + (top - start) * tilt + (start - elevation) * _ForestBoltShape.w + Wiggle(elevation * 90.0 + 21.0) * 0.006;
                float branch = BoltLine(azimuth - branchPath, width * 0.6) * step(end, elevation) * step(elevation, start) * (1.0 - along) * 0.6;
                return max(body, branch);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float up = max(direction.y, 0.0);
                float3 color = lerp(unity_FogColor.rgb, _ForestSkyZenith, smoothstep(0.03, 0.55, up));

                // Насколько направление близко к месту удара: туча там светится сильнее.
                float azimuth = atan2(direction.x, direction.z);
                float boltAzimuth = atan2(_ForestBolt.x, _ForestBolt.z);
                float side = azimuth - boltAzimuth;
                side -= 6.2831853 * round(side / 6.2831853);
                float nearStrike = exp(-abs(side) * 2.6);

                float2 cloudUv = direction.xz / (up + 0.2) * 0.1 + _ForestCloudOffset;
                float cloudShape = tex2D(_ForestNoise, cloudUv).r * 0.5
                    + tex2D(_ForestNoise, cloudUv * 2.3 + 0.31).g * 0.33
                    + tex2D(_ForestNoise, cloudUv * 6.1 + 0.67).b * 0.17;

                // Чем пасмурнее, тем ниже порог: разрывы между облаками затягиваются сплошной пеленой.
                float edge = lerp(0.6, 0.12, _ForestCloudCover);
                float cover = smoothstep(edge, edge + 0.24, cloudShape) * smoothstep(0.02, 0.2, up);

                // Звёзды и луна не видны сквозь облака и днём.
                float3 starCell = floor(direction * 320.0);
                float starHash = frac(sin(dot(starCell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float stars = step(0.9965, starHash) * frac(starHash * 311.7) * smoothstep(0.02, 0.3, up);
                float moon = dot(direction, -_ForestSunDirection);
                float3 night = stars * float3(0.5, 0.55, 0.7)
                    + float3(0.75, 0.82, 1.0) * (smoothstep(0.9993, 0.99955, moon) * 1.4 + pow(saturate(moon), 300.0) * 0.1);
                color += night * _ForestNight * (1.0 - cover * 0.95) * (1.0 - _ForestCloudCover * 0.7);

                // Плотная часть облака темнее краёв, грозовая туча темнее обычной.
                float heavy = _ForestCloudCover * _ForestCloudCover;
                float3 cloudLight = unity_FogColor.rgb * lerp(1.3, 0.85, heavy);
                float3 cloudDark = lerp(_ForestSkyZenith * 0.6, unity_FogColor.rgb * 0.62, heavy);
                float3 cloudColor = lerp(cloudDark, cloudLight, saturate(1.0 - (cloudShape - edge) * lerp(2.2, 1.3, heavy)));
                cloudColor += FlashColor * _ForestFlash * (0.08 + 0.55 * nearStrike) * cloudShape;
                color = lerp(color, cloudColor, cover * lerp(0.85, 0.97, _ForestCloudCover));

                // Небо в разрывах туч тоже вспыхивает, сильнее всего над местом удара.
                color += FlashColor * _ForestFlash * (0.03 + 0.12 * nearStrike) * smoothstep(-0.05, 0.25, direction.y);

                // Широкое гало, потому что солнце видно сквозь дымку, и само пятно. Сплошная пелена его прячет.
                float sun = saturate(dot(direction, _ForestSunDirection));
                float sunVisible = (1.0 - cover * 0.75) * (1.0 - heavy * 0.85);
                color += _ForestSunColor * (pow(sun, 6.0) * 0.1 + pow(sun, 60.0) * 0.32 * sunVisible + smoothstep(0.9988, 0.9994, sun) * 5.0 * sunVisible);

                if (_ForestBolt.w > 0.001)
                    color += FlashColor * Bolt(side, asin(direction.y)) * _ForestBolt.w * 5.0;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
