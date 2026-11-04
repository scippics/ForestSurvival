// Сколько воды на лобовом стекле: дождь её копит, щётки стирают ровно там, где прошли.
// Координаты те же, что у стекла: 1.68 на 0.66 м, нижняя кромка на высоте 0.2 м от центра кузова.
Shader "Hidden/Forest/WiperMask"
{
    Properties
    {
        _MainTex ("Water", 2D) = "black" {}
    }

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Pivots;
            float4 _Sweep;
            float _Change;
            float _ForestGlassBottom;
            float _SlopeScale;

            // Наклонное стекло сжато по высоте: на нём щётка проходит больше, чем видно спереди.
            float Cleared(float2 glass, float2 pivot)
            {
                float2 arm = glass - pivot;
                arm.y *= _SlopeScale;
                float along = length(arm);
                float angle = atan2(-arm.x, arm.y);
                float reach = smoothstep(_Sweep.z - 0.015, _Sweep.z, along) * smoothstep(_Sweep.w + 0.015, _Sweep.w, along);
                float margin = 0.018 / max(along, 0.05);
                return reach * step(_Sweep.x - margin, angle) * step(angle, _Sweep.y + margin);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float2 glass = float2((i.uv.x - 0.5) * 1.68, i.uv.y * 0.66 + _ForestGlassBottom);
                float water = saturate(tex2D(_MainTex, i.uv).r + _Change);
                float cleared = max(Cleared(glass, _Pivots.xy), Cleared(glass, _Pivots.zw));
                return water * (1.0 - cleared * 0.97);
            }
            ENDCG
        }
    }
}
