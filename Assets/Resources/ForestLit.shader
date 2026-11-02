// Сцена собирается кодом, поэтому на встроенный Standard никто не ссылается и он не попадает
// в сборку. Этот шейдер лежит в Resources и включается всегда.
Shader "Forest/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _Color;
        half _Smoothness;
        fixed4 _EmissionColor;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Smoothness = _Smoothness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
}
