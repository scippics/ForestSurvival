// Ветер и материал растительности. Вершины с Y <= 0 в пространстве модели неподвижны, поэтому
// начало координат модели должно стоять у основания. Красный канал цвета вершин — затенение
// внутри кроны и у корней, зелёный — кончик ветки. Лежит в Resources, чтобы попасть в сборку.
Shader "Forest/CustomWindFoliage"
{
    Properties
    {
        _Color ("Color", Color) = (0.17, 0.19, 0.11, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.05
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1
        _FlutterStrength ("Flutter Strength", Range(0, 0.2)) = 0.02
        _Bark ("Bark", Range(0, 1)) = 0
        _Needles ("Needles", Range(0, 1)) = 0
        _Grass ("Grass", Range(0, 1)) = 0
        _Moss ("Moss", Range(0, 1)) = 0
        _Birch ("Birch Bark", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        // Пучки травы — плоские полосы: без этого их обратная сторона была бы невидима.
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        #include "ForestDynamicLights.cginc"

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float3 viewDir;
            float4 color : COLOR;
            float variation;
            float height;
            float2 treeOrigin;
        };

        sampler2D _ForestNoise;
        float3 _ForestSunDirection;
        float3 _ForestSunColor;
        half _Smoothness;
        float _WindStrength;
        float _WindSpeed;
        float _FlutterStrength;
        half _Bark, _Needles, _Grass, _Moss, _Birch;

        UNITY_INSTANCING_BUFFER_START(FoliageProps)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
        UNITY_INSTANCING_BUFFER_END(FoliageProps)

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            UNITY_SETUP_INSTANCE_ID(v);

            // Смещение растёт квадратично: ствол гнётся у вершины, а не наклоняется целиком.
            float height = max(v.vertex.y, 0.0);
            float bend = height * height;

            // Фаза от положения экземпляра: у каждого дерева свои порывы.
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float phase = dot(origin.xz, float2(0.37, 0.71));
            float t = _Time.y * _WindSpeed;

            // Разные частоты по осям: вершина ходит по неровной кривой, а не вдоль одной линии.
            float gustX = sin(t + phase) + 0.5 * sin(t * 2.3 + phase * 1.7) + 0.3 * sin(t * 0.41 + phase * 0.5);
            float gustZ = sin(t * 0.8 + phase * 1.3) + 0.4 * sin(t * 1.9 + phase * 0.6);

            // Мелкая дрожь отдельных вершин поверх общего порыва.
            float flutter = sin(t * 6.1 + phase + dot(v.vertex.xyz, float3(3.1, 1.7, 2.3)));

            float3 worldOffset = float3(gustX + flutter * _FlutterStrength / max(_WindStrength, 0.001), 0, gustZ) * (_WindStrength * bend);
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, worldOffset);

            // Свой оттенок у каждого экземпляра: лес не выглядит копией одного дерева.
            o.variation = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
            o.height = height;
            o.treeOrigin = origin.xz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = UNITY_ACCESS_INSTANCED_PROP(FoliageProps, _Color).rgb;
            float occlusion = IN.color.r;
            float tip = IN.color.g;
            float3 world = IN.worldPos;

            albedo *= lerp(0.8, 1.2, IN.variation);
            albedo.r *= lerp(0.88, 1.18, frac(IN.variation * 7.31));

            if (_Bark > 0)
            {
                float fibre = tex2D(_ForestNoise, float2((world.x + world.z) * 0.22, world.y * 0.1)).a;
                albedo *= 0.55 + 0.9 * fibre;
            }

            if (_Birch > 0)
            {
                // Белая кора с короткими чёрными поперечными штрихами, у комля темнее.
                float dash = tex2D(_ForestNoise, float2(world.y * 0.11, (world.x + world.z) * 0.035)).a;
                albedo = lerp(albedo, float3(0.012, 0.011, 0.01), smoothstep(0.6, 0.68, dash));
                albedo *= lerp(0.45, 1.0, saturate(IN.height * 7.0));
            }

            if (_Needles > 0)
            {
                // Хвоя идёт от ствола наружу, поэтому полосы расходятся лучами вокруг дерева.
                float2 fromAxis = world.xz - IN.treeOrigin;
                float2 around = fromAxis / max(length(fromAxis), 0.05);
                float rays = tex2D(_ForestNoise, float2((around.x * 1.3 + around.y * 0.9) * 1.9 + IN.variation, world.y * 0.045)).a;
                float streak = tex2D(_ForestNoise, world.xz * 1.6 + world.y * 0.9).b;
                albedo *= (0.5 + 1.0 * rays) * (0.85 + 0.3 * streak);

                // Нижняя сторона лап темнее верхней: свет приходит сверху.
                albedo *= lerp(0.55, 1.0, saturate(IN.worldNormal.y * 0.8 + 0.55));
                albedo = lerp(albedo, albedo * float3(1.5, 1.6, 1.1) + 0.006, tip * 0.7);
            }

            if (_Grass > 0)
            {
                // Тот же крупный шум, что на земле: трава желтеет там, где подстилка суше.
                float macro = tex2D(_ForestNoise, world.xz * 0.017).r;
                albedo = lerp(albedo, albedo * float3(1.22, 1.03, 0.72), smoothstep(0.35, 0.65, macro));
                albedo *= lerp(0.9, 1.15, tip);
            }

            if (_Moss > 0)
            {
                // Мох растёт у корней стволов и сверху на камнях.
                float blotch = tex2D(_ForestNoise, (world.xz + world.y) * 0.8).g;
                float place = max(saturate(IN.worldNormal.y), saturate(1.0 - IN.height * 6.0));
                albedo = lerp(albedo, float3(0.018, 0.028, 0.01), smoothstep(0.3, 0.8, blotch) * place * 0.75 * _Moss);
            }

            albedo *= lerp(0.45, 1.0, occlusion);
            o.Albedo = albedo;
            o.Occlusion = occlusion;
            o.Smoothness = _Smoothness;

            // Просвет кончиков хвои и травы, когда смотришь против солнца.
            float backlight = pow(saturate(dot(-normalize(IN.viewDir), _ForestSunDirection)), 3.0) * max(tip, _Grass * 0.3);

            o.Emission = albedo * (ForestDynamicLighting(IN.worldPos, IN.worldNormal) + _ForestSunColor * backlight * 0.9);
            o.Alpha = 1;
        }
        ENDCG
    }
}
