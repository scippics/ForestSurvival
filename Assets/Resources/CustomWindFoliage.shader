// Ветер для растительности. Вершины с Y <= 0 в пространстве модели неподвижны, поэтому
// начало координат модели должно стоять у основания. Лежит в Resources, чтобы попасть в сборку.
Shader "Forest/CustomWindFoliage"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.32, 0.14, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.05
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1
        _FlutterStrength ("Flutter Strength", Range(0, 0.2)) = 0.02
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
        };

        half _Smoothness;
        float _WindStrength;
        float _WindSpeed;
        float _FlutterStrength;

        UNITY_INSTANCING_BUFFER_START(FoliageProps)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
        UNITY_INSTANCING_BUFFER_END(FoliageProps)

        void vert(inout appdata_full v)
        {
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
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = UNITY_ACCESS_INSTANCED_PROP(FoliageProps, _Color).rgb;
            o.Smoothness = _Smoothness;
            o.Emission = o.Albedo * ForestDynamicLighting(IN.worldPos, IN.worldNormal);
            o.Alpha = 1;
        }
        ENDCG
    }
}
