using UnityEngine;

// Вешается на солнце. Всё состояние — одно число TimeOfDay, поэтому в кооперативе
// хост сможет рассылать только его. 0 — рассвет, 0.25 — полдень, 0.5 — закат, 0.75 — полночь.
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    const float SunYaw = -30f;
    const float MaxIntensity = 0.95f;
    const float StartTime = 0.12f;
    const float AmbientSkyFromFog = 2.3f;
    const float AmbientEquatorFromFog = 1.7f;
    const float AmbientGroundFromFog = 0.9f;
    const float DarkSunHeight = 0.1f;

    static readonly Color SunColor = new Color(1f, 0.93f, 0.8f);
    static readonly Color LowSunColor = new Color(1f, 0.62f, 0.36f);
    static readonly Color DayFog = new Color(0.36f, 0.42f, 0.46f);
    static readonly Color DuskFog = new Color(0.34f, 0.24f, 0.2f);
    static readonly Color NightFog = new Color(0.012f, 0.016f, 0.024f);
    static readonly Color MoonAmbient = new Color(0.085f, 0.1f, 0.14f);
    static readonly Color DayZenith = new Color(0.25f, 0.35f, 0.48f);
    static readonly Color DuskZenith = new Color(0.17f, 0.15f, 0.26f);
    static readonly Color NightZenith = new Color(0.006f, 0.009f, 0.02f);

    static readonly int SunDirectionId = Shader.PropertyToID("_ForestSunDirection");
    static readonly int SunColorId = Shader.PropertyToID("_ForestSunColor");
    static readonly int SkyZenithId = Shader.PropertyToID("_ForestSkyZenith");
    static readonly int NightId = Shader.PropertyToID("_ForestNight");

    public float dayLengthSeconds = 600f;

    public float TimeOfDay { get; set; } = StartTime;

    public static bool IsDark { get; private set; }

    Light sun;

    void Awake()
    {
        sun = GetComponent<Light>();
        sun.color = SunColor;
        Apply();
    }

    void Update()
    {
        TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime / dayLengthSeconds, 1f);
        Apply();
    }

    void Apply()
    {
        float angle = TimeOfDay * 360f;
        transform.rotation = Quaternion.Euler(angle, SunYaw, 0f);

        float height = Mathf.Sin(angle * Mathf.Deg2Rad);
        IsDark = height < DarkSunHeight;

        // Под горизонтом свет шёл бы сквозь землю снизу, поэтому он выключается целиком.
        sun.intensity = MaxIntensity * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.25f, height));
        sun.enabled = sun.intensity > 0f;
        sun.color = Color.Lerp(LowSunColor, SunColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.45f, height)));

        Color fog = Color.Lerp(NightFog, DuskFog, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.05f, height)));
        fog = Color.Lerp(fog, DayFog, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.35f, height)));
        RenderSettings.fogColor = fog;
        // Луна не даёт ночи стать сплошной чернотой: вблизи видны силуэты, вдали их съедает туман.
        // Сверху светит небо, снизу — только отражённый от земли свет: так у деревьев появляется объём.
        RenderSettings.ambientSkyColor = fog * AmbientSkyFromFog + MoonAmbient;
        RenderSettings.ambientEquatorColor = fog * AmbientEquatorFromFog + MoonAmbient;
        RenderSettings.ambientGroundColor = fog * AmbientGroundFromFog + MoonAmbient * 0.5f;

        Color zenith = Color.Lerp(NightZenith, DuskZenith, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.05f, height)));
        zenith = Color.Lerp(zenith, DayZenith, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.35f, height)));

        Shader.SetGlobalVector(SunDirectionId, -transform.forward);
        Shader.SetGlobalVector(SunColorId, sun.color.linear * sun.intensity);
        Shader.SetGlobalColor(SkyZenithId, zenith);
        Shader.SetGlobalFloat(NightId, Mathf.InverseLerp(0.05f, -0.2f, height));
    }
}
