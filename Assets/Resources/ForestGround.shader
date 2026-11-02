// Земля из чанков: красный канал цвета вершин задаёт глубину грязи. Лежит в Resources,
// чтобы попасть в сборку: сцена создаётся кодом, и ссылок на него в ассетах нет.
Shader "Forest/Ground"
{
    Properties
    {
        _Color ("Ground Color", Color) = (0.22, 0.27, 0.17, 1)
        _MudColor ("Mud Color", Color) = (0.11, 0.08, 0.055, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _MudSmoothness ("Mud Smoothness", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        // addshadow обязателен: тени считаются по карте глубины из этого прохода. Без него
        // земли в карте нет, и на ней проступают тени всего, что скрыто за холмом и под землёй.
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        fixed4 _Color;
        fixed4 _MudColor;
        half _Smoothness;
        half _MudSmoothness;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float mud = saturate(IN.color.r);
            o.Albedo = lerp(_Color.rgb, _MudColor.rgb, mud);
            o.Smoothness = lerp(_Smoothness, _MudSmoothness, mud);
            o.Alpha = 1;
        }
        ENDCG
    }
}
