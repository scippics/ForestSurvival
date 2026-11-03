using System;
using UnityEngine;

public enum ForestSound
{
    Click,
    Door,
    Ratchet,
    Paper,
    Step,
    Land,
    Bang,
    Start,
    Stall,
    Cough,
    Bird,
    Owl,
    Crack,
    Moan,
    Thunder
}

// Весь звук синтезируется при запуске: файлов в проекте нет. Фон, мотор и случайные звуки леса
// живут здесь же, а остальной код только просит сыграть нужный звук.
public class ForestAudio : MonoBehaviour
{
    const int Rate = 22050;
    const int PoolSize = 10;
    const float StepDistance = 1.75f;
    const float LandMinAirSeconds = 0.35f;
    const float CabinCutoff = 1500f;
    const float OutsideCutoff = 6000f;
    const float CabinWind = 0.3f;

    static ForestAudio instance;
    static float ambience = 1f;
    static float ambienceFrom = 1f;
    static float ambienceTarget = 1f;
    static float ambienceFadeLeft;
    static float ambienceFadeTotal;

    AudioClip[] clips;
    AudioSource[] pool;
    int poolIndex;
    AudioSource ui, wind, crickets, engine, mud, pour, radio, rain;
    AudioLowPassFilter rainFilter;
    AudioLowPassFilter engineFilter;

    PlayerSeat seat;
    PlayerMovement movement;
    VehicleController car;
    float stepRemaining = StepDistance;
    float airTime;
    float nextBird, nextOwl, nextCrack, nextMoan;
    bool wasRunning;
    System.Random random = new System.Random(4242);

    public static int ClipCount => instance != null ? instance.clips.Length : 0;

    // Громкость всего леса: пока идут чёрные титры, тишина.
    public static void SetAmbience(float value, float seconds = 0f)
    {
        ambienceFrom = ambience;
        ambienceTarget = value;
        ambienceFadeLeft = ambienceFadeTotal = seconds;
        if (seconds <= 0f)
            ambience = value;
    }

    public static void Play(ForestSound sound, float volume = 1f)
    {
        if (instance == null)
            return;
        instance.ui.pitch = 1f;
        instance.ui.PlayOneShot(instance.clips[(int)sound], volume);
    }

    public static void PlayAt(ForestSound sound, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (instance == null)
            return;
        AudioSource source = instance.pool[instance.poolIndex];
        instance.poolIndex = (instance.poolIndex + 1) % PoolSize;
        source.transform.position = position;
        source.clip = instance.clips[(int)sound];
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
    }

    static bool pouringNow;
    static bool flowingNow;

    // Бензин течёт сам, пока идёт слив, даже когда игрок ничего не льёт.
    public static void SetFlowing(bool on)
    {
        flowingNow = on;
        SetPouring(pouringNow);
    }

    public static void SetPouring(bool on)
    {
        pouringNow = on;
        if (instance != null)
            instance.pour.volume = on ? 0.5f : flowingNow ? 0.22f : 0f;
    }

    void Awake()
    {
        instance = this;
        BuildClips();

        ui = gameObject.AddComponent<AudioSource>();
        ui.spatialBlend = 0f;

        wind = Loop(BuildWind(), 0.3f);
        crickets = Loop(BuildCrickets(), 0f);
        pour = Loop(BuildPour(), 0f);
        rain = Loop(BuildRain(), 0f);
        rainFilter = rain.gameObject.AddComponent<AudioLowPassFilter>();

        var engineObject = new GameObject("EngineSound");
        engineObject.transform.SetParent(transform, false);
        engine = engineObject.AddComponent<AudioSource>();
        engine.clip = BuildEngine();
        engine.loop = true;
        engine.volume = 0f;
        engine.rolloffMode = AudioRolloffMode.Linear;
        engine.maxDistance = 45f;
        engine.Play();
        engineFilter = engineObject.AddComponent<AudioLowPassFilter>();

        mud = Loop(BuildMud(), 0f);

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var source = new GameObject("Sound3D").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 60f;
            source.playOnAwake = false;
            pool[i] = source;
        }

        var radioObject = new GameObject("RadioSound");
        radioObject.transform.position = Kordon.RadioPosition;
        radio = radioObject.AddComponent<AudioSource>();
        radio.clip = BuildRadio();
        radio.loop = true;
        radio.spatialBlend = 1f;
        radio.rolloffMode = AudioRolloffMode.Linear;
        radio.minDistance = 0.5f;
        radio.maxDistance = 9f;
        radio.volume = 0.3f;
        radio.Play();

        nextBird = 6f;
        nextOwl = 30f;
        nextCrack = 40f;
        nextMoan = 150f;
    }

    AudioSource Loop(AudioClip clip, float volume)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.Play();
        return source;
    }

    void Update()
    {
        if (seat == null)
        {
            seat = FindAnyObjectByType<PlayerSeat>();
            car = FindAnyObjectByType<VehicleController>();
            if (seat == null || car == null)
                return;
            movement = seat.GetComponent<PlayerMovement>();
        }

        if (ambienceFadeLeft > 0f)
        {
            ambienceFadeLeft -= Time.unscaledDeltaTime;
            ambience = Mathf.Lerp(ambienceTarget, ambienceFrom, Mathf.Clamp01(ambienceFadeLeft / ambienceFadeTotal));
        }

        // В паузе и в меню лес тоже слышен, поэтому только чёрные титры глушат всё.
        float cabin = seat.IsSeated ? CabinWind : 1f;
        wind.volume = (0.13f + 0.05f * Mathf.Sin(Time.time * 0.13f)) * (0.6f + 0.4f * ForestWeather.Wind) * cabin * ambience;
        crickets.volume = (DayNightCycle.IsDark ? 0.14f : 0f) * cabin * ambience;
        radio.volume = 0.3f * ambience;

        // По крыше кабины дождь глуше, но ближе: вся барабанная дробь над головой.
        rain.volume = ForestWeather.Rain * (seat.IsSeated ? 0.24f : 0.2f) * ambience;
        rainFilter.cutoffFrequency = seat.IsSeated ? 1500f : 3400f;

        UpdateEngine();
        UpdateSteps();
        UpdateForest();
    }

    void UpdateEngine()
    {
        bool running = car.EngineRunning;
        if (running && !wasRunning)
            PlayAt(ForestSound.Start, car.transform.position, 0.9f);
        else if (!running && wasRunning && car.Driver != null)
            PlayAt(ForestSound.Stall, car.transform.position, 0.9f);
        wasRunning = running;

        float speed = Mathf.Abs(car.ForwardSpeed);
        engine.transform.position = car.transform.position;
        engine.spatialBlend = seat.IsSeated ? 0f : 1f;
        engineFilter.cutoffFrequency = seat.IsSeated ? CabinCutoff : OutsideCutoff;

        float load = Mathf.Abs(car.Throttle);
        engine.pitch = 0.75f + speed * 0.055f + load * 0.35f;
        float target = running ? 0.32f + load * 0.3f : 0f;
        engine.volume = Mathf.MoveTowards(engine.volume, target * ambience, Time.deltaTime * 1.6f);

        float squelch = car.Driver != null ? Mathf.Clamp01(car.CurrentMudDepth * speed * 0.5f) : 0f;
        mud.volume = Mathf.MoveTowards(mud.volume, squelch * 0.5f, Time.deltaTime);
    }

    void UpdateSteps()
    {
        if (seat.IsSeated || movement == null)
            return;

        if (!movement.IsGrounded)
        {
            airTime += Time.deltaTime;
            return;
        }

        if (airTime > LandMinAirSeconds)
            Play(ForestSound.Land, 0.7f);
        airTime = 0f;

        stepRemaining -= movement.GroundSpeed * Time.deltaTime;
        if (stepRemaining <= 0f)
        {
            stepRemaining = StepDistance;
            ui.pitch = 0.85f + (float)random.NextDouble() * 0.3f;
            ui.PlayOneShot(clips[(int)ForestSound.Step], 0.42f);
        }
    }

    // Случайные звуки леса приходят с разных сторон и далеко от игрока.
    void UpdateForest()
    {
        if (GameFlow.State != GameState.Playing)
            return;

        float now = Time.time;
        if (!DayNightCycle.IsDark && now > nextBird)
        {
            nextBird = now + Range(9f, 26f);
            PlayFar(ForestSound.Bird, 0.32f, Range(0.85f, 1.2f));
        }
        if (DayNightCycle.IsDark && now > nextOwl)
        {
            nextOwl = now + Range(28f, 70f);
            PlayFar(ForestSound.Owl, 0.5f, Range(0.92f, 1.06f));
        }
        if (now > nextCrack)
        {
            nextCrack = now + Range(45f, 120f);
            PlayFar(ForestSound.Crack, 0.55f, Range(0.8f, 1.2f));
        }
        if (DayNightCycle.IsDark && now > nextMoan)
        {
            nextMoan = now + Range(140f, 320f);
            PlayFar(ForestSound.Moan, 0.4f, Range(0.9f, 1.05f));
        }
    }

    void PlayFar(ForestSound sound, float volume, float pitch)
    {
        float angle = Range(0f, Mathf.PI * 2f);
        float distance = Range(18f, 40f);
        Vector3 position = seat.transform.position + new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)) * distance;
        PlayAt(sound, position, volume, pitch);
    }

    float Range(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    // ---- Синтез ----

    void BuildClips()
    {
        var values = Enum.GetValues(typeof(ForestSound));
        clips = new AudioClip[values.Length];
        clips[(int)ForestSound.Click] = BuildClick();
        clips[(int)ForestSound.Door] = BuildDoor();
        clips[(int)ForestSound.Ratchet] = BuildRatchet();
        clips[(int)ForestSound.Paper] = BuildPaper();
        clips[(int)ForestSound.Step] = BuildStep();
        clips[(int)ForestSound.Land] = BuildLand();
        clips[(int)ForestSound.Bang] = BuildBang();
        clips[(int)ForestSound.Start] = BuildStart();
        clips[(int)ForestSound.Stall] = BuildStall();
        clips[(int)ForestSound.Cough] = BuildCough();
        clips[(int)ForestSound.Bird] = BuildBird();
        clips[(int)ForestSound.Owl] = BuildOwl();
        clips[(int)ForestSound.Crack] = BuildCrack();
        clips[(int)ForestSound.Moan] = BuildMoan();
        clips[(int)ForestSound.Thunder] = BuildThunder();
    }

    static AudioClip Make(string name, float[] samples)
    {
        Normalize(samples);
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static void Normalize(float[] samples, float peak = 0.9f)
    {
        float max = 0f;
        foreach (float sample in samples)
            max = Mathf.Max(max, Mathf.Abs(sample));
        if (max <= 0f)
            return;
        float gain = peak / max;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= gain;
    }

    // Хвост наплывает на начало, поэтому повтор не слышен щелчком.
    static AudioClip MakeLoop(string name, float[] samples, float crossfadeSeconds)
    {
        int fade = (int)(crossfadeSeconds * Rate);
        int length = samples.Length - fade;
        var loop = new float[length];
        Array.Copy(samples, loop, length);
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            loop[i] = samples[i] * t + samples[length + i] * (1f - t);
        }
        return Make(name, loop);
    }

    static float[] Buffer(float seconds)
    {
        return new float[(int)(seconds * Rate)];
    }

    static float Noise(System.Random random)
    {
        return (float)random.NextDouble() * 2f - 1f;
    }

    static float Pole(float cutoff)
    {
        return 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
    }

    static float Tau(float t)
    {
        return Mathf.PI * 2f * t;
    }

    static AudioClip BuildWind()
    {
        var random = new System.Random(1);
        float[] data = Buffer(14f);
        float low = 0f, band = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float gust = 0.55f + 0.3f * Mathf.Sin(Tau(t * 0.09f)) + 0.15f * Mathf.Sin(Tau(t * 0.23f) + 1.3f);
            low += (Noise(random) - low) * Pole(70f + gust * 170f);
            band += (low - band) * Pole(70f);
            data[i] = (low - band * 0.7f) * gust;
        }
        return MakeLoop("Wind", data, 2f);
    }

    static AudioClip BuildCrickets()
    {
        float[] data = Buffer(6f);
        float[] carriers = { 4100f, 4650f, 3800f };
        float[] periods = { 1.1f, 1.37f, 0.93f };
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float sum = 0f;
            for (int c = 0; c < carriers.Length; c++)
            {
                float local = Mathf.Repeat(t + c * 0.31f, periods[c]);
                float group = local < 0.42f ? Mathf.Sin(Mathf.PI * local / 0.42f) : 0f;
                float pulse = Mathf.Max(0f, Mathf.Sin(Tau(t * 19f + c * 0.3f)));
                sum += Mathf.Sin(Tau(t * carriers[c])) * group * pulse * (1f - c * 0.25f);
            }
            data[i] = sum;
        }
        return MakeLoop("Crickets", data, 0.2f);
    }

    // Частоты целые, а длина ровно секунда: цикл сходится сам, без наплыва.
    static AudioClip BuildEngine()
    {
        var random = new System.Random(2);
        float[] data = new float[Rate];
        const int fundamental = 42;
        const int harmonics = 14;
        var phases = new float[harmonics];
        for (int h = 0; h < harmonics; h++)
            phases[h] = (float)random.NextDouble() * Mathf.PI * 2f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float rough = 0.82f + 0.18f * Mathf.Sin(Tau(t * 7f)) * Mathf.Sin(Tau(t * 3f) + 0.7f);
            float sum = Mathf.Sin(Tau(t * fundamental * 0.5f)) * 0.5f;
            for (int h = 1; h <= harmonics; h++)
                sum += Mathf.Sin(Tau(t * fundamental * h) + phases[h - 1]) / Mathf.Pow(h, 1.1f);
            data[i] = sum * rough;
        }
        return Make("Engine", data);
    }

    static AudioClip BuildMud()
    {
        var random = new System.Random(3);
        float[] data = Buffer(5f);
        float low = 0f;
        float blob = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low += (Noise(random) - low) * Pole(260f);
            if (random.NextDouble() < 0.0006)
                blob = 1f;
            blob *= 0.9994f;
            data[i] = low * (0.45f + 0.25f * Mathf.Sin(Tau(t * 1.7f))) + Mathf.Sin(Tau(t * (90f + blob * 70f))) * blob * 0.35f;
        }
        return MakeLoop("Mud", data, 0.5f);
    }

    static AudioClip BuildPour()
    {
        var random = new System.Random(4);
        float[] data = Buffer(3f);
        float low = 0f, high = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(random);
            low += (noise - low) * Pole(2600f);
            high += (low - high) * Pole(500f);
            float glug = 0.7f + 0.3f * Mathf.Sin(Tau(t * 3.3f) + Mathf.Sin(Tau(t * 0.7f)) * 2f);
            data[i] = (low - high) * glug;
        }
        return MakeLoop("Pour", data, 0.3f);
    }

    static AudioClip BuildRadio()
    {
        var random = new System.Random(5);
        float[] data = Buffer(9f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low += (Noise(random) - low) * Pole(3200f);
            float crackle = random.NextDouble() < 0.0009 ? Noise(random) * 3f : 0f;

            // Едва слышная морзянка под шипением: кто-то на этой частоте всё ещё работает.
            float slot = Mathf.Repeat(t, 9f);
            bool key = slot > 3f && slot < 5.4f && (Mathf.FloorToInt(slot * 5f) % 3 != 2);
            float tone = key ? Mathf.Sin(Tau(t * 720f)) * 0.08f : 0f;
            data[i] = low * 0.4f + crackle + tone;
        }
        return MakeLoop("RadioStatic", data, 0.4f);
    }

    static AudioClip BuildRain()
    {
        var random = new System.Random(17);
        float[] data = Buffer(6f);
        float low = 0f, high = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float noise = Noise(random);
            low += (noise - low) * Pole(2200f);
            high += (low - high) * Pole(300f);
            float drop = random.NextDouble() < 0.0012 ? Noise(random) * 1.2f : 0f;
            data[i] = (low - high) + drop;
        }
        return MakeLoop("Rain", data, 0.6f);
    }

    static AudioClip BuildThunder()
    {
        var random = new System.Random(18);
        const float length = 5f;
        float[] data = Buffer(length);
        float low = 0f, deep = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(random);
            low += (noise - low) * Pole(160f);
            deep += (low - deep) * Pole(45f);
            float roll = 0.55f + 0.45f * Mathf.PerlinNoise(t * 3.1f, 9.4f);
            data[i] = (low * 0.7f + deep * 2.2f) * roll * Mathf.Min(1f, t * 3f) * Mathf.Exp(-t * 0.85f);
        }
        return Make("Thunder", data);
    }

    static AudioClip BuildClick()
    {
        var random = new System.Random(6);
        float[] data = Buffer(0.05f);
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            data[i] = (Noise(random) * 0.6f + Mathf.Sin(Tau(t * 1900f))) * Mathf.Exp(-t * 170f);
        }
        return Make("Click", data);
    }

    static AudioClip BuildDoor()
    {
        var random = new System.Random(7);
        float[] data = Buffer(0.45f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low += (Noise(random) - low) * Pole(700f);
            float thud = (Mathf.Sin(Tau(t * 95f)) + low) * Mathf.Exp(-t * 14f);
            float latch = t > 0.03f ? (Noise(random) + Mathf.Sin(Tau(t * 2300f))) * 0.3f * Mathf.Exp(-(t - 0.03f) * 90f) : 0f;
            data[i] = thud + latch;
        }
        return Make("Door", data);
    }

    static AudioClip BuildRatchet()
    {
        var random = new System.Random(8);
        float[] data = Buffer(0.4f);
        for (int tooth = 0; tooth < 8; tooth++)
        {
            int start = (int)(tooth * 0.042f * Rate);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                data[i] += (Noise(random) * 0.5f + Mathf.Sin(Tau(t * 2800f))) * Mathf.Exp(-t * 320f);
            }
        }
        return Make("Ratchet", data);
    }

    static AudioClip BuildPaper()
    {
        var random = new System.Random(9);
        float[] data = Buffer(0.55f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(random);
            low += (noise - low) * Pole(1200f);
            float rustle = Mathf.Pow(Mathf.Abs(Mathf.Sin(Tau(t * 7f) + Mathf.Sin(Tau(t * 3f)))), 3f);
            data[i] = (noise - low) * rustle * Mathf.Sin(Mathf.PI * t / 0.55f);
        }
        return Make("Paper", data);
    }

    static AudioClip BuildStep()
    {
        var random = new System.Random(10);
        float[] data = Buffer(0.2f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(random);
            low += (noise - low) * Pole(900f);
            float twig = random.NextDouble() < 0.004 ? noise * 2f : 0f;
            data[i] = (low * 1.4f + Mathf.Sin(Tau(t * 75f)) * 0.5f) * Mathf.Exp(-t * 26f) + twig * Mathf.Exp(-t * 12f);
        }
        return Make("Step", data);
    }

    static AudioClip BuildLand()
    {
        var random = new System.Random(11);
        float[] data = Buffer(0.3f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low += (Noise(random) - low) * Pole(500f);
            data[i] = (Mathf.Sin(Tau(t * (70f - t * 60f))) + low) * Mathf.Exp(-t * 15f);
        }
        return Make("Land", data);
    }

    static AudioClip BuildBang()
    {
        var random = new System.Random(12);
        float[] data = Buffer(0.5f);
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float metal = Mathf.Sin(Tau(t * 213f)) + Mathf.Sin(Tau(t * 487f)) * 0.7f + Mathf.Sin(Tau(t * 1131f)) * 0.4f;
            data[i] = metal * Mathf.Exp(-t * 11f) + Noise(random) * Mathf.Exp(-t * 60f);
        }
        return Make("Bang", data);
    }

    static AudioClip BuildStart()
    {
        var random = new System.Random(13);
        float[] data = Buffer(1.3f);
        float phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float crank = t < 0.85f ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Tau(t * 11f))), 2f) : 0f;
            float starter = (Mathf.Sin(Tau(t * 190f)) * 0.4f + Noise(random) * 0.25f) * crank;
            phase += (t < 0.85f ? 30f : 42f) / Rate;
            float catchUp = Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.4f);
            float engine = (Mathf.Sin(Tau(phase)) + Mathf.Sin(Tau(phase * 2f)) * 0.6f + Mathf.Sin(Tau(phase * 3f)) * 0.3f) * catchUp;
            data[i] = starter + engine * 0.6f;
        }
        return Make("Start", data);
    }

    static AudioClip BuildStall()
    {
        float[] data = Buffer(1.2f);
        float phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            phase += Mathf.Lerp(46f, 12f, t / 1.2f) / Rate;
            float misfire = Mathf.PerlinNoise(t * 11f, 3f) > 0.32f ? 1f : 0.15f;
            data[i] = (Mathf.Sin(Tau(phase)) + Mathf.Sin(Tau(phase * 2f)) * 0.6f) * misfire * (1f - t / 1.2f);
        }
        return Make("Stall", data);
    }

    static AudioClip BuildCough()
    {
        var random = new System.Random(14);
        float[] data = Buffer(1f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low += (Noise(random) - low) * Pole(1100f);
            float bursts = Mathf.Exp(-t * 16f) + (t > 0.32f ? Mathf.Exp(-(t - 0.32f) * 16f) : 0f) + (t > 0.62f ? 0.7f * Mathf.Exp(-(t - 0.62f) * 14f) : 0f);
            data[i] = (low + Mathf.Sin(Tau(t * 140f)) * 0.3f) * bursts;
        }
        return Make("Cough", data);
    }

    static AudioClip BuildBird()
    {
        float[] data = Buffer(1.1f);
        float[] notes = { 0f, 0.17f, 0.32f, 0.58f };
        float[] pitches = { 3100f, 3600f, 2800f, 3300f };
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(notes[n] * Rate);
            float phase = 0f;
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                if (t > 0.13f)
                    break;
                phase += (pitches[n] + 900f * Mathf.Sin(Mathf.PI * t / 0.13f)) / Rate;
                data[i] += Mathf.Sin(Tau(phase)) * Mathf.Sin(Mathf.PI * t / 0.13f);
            }
        }
        return Make("Bird", data);
    }

    static AudioClip BuildOwl()
    {
        float[] data = Buffer(2f);
        float[] notes = { 0f, 0.62f, 0.95f };
        float[] lengths = { 0.5f, 0.28f, 0.8f };
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(notes[n] * Rate);
            float phase = 0f;
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                if (t > lengths[n])
                    break;
                phase += (345f - t * 45f) / Rate;
                float shape = Mathf.Sin(Mathf.PI * t / lengths[n]);
                data[i] += (Mathf.Sin(Tau(phase)) + Mathf.Sin(Tau(phase * 2f)) * 0.12f) * shape * shape;
            }
        }
        return Make("Owl", data);
    }

    static AudioClip BuildCrack()
    {
        var random = new System.Random(15);
        float[] data = Buffer(0.7f);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(random);
            low += (noise - low) * Pole(700f);
            float snap = noise * Mathf.Exp(-t * 90f);
            float second = t > 0.17f ? noise * 0.5f * Mathf.Exp(-(t - 0.17f) * 110f) : 0f;
            data[i] = snap + second + low * Mathf.Exp(-t * 7f);
        }
        return Make("Crack", data);
    }

    static AudioClip BuildMoan()
    {
        var random = new System.Random(16);
        const float length = 4f;
        float[] data = Buffer(length);
        float phase = 0f, low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            phase += (108f - t * 8f + Mathf.Sin(Tau(t * 5f)) * 1.8f) / Rate;
            low += (Noise(random) - low) * Pole(300f);
            float shape = Mathf.Sin(Mathf.PI * t / length);
            float voice = Mathf.Sin(Tau(phase)) + Mathf.Sin(Tau(phase * 2f)) * 0.55f + Mathf.Sin(Tau(phase * 3f)) * 0.3f + Mathf.Sin(Tau(phase * 5f)) * 0.12f;
            data[i] = (voice + low * 0.5f) * shape * shape;
        }
        return Make("Moan", data);
    }
}
