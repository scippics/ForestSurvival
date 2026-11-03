// Постобработка кадра: затенение в углах (AO), свечение ярких мест, лучи солнца сквозь деревья
// и итоговая сборка с тонмаппингом. Порядок проходов совпадает с константами ForestPostProcess.
Shader "Hidden/ForestPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    #define AO_SAMPLES 10
    #define RAY_SAMPLES 14
    #define AO_MAX_SCREEN_RADIUS 0.09

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _CameraDepthTexture_TexelSize;
    sampler2D _AoTex, _BloomTex, _RayTex;

    float2 _AoDirection;
    float _AoRadius, _AoStrength;
    float _BloomThreshold, _BloomStrength;
    float3 _SunViewport;
    float _RayStep, _RayStrength;
    float3 _RayColor;
    float _Exposure, _FogDensity, _GrainTime, _VignetteStrength;

    // При сглаживании на D3D исходный кадр бывает перевёрнут относительно глубины.
    float2 DepthUv(float2 uv)
    {
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
            uv.y = 1.0 - uv.y;
    #endif
        return uv;
    }

    // Центр половинного тексела попадает ровно на границу текселов глубины, и какой из них прочитается,
    // решало округление: на отдельных строках и столбцах кадра глубина и координата расходились,
    // и через весь экран шли тонкие линии. Поэтому координата всегда сдвигается на центр тексела.
    float2 SnapToDepth(float2 uv)
    {
        return (floor(uv * _CameraDepthTexture_TexelSize.zw) + 0.5) * _CameraDepthTexture_TexelSize.xy;
    }

    float3 ViewPosition(float2 uv, float eye)
    {
        float2 slope = float2(1.0 / unity_CameraProjection._11, 1.0 / unity_CameraProjection._22);
        return float3((uv * 2.0 - 1.0) * slope * eye, eye);
    }

    float WhiteNoise(float2 p)
    {
        float3 q = frac(p.xyx * 0.1031);
        q += dot(q, q.yzx + 33.33);
        return frac((q.x + q.y) * q.z);
    }

    // Поворот сэмплов AO: шаблон 4 на 4, ровно столько и усредняет размытие. Случайный шум здесь рисовал
    // на всю ширину кадра полосы: строки и столбцы с похожим поворотом затенялись одинаково.
    float AoRotation(float2 pixel)
    {
        int2 cell = int2(fmod(floor(pixel), 4.0));
        return frac((cell.x * 4.0 + cell.y * 9.0 + (cell.x ^ cell.y) * 5.0) / 16.0);
    }

    // Один пиксель с NaN размытие разносит на весь кадр, и экран становится чёрным.
    float3 Clean(float3 color)
    {
        return any(isnan(color)) || any(isinf(color)) ? float3(0.0, 0.0, 0.0) : color;
    }

    float Luma(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    // AO: доля полусферы вокруг точки, закрытая соседней геометрией. RG — затенение и глубина для размытия.
    float4 FragAo(v2f_img i) : SV_Target
    {
        float2 uv = SnapToDepth(DepthUv(i.uv));
        float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
        float eye = LinearEyeDepth(raw);
        float normalized = Linear01Depth(raw);
        if (normalized > 0.95)
            return float4(1.0, 1.0, 0.0, 0.0);

        float3 position = ViewPosition(uv, eye);
        float3 normal = normalize(cross(ddy(position), ddx(position)));
        if (normal.z > 0.0)
            normal = -normal;
        // Вблизи радиус ограничен: иначе сэмплы улетают за край экрана, прилипают к его крайним строкам
        // и рисуют на всю ширину кадра ровные линии.
        float radius = min(_AoRadius * unity_CameraProjection._22 * 0.5 / eye, AO_MAX_SCREEN_RADIUS);

        // Свой поворот на пиксель; шаблон убирает размытие.
        float angle = AoRotation(i.pos.xy) * 6.2831853;
        float occlusion = 0.0;
        for (int s = 0; s < AO_SAMPLES; s++)
        {
            float t = (s + 0.5) / AO_SAMPLES;
            float a = angle + t * 24.0;
            float2 sampleUv = SnapToDepth(uv + float2(cos(a), sin(a)) * radius * t * float2(_MainTex_TexelSize.w / _MainTex_TexelSize.z, 1.0));
            float sampleEye = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampleUv));
            float3 delta = ViewPosition(sampleUv, sampleEye) - position;
            float lengthSq = dot(delta, delta);
            float inRange = saturate(1.0 - lengthSq / (_AoRadius * _AoRadius));
            occlusion += saturate(dot(normal, delta) / sqrt(lengthSq + 0.0001) - 0.15) * inRange;
        }

        float ao = saturate(1.0 - occlusion / AO_SAMPLES * _AoStrength);
        return float4(ao, normalized, 0.0, 0.0);
    }

    // Размытие не переходит через разрыв глубины, иначе вокруг стволов появлялся бы ореол.
    float4 FragAoBlur(v2f_img i) : SV_Target
    {
        // Четыре отсчёта между текселами дают каждому классу шаблона 4 на 4 равный вес, и шаблон исчезает.
        float4 center = tex2D(_MainTex, i.uv);
        // Малый вес центра: если все соседи за разрывом глубины, остаётся он сам, а не чёрная точка.
        float total = center.r * 0.01;
        float weight = 0.01;
        for (int s = 0; s < 4; s++)
        {
            float4 taken = tex2D(_MainTex, i.uv + _AoDirection * (s - 1.5));
            float w = saturate(1.0 - abs(taken.g - center.g) * 300.0);
            total += taken.r * w;
            weight += w;
        }
        return float4(total / weight, center.g, 0.0, 0.0);
    }

    float4 FragBloomPrefilter(v2f_img i) : SV_Target
    {
        float3 color = min(Clean(tex2D(_MainTex, i.uv).rgb), 12.0);
        float luma = Luma(color);
        return float4(color * saturate((luma - _BloomThreshold) / max(luma, 0.0001) * 2.0), 1.0);
    }

    float4 FragBloomDown(v2f_img i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        float3 color = tex2D(_MainTex, i.uv + d * float2(-1, -1)).rgb + tex2D(_MainTex, i.uv + d * float2(1, -1)).rgb
                     + tex2D(_MainTex, i.uv + d * float2(-1, 1)).rgb + tex2D(_MainTex, i.uv + d * float2(1, 1)).rgb;
        return float4(color * 0.25, 1.0);
    }

    float4 FragBloomUp(v2f_img i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        float3 color = tex2D(_MainTex, i.uv + d * float2(-1, 0)).rgb + tex2D(_MainTex, i.uv + d * float2(1, 0)).rgb
                     + tex2D(_MainTex, i.uv + d * float2(0, -1)).rgb + tex2D(_MainTex, i.uv + d * float2(0, 1)).rgb;
        return float4(color * 0.25, 0.7);
    }

    // Маска лучей: только небо, причём рядом с солнцем. Деревья закрывают его и дают тени в лучах.
    float4 FragRayMask(v2f_img i) : SV_Target
    {
        float2 uv = DepthUv(i.uv);
        float normalized = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
        float sky = smoothstep(0.5, 0.95, normalized);
        float2 toSun = (uv - _SunViewport.xy) * float2(_MainTex_TexelSize.z / _MainTex_TexelSize.w, 1.0);
        return float4(sky * pow(saturate(1.0 - length(toSun) * 1.1), 2.0), 0.0, 0.0, 1.0);
    }

    float4 FragRayBlur(v2f_img i) : SV_Target
    {
        float2 stride = (_SunViewport.xy - i.uv) * _RayStep;

        // Свой сдвиг начала у каждого пикселя: ступени шага превращаются в шум, который убирает второй проход.
        float2 uv = i.uv + stride * WhiteNoise(i.pos.xy);
        float total = 0.0;
        float decay = 1.0;
        for (int s = 0; s < RAY_SAMPLES; s++)
        {
            total += tex2D(_MainTex, uv).r * decay;
            decay *= 0.94;
            uv += stride;
        }
        return float4(total / RAY_SAMPLES * 1.6, 0.0, 0.0, 1.0);
    }

    float3 Aces(float3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    float4 FragComposite(v2f_img i) : SV_Target
    {
        float2 uv = DepthUv(i.uv);
        float3 color = Clean(tex2D(_MainTex, i.uv).rgb);

        // Вдали AO гаснет вместе с туманом: туман рассеивает свет и там углов не видно.
        float4 ao = tex2D(_AoTex, uv);
        float fogDepth = ao.g * _ProjectionParams.z * _FogDensity;
        float fogged = exp(-fogDepth * fogDepth);
        color *= isnan(ao.r) ? 1.0 : lerp(1.0, ao.r, fogged);

        color += Clean(tex2D(_BloomTex, i.uv).rgb) * _BloomStrength;
        color += _RayColor * tex2D(_RayTex, uv).r * _RayStrength;

        color = Aces(color * _Exposure);

        // Прохладные тени и чуть меньше насыщенности: лес должен быть холодным, а не кислотным.
        float luma = Luma(color);
        color = lerp(luma.xxx, color, 0.92);
        color *= lerp(float3(0.93, 0.98, 1.08), float3(1.03, 1.0, 0.96), saturate(luma * 2.5));

        float2 centered = i.uv - 0.5;
        color *= saturate(1.0 - dot(centered, centered) * _VignetteStrength);

        // Плёночное зерно в гамма-пространстве заодно убирает полосы в тёмном тумане.
        float3 display = LinearToGammaSpace(color);
        float grain = WhiteNoise(i.pos.xy + _GrainTime) - 0.5;
        display += grain * 0.018;
        color = GammaToLinearSpace(saturate(display));

        return float4(color, 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragAo
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragAoBlur
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBloomPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBloomDown
            ENDCG }
        Pass { Blend One SrcAlpha
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBloomUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragRayMask
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragRayBlur
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            #pragma target 3.0
            ENDCG }
    }
}
