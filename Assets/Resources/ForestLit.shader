// Сцена собирается кодом, поэтому на встроенный Standard никто не ссылается и он не попадает
// в сборку. Этот шейдер лежит в Resources и включается всегда.
Shader "Forest/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _Grime ("Vehicle Grime", Range(0, 1)) = 0
        _Detail ("World Detail", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float3 vehiclePos;
        };

        sampler2D _ForestNoise;
        float4x4 _ForestVehicleWorldToLocal;
        fixed4 _Color;
        half _Smoothness;
        fixed4 _EmissionColor;
        half _Grime;
        half _Detail;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Грязь привязана к машине: в мировых координатах она плыла бы по кузову на ходу.
            o.vehiclePos = mul(_ForestVehicleWorldToLocal, mul(unity_ObjectToWorld, v.vertex)).xyz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = _Color.rgb;
            float smoothness = _Smoothness;

            if (_Detail > 0)
            {
                float2 uv = float2(IN.worldPos.x + IN.worldPos.z, IN.worldPos.y * 0.4);
                float fibre = tex2D(_ForestNoise, uv * 0.9).a;
                float blotch = tex2D(_ForestNoise, (IN.worldPos.xz + IN.worldPos.y) * 0.21).g;
                albedo *= lerp(1.0, (0.55 + 0.9 * fibre) * (0.7 + 0.6 * blotch), _Detail);
            }

            if (_Grime > 0)
            {
                float3 p = IN.vehiclePos;
                float blotch = tex2D(_ForestNoise, p.xz * 0.5 + p.y * 0.5).g;
                float fine = tex2D(_ForestNoise, p.zy * 2.3 + p.x).b;

                // Снизу кузов забрызган, выше остаются только потёки и выгоревшая краска.
                float mud = saturate((-0.12 - p.y) * 2.2 + (blotch - 0.5) * 1.1 + (fine - 0.5) * 0.7) * _Grime;
                albedo *= lerp(1.0, 0.72 + 0.56 * blotch, _Grime);
                albedo = lerp(albedo, float3(0.016, 0.012, 0.009), mud);
                smoothness *= (1.0 - mud) * lerp(1.0, 0.5 + fine, _Grime);
            }

            o.Albedo = albedo;
            o.Smoothness = smoothness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
}
