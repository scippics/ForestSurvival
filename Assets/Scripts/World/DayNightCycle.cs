using UnityEngine;

// Вешается на солнце. Всё состояние — одно число TimeOfDay, поэтому в кооперативе
// хост сможет рассылать только его. 0 — рассвет, 0.25 — полдень, 0.5 — закат, 0.75 — полночь.
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    const float SunYaw = -30f;
    const float MaxIntensity = 0.55f;
    const float StartTime = 0.12f;
    const float AmbientFromFog = 1.3f;
    const float DarkSunHeight = 0.1f;

    static readonly Color SunColor = new Color(0.72f, 0.8f, 0.95f);
    static readonly Color DayFog = new Color(0.23f, 0.26f, 0.29f);
    static readonly Color DuskFog = new Color(0.2f, 0.16f, 0.15f);
    static readonly Color NightFog = new Color(0.012f, 0.016f, 0.024f);
    static readonly Color MoonAmbient = new Color(0.06f, 0.07f, 0.1f);

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

        Color fog = Color.Lerp(NightFog, DuskFog, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.05f, height)));
        fog = Color.Lerp(fog, DayFog, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.35f, height)));
        RenderSettings.fogColor = fog;
        // Луна не даёт ночи стать сплошной чернотой: вблизи видны силуэты, вдали их съедает туман.
        RenderSettings.ambientLight = fog * AmbientFromFog + MoonAmbient;

        // Неба в сцене нет: фон камеры обязан совпадать с туманом, иначе дальние объекты
        // растворяются в одном цвете на фоне другого.
        Camera view = Camera.main;
        if (view != null)
            view.backgroundColor = fog;
    }
}
