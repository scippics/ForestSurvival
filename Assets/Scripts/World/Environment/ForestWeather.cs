using UnityEngine;

// Погода: дождь приходит и уходит сам. Значение Rain читают земля, стёкла, туман, дворники и звук.
public class ForestWeather : MonoBehaviour
{
    const float FirstRainMinSeconds = 420f;
    const float FirstRainMaxSeconds = 900f;
    const float ClearMinSeconds = 480f;
    const float ClearMaxSeconds = 1200f;
    const float RainMinSeconds = 90f;
    const float RainMaxSeconds = 200f;
    const float RainFadeSeconds = 25f;
    const float ClearFog = 0.042f;
    const float RainFog = 0.058f;
    const float RainSunDim = 0.45f;
    const float DropsPerSecond = 3600f;
    const float SplashesPerSecond = 1100f;
    const float SplashRadius = 32f;
    const float EmitHeight = 8f;

    // Струя у самой камеры не должна становиться толстой белой полосой на весь экран.
    const float MaxDropScreenSize = 0.007f;
    const float CalmWind = 0.65f;
    const float StormWind = 1.9f;
    const float GustSize = 0.7f;
    const float GustSpeed = 0.09f;
    const float WindTurnSpeed = 0.012f;
    const float RainSlant = 2.2f;

    static readonly int RainId = Shader.PropertyToID("_ForestRain");
    static readonly int WindId = Shader.PropertyToID("_ForestWind");
    static readonly Color DropColor = new Color(0.72f, 0.78f, 0.85f, 0.35f);
    static readonly Color SplashColor = new Color(0.75f, 0.8f, 0.85f, 0.3f);

    public static float Rain { get; private set; }

    // Сила и направление ветра: от них качаются деревья, косит дождь и летят листья.
    public static float Wind { get; private set; } = CalmWind;
    public static Vector2 WindDirection { get; private set; } = Vector2.right;

    public bool IsRaining => raining;

    // Скоро пойдёт дождь: тучи затягивают небо заранее. Клиент этого не знает и ждёт первых капель.
    public bool RainComing(float leadSeconds) => !followsHost && !raining && nextChange - Time.time < leadSeconds;

    ParticleSystem drops;
    ParticleSystem splashes;
    Light sun;
    bool raining;
    bool followsHost;
    float nextChange;

    public void ForceRain(bool on, bool instantly)
    {
        raining = on;
        nextChange = Time.time + RainMaxSeconds;
        if (instantly)
            Rain = on ? 1f : 0f;
    }

    // У клиента расписание своё не считается: дождём управляет хост.
    public void SetFromNetwork(bool on)
    {
        followsHost = true;
        raining = on;
    }

    void Start()
    {
        sun = FindAnyObjectByType<DayNightCycle>().GetComponent<Light>();
        nextChange = Time.time + Random.Range(FirstRainMinSeconds, FirstRainMaxSeconds);
        CreateDrops();
        CreateSplashes();
    }

    // Брызги у самой земли: вблизи именно они показывают, что идёт дождь.
    void CreateSplashes()
    {
        var splashObject = new GameObject("RainSplashes");
        splashObject.transform.SetParent(transform, false);
        splashObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        splashes = splashObject.AddComponent<ParticleSystem>();
        splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = splashes.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 700;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.075f);
        main.startColor = SplashColor;
        main.gravityModifier = 0.5f;

        ParticleSystem.ShapeModule shape = splashes.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(SplashRadius, SplashRadius, 0.1f);

        ParticleSystem.EmissionModule emission = splashes.emission;
        emission.rateOverTime = 0f;

        var splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
        splashRenderer.sharedMaterial = ForestAtmosphere.CreateParticleMaterial(0.2f);
        splashes.Play();
    }

    void CreateDrops()
    {
        var dropObject = new GameObject("RainDrops");
        dropObject.transform.SetParent(transform, false);
        dropObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        drops = dropObject.AddComponent<ParticleSystem>();
        drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = drops.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4200;
        main.playOnAwake = false;
        main.startLifetime = 0.85f;
        main.startSpeed = 15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.03f);
        main.startColor = DropColor;

        ParticleSystem.ShapeModule shape = drops.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(26f, 26f, 0.5f);

        ParticleSystem.EmissionModule emission = drops.emission;
        emission.rateOverTime = 0f;

        var dropRenderer = dropObject.GetComponent<ParticleSystemRenderer>();
        dropRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        dropRenderer.lengthScale = 2f;
        dropRenderer.velocityScale = 0.035f;
        dropRenderer.sharedMaterial = ForestAtmosphere.CreateParticleMaterial(0.2f);
        dropRenderer.maxParticleSize = MaxDropScreenSize;

        // Капли гаснут внутри кабины: крыша настоящая, и в машине дождя быть не должно.
        var car = FindAnyObjectByType<VehicleController>();
        if (car != null)
        {
            ParticleSystem.TriggerModule trigger = drops.trigger;
            trigger.enabled = true;
            trigger.inside = ParticleSystemOverlapAction.Kill;
            trigger.SetCollider(0, car.transform.Find("CabinCollider").GetComponent<Collider>());
        }

        drops.Play();
    }

    void UpdateWind(float now)
    {
        float gust = Mathf.PerlinNoise(now * GustSpeed, 3.7f);
        Wind = Mathf.Lerp(CalmWind, StormWind, Rain) + gust * gust * GustSize * 2f;

        float angle = (Mathf.PerlinNoise(now * WindTurnSpeed, 9.1f) - 0.5f) * Mathf.PI * 3f;
        WindDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Shader.SetGlobalVector(WindId, new Vector4(WindDirection.x, WindDirection.y, Wind, 0f));

        // Дождь косит по ветру.
        ParticleSystem.VelocityOverLifetimeModule slant = drops.velocityOverLifetime;
        slant.enabled = true;
        slant.space = ParticleSystemSimulationSpace.World;
        slant.x = WindDirection.x * Wind * RainSlant;
        slant.y = 0f;
        slant.z = WindDirection.y * Wind * RainSlant;
    }

    void LateUpdate()
    {
        float now = Time.time;
        if (!followsHost && now >= nextChange)
        {
            raining = !raining;
            nextChange = now + (raining ? Random.Range(RainMinSeconds, RainMaxSeconds) : Random.Range(ClearMinSeconds, ClearMaxSeconds));
        }

        Rain = Mathf.MoveTowards(Rain, raining ? 1f : 0f, Time.deltaTime / RainFadeSeconds);
        Shader.SetGlobalFloat(RainId, Rain);
        UpdateWind(now);
        RenderSettings.fogDensity = Mathf.Lerp(ClearFog, RainFog, Rain);
        sun.intensity *= 1f - RainSunDim * Rain;

        Camera view = Camera.main;
        if (view != null)
        {
            Vector3 eye = view.transform.position;
            drops.transform.position = eye + Vector3.up * EmitHeight;
            splashes.transform.position = new Vector3(eye.x, OptimizedForestGenerator.TerrainHeight(eye.x, eye.z) + 0.04f, eye.z);
        }

        ParticleSystem.EmissionModule emission = drops.emission;
        emission.rateOverTime = DropsPerSecond * Rain;

        ParticleSystem.EmissionModule splashEmission = splashes.emission;
        splashEmission.rateOverTime = SplashesPerSecond * Rain;

    }
}
