using UnityEngine;

// Тучи и гроза. Облака плывут по ветру и затягивают небо ещё до первых капель.
// В сильный дождь бьют молнии: вспышка освещает тучи и лес, близкий удар виден на небе,
// гром доходит с задержкой по расстоянию. Удары выбираются по общим часам суток,
// которые хост рассылает клиентам, поэтому у всех игроков вспышки совпадают.
[RequireComponent(typeof(ForestWeather))]
public class ForestStorm : MonoBehaviour
{
    const float CloudFadeSeconds = 40f;
    const float CloudLeadSeconds = 75f;
    const float RainComingCover = 0.85f;
    const float CloudDrift = 0.0025f;
    const float OvercastSunDim = 0.35f;
    const float OvercastShadowSoften = 0.55f;
    const float OvercastDarken = 0.4f;

    const float StormMinRain = 0.6f;
    const float SlotSeconds = 1.5f;
    const float RareStrikeChance = 0.03f;
    const float FrequentStrikeChance = 0.16f;
    const float StormMoodSpeed = 0.004f;

    // Дальше этой доли удар виден только как свечение в тучах, без самого канала.
    const float VisibleBoltDistance = 0.75f;
    const float FlashDecay = 14f;
    const float BoltSeconds = 0.45f;
    const float LightStrength = 1.4f;
    const float SoundSecondsPerDistance = 7f;
    const float MinThunderDelay = 0.35f;

    static readonly Color FlashColor = new Color(0.78f, 0.84f, 1f);
    static readonly float[] StrobeTimes = { 0f, 0.12f, 0.27f };
    static readonly float[] StrobePower = { 1f, 0.75f, 0.5f };

    static readonly int CoverId = Shader.PropertyToID("_ForestCloudCover");
    static readonly int OffsetId = Shader.PropertyToID("_ForestCloudOffset");
    static readonly int FlashId = Shader.PropertyToID("_ForestFlash");
    static readonly int BoltId = Shader.PropertyToID("_ForestBolt");
    static readonly int BoltShapeId = Shader.PropertyToID("_ForestBoltShape");

    public static float CloudCover { get; private set; }
    public static float Flash { get; private set; }

    ForestWeather weather;
    DayNightCycle cycle;
    Light sun;
    Light flashLight;
    Vector2 cloudOffset;
    long lastSlot = long.MinValue;

    float strikeTime = -100f;
    float strikeDistance = 1f;
    Vector3 strikeDirection = Vector3.forward;
    Vector4 boltShape;
    float boltTop;
    bool boltVisible;
    float thunderAt = -1f;
    float thunderVolume;

    // Удар прямо сейчас: для отладки и проверок. distance от 0 (рядом) до 1 (у горизонта).
    public void Strike(int seed, float distance = -1f)
    {
        var random = new System.Random(seed);
        strikeTime = Time.time;
        strikeDistance = distance >= 0f ? Mathf.Clamp01(distance) : Mathf.Lerp(0.1f, 1f, (float)random.NextDouble());
        float azimuth = (float)random.NextDouble() * Mathf.PI * 2f;
        strikeDirection = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));

        float tilt = Mathf.Lerp(-0.25f, 0.25f, (float)random.NextDouble());
        float branchSide = random.NextDouble() < 0.5 ? -1f : 1f;
        float branchSlope = branchSide * Mathf.Lerp(0.3f, 0.8f, (float)random.NextDouble());
        // Близкий удар высокий и толстый, иначе его целиком закрывают кроны.
        boltShape = new Vector4((float)random.NextDouble() * 100f, tilt, Mathf.Lerp(0.006f, 0.002f, strikeDistance), branchSlope);
        boltTop = Mathf.Lerp(0.75f, 0.28f, strikeDistance);
        boltVisible = strikeDistance < VisibleBoltDistance;

        thunderAt = strikeTime + MinThunderDelay + strikeDistance * SoundSecondsPerDistance;
        thunderVolume = Mathf.Lerp(1f, 0.3f, strikeDistance);
    }

    public void SetDirectionForTest(Vector3 direction)
    {
        direction.y = 0f;
        strikeDirection = direction.normalized;
    }

    void Start()
    {
        weather = GetComponent<ForestWeather>();
        cycle = FindAnyObjectByType<DayNightCycle>();
        sun = cycle.GetComponent<Light>();

        var lightObject = new GameObject("LightningFlash");
        lightObject.transform.SetParent(transform, false);
        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Directional;
        flashLight.color = FlashColor;
        flashLight.shadows = LightShadows.None;
        flashLight.intensity = 0f;
        flashLight.enabled = false;
    }

    void LateUpdate()
    {
        float now = Time.time;
        UpdateClouds(now);
        ScheduleStrikes();
        UpdateFlash(now);

        if (thunderAt > 0f && now >= thunderAt)
        {
            thunderAt = -1f;
            ForestAudio.Play(ForestSound.Thunder, thunderVolume);
        }
    }

    void UpdateClouds(float now)
    {
        // В ясную погоду облаков то больше, то меньше в течение суток, но небо не затягивает целиком.
        float clearCover = 0.25f + 0.2f * Mathf.Sin(cycle.TimeOfDay * Mathf.PI * 6f + 1.3f);
        float target = weather.IsRaining ? 1f : weather.RainComing(CloudLeadSeconds) ? RainComingCover : clearCover;
        // Тучи не бывают реже, чем идёт дождь.
        CloudCover = Mathf.Max(Mathf.MoveTowards(CloudCover, target, Time.deltaTime / CloudFadeSeconds), ForestWeather.Rain);

        cloudOffset += ForestWeather.WindDirection * (ForestWeather.Wind * CloudDrift * Time.deltaTime);
        cloudOffset.x = Mathf.Repeat(cloudOffset.x, 1f);
        cloudOffset.y = Mathf.Repeat(cloudOffset.y, 1f);

        Shader.SetGlobalFloat(CoverId, CloudCover);
        Shader.SetGlobalVector(OffsetId, cloudOffset);

        // Сплошная пелена гасит солнце и размывает тени.
        float overcast = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, CloudCover));
        sun.intensity *= 1f - OvercastSunDim * overcast;
        sun.shadowStrength *= 1f - OvercastShadowSoften * overcast;

        // Под тучами темнеет и дымка, иначе дальний лес светлее неба и выглядит белёсым.
        float dim = 1f - OvercastDarken * overcast;
        RenderSettings.fogColor *= dim;
        RenderSettings.ambientSkyColor *= dim;
        RenderSettings.ambientEquatorColor *= dim;
        RenderSettings.ambientGroundColor *= dim;
    }

    void ScheduleStrikes()
    {
        double clock = (double)cycle.TimeOfDay * cycle.dayLengthSeconds;
        long slot = (long)System.Math.Floor(clock / SlotSeconds);
        if (slot == lastSlot)
            return;
        bool first = lastSlot == long.MinValue;
        lastSlot = slot;
        if (first || ForestWeather.Rain < StormMinRain)
            return;

        // Гроза то стихает, то разыгрывается: частота ударов плавно гуляет.
        float mood = Mathf.PerlinNoise(slot * StormMoodSpeed, 4.2f);
        float chance = Mathf.Lerp(RareStrikeChance, FrequentStrikeChance, mood) * Mathf.InverseLerp(StormMinRain, 1f, ForestWeather.Rain);
        if (Hash(slot) < chance)
            Strike((int)(slot * 7919 % int.MaxValue));
    }

    static float Hash(long slot)
    {
        double value = System.Math.Sin(slot * 12.9898 + 78.233) * 43758.5453;
        return (float)(value - System.Math.Floor(value));
    }

    void UpdateFlash(float now)
    {
        float since = now - strikeTime;
        float strobe = 0f;
        for (int i = 0; i < StrobeTimes.Length; i++)
        {
            if (since >= StrobeTimes[i])
                strobe += StrobePower[i] * Mathf.Exp(-(since - StrobeTimes[i]) * FlashDecay);
        }

        float power = Mathf.Lerp(1.3f, 0.45f, strikeDistance);
        Flash = strobe * power;
        float bolt = boltVisible && since < BoltSeconds ? Mathf.Clamp01(strobe * 1.4f) : 0f;

        Shader.SetGlobalFloat(FlashId, Flash);
        Shader.SetGlobalVector(BoltId, new Vector4(strikeDirection.x, boltTop, strikeDirection.z, bolt));
        Shader.SetGlobalVector(BoltShapeId, boltShape);

        // Свет идёт от места удара сверху вниз и на миг подсвечивает весь лес.
        flashLight.intensity = Flash * LightStrength;
        flashLight.enabled = flashLight.intensity > 0.01f;
        if (flashLight.enabled)
        {
            flashLight.transform.rotation = Quaternion.LookRotation((-strikeDirection + Vector3.down * 1.2f).normalized);
            Color glow = FlashColor * Flash;
            RenderSettings.ambientSkyColor += glow * 0.5f;
            RenderSettings.ambientEquatorColor += glow * 0.35f;
            RenderSettings.ambientGroundColor += glow * 0.15f;
        }
    }
}
