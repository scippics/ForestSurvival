// Земля из чанков. Вершины несут данные, а не цвет: красный — глубина грязи, зелёный — вытоптанная
// поляна, texcoord.x — смещение от оси дороги в метрах. Фактура собрана из общей текстуры шума
// в мировых координатах, поэтому швов на стыках чанков нет. Лежит в Resources, чтобы попасть в сборку.
Shader "Forest/Ground"
{
    Properties
    {
        _MossColor ("Moss", Color) = (0.115, 0.14, 0.075, 1)
        _LitterColor ("Needle Litter", Color) = (0.16, 0.12, 0.085, 1)
        _SoilColor ("Soil", Color) = (0.075, 0.055, 0.04, 1)
        _DirtColor ("Trampled Dirt", Color) = (0.17, 0.14, 0.11, 1)
        _RoadColor ("Road", Color) = (0.15, 0.125, 0.1, 1)
        _MudColor ("Mud", Color) = (0.055, 0.04, 0.028, 1)
        _RoadHalfWidth ("Road Half Width", Float) = 5.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        // addshadow обязателен: тени считаются по карте глубины из этого прохода. Без него
        // земли в карте нет, и на ней проступают тени всего, что скрыто за холмом и под землёй.
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
            float3 worldPos;
            float3 viewDir;
            float roadOffset;
        };

        sampler2D _ForestNoise;
        fixed4 _MossColor, _LitterColor, _SoilColor, _DirtColor, _RoadColor, _MudColor;
        float _RoadHalfWidth;
        float _ForestRain;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.roadOffset = v.texcoord.x;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 xz = IN.worldPos.xz;
            float macro = tex2D(_ForestNoise, xz * 0.017).r;
            float patches = tex2D(_ForestNoise, xz * 0.061).g;
            float fine = tex2D(_ForestNoise, xz * 0.37).b;
            float grit = tex2D(_ForestNoise, xz * 1.9).b;

            // Лесная подстилка: пятна мха и хвои поверх тёмной почвы.
            float litter = smoothstep(0.38, 0.62, macro * 0.55 + patches * 0.45);
            float3 forest = lerp(_MossColor.rgb * (0.82 + 0.36 * fine), _LitterColor.rgb * (0.8 + 0.4 * grit), litter);
            forest = lerp(forest, _SoilColor.rgb, smoothstep(0.6, 0.78, fine) * 0.55);

            float bare = saturate(IN.color.g * 1.6 - (patches - 0.5) * 1.2 - 0.3);
            forest = lerp(forest, _DirtColor.rgb * (0.85 + 0.3 * grit), bare);

            // Край дороги рваный: граница смещена шумом, а не идёт ровной линией сетки.
            float depth = saturate(IN.color.r);
            float offset = IN.roadOffset + (macro - 0.5) * 1.2;
            float road = smoothstep(_RoadHalfWidth + 0.4, _RoadHalfWidth - 1.5, abs(offset) + (patches - 0.5) * 2.0) * saturate(depth * 10.0);

            // Две колеи на ширину колёс и трава между ними.
            float rut = smoothstep(0.62, 0.14, abs(abs(offset) - 0.98));
            float crown = smoothstep(0.55, 0.1, abs(offset)) * smoothstep(0.35, 0.6, fine);

            float wet = saturate(depth * 0.9 + rut * 0.45 + (patches - 0.5) * 0.7 + _ForestRain * 0.35);
            float puddle = smoothstep(0.62 - _ForestRain * 0.16, 0.74 - _ForestRain * 0.16, depth * 0.55 + rut * 0.34 + (patches - 0.5) * 0.55) * road;
            float3 roadAlbedo = lerp(_RoadColor.rgb * (0.88 + 0.24 * grit), _MudColor.rgb, wet);
            roadAlbedo = lerp(roadAlbedo, _MossColor.rgb * 0.8, crown * (1.0 - wet) * 0.8);

            o.Albedo = lerp(forest, roadAlbedo, road);
            o.Albedo *= 1.0 - puddle * 0.45;
            o.Smoothness = lerp(lerp(0.06, 0.12, bare), lerp(0.12, 0.42, wet), road);
            o.Smoothness = lerp(o.Smoothness, 0.93, puddle);

            // В дождь земля темнеет и блестит везде, а не только на дороге.
            o.Albedo *= 1.0 - _ForestRain * 0.28;
            o.Smoothness = saturate(o.Smoothness + _ForestRain * 0.22);

            // Мелкий рельеф из разности шума; в луже поверхность гладкая.
            const float texel = 0.012;
            float2 uv = xz * 0.55;
            float height = tex2D(_ForestNoise, uv).b;
            float dx = tex2D(_ForestNoise, uv + float2(texel, 0)).b - height;
            float dz = tex2D(_ForestNoise, uv + float2(0, texel)).b - height;
            o.Normal = normalize(float3(-dx * 0.9, dz * 0.9, 1.0) * float3(1.0 - puddle, 1.0 - puddle, 1.0));

            // Фары светят почти вдоль земли: под таким углом любой рельеф даёт чёрные пятна вместо тени.
            #ifdef UNITY_PASS_FORWARDADD
                o.Normal = float3(0, 0, 1);
            #endif

            // Неба в отражениях нет, поэтому мокрая земля под углом отражает цвет тумана.
            float fresnel = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), 4.0);
            o.Emission = unity_FogColor.rgb * fresnel * road * (wet * 0.25 + puddle * 0.75);
            o.Alpha = 1;
        }
        ENDCG
    }
}
