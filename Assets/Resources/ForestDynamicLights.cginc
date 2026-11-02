// Фонарик, фары и габариты для объектов из Graphics.DrawMeshInstanced: дополнительные
// источники света на них не действуют, поэтому свет считается прямо в шейдере за один проход.
#ifndef FOREST_DYNAMIC_LIGHTS_INCLUDED
#define FOREST_DYNAMIC_LIGHTS_INCLUDED

#define FOREST_MAX_LIGHTS 8

int _ForestLightCount;
float4 _ForestLightPosition[FOREST_MAX_LIGHTS];  // xyz — позиция, w — дальность
float4 _ForestLightDirection[FOREST_MAX_LIGHTS]; // xyz — направление луча, w — cos внешнего угла
float4 _ForestLightColor[FOREST_MAX_LIGHTS];     // rgb — цвет с яркостью, w — cos внутреннего угла

float3 ForestDynamicLighting(float3 worldPos, float3 worldNormal)
{
    float3 result = 0;
    for (int i = 0; i < _ForestLightCount; i++)
    {
        float3 toLight = _ForestLightPosition[i].xyz - worldPos;
        float range = _ForestLightPosition[i].w;
        float distanceSq = dot(toLight, toLight);
        float normalized = distanceSq / (range * range);
        if (normalized >= 1.0)
            continue;

        float3 direction = toLight / sqrt(max(distanceSq, 0.0001));
        float cone = smoothstep(_ForestLightDirection[i].w, _ForestLightColor[i].w, dot(-direction, _ForestLightDirection[i].xyz));

        // Та же кривая спада, что у встроенных источников, чтобы граница с землёй не бросалась в глаза.
        float fade = saturate(1.0 - normalized);
        float attenuation = fade * fade / (1.0 + 25.0 * normalized);

        // Мягкая обёртка: у травы нормали смотрят вверх, и обычный закон косинуса оставил бы её чёрной.
        float wrap = saturate(dot(worldNormal, direction) * 0.5 + 0.5);
        result += _ForestLightColor[i].rgb * attenuation * cone * wrap;
    }
    return result;
}

#endif
