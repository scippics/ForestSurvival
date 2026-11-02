using UnityEngine;

// Воздух вокруг камеры: пылинки вблизи и клочья тумана над землёй. Частицы живут в мировых
// координатах, а источник просто идёт за камерой, поэтому они не приклеены к взгляду.
public class ForestAtmosphere : MonoBehaviour
{
    const int SoftTextureSize = 64;
    const float MistHeightOffset = -1f;
    const float MistBrightness = 1.5f;

    ParticleSystem dust;
    ParticleSystem mist;

    public static Texture2D SoftTexture { get; private set; }

    public static Material CreateParticleMaterial(float softDistance)
    {
        if (SoftTexture == null)
            SoftTexture = BuildSoftTexture();

        var material = new Material(Resources.Load<Shader>("ForestParticle")) { mainTexture = SoftTexture };
        material.SetFloat("_SoftDistance", softDistance);
        return material;
    }

    static Texture2D BuildSoftTexture()
    {
        var texture = new Texture2D(SoftTextureSize, SoftTextureSize, TextureFormat.RGBA32, false);
        float center = (SoftTextureSize - 1) * 0.5f;
        for (int y = 0; y < SoftTextureSize; y++)
        {
            for (int x = 0; x < SoftTextureSize; x++)
            {
                float distance = new Vector2(x - center, y - center).magnitude / center;
                float alpha = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        return texture;
    }

    void Start()
    {
        dust = CreateSystem("Dust", 0.3f, 120, new Vector3(26f, 9f, 26f));
        ParticleSystem.MainModule dustMain = dust.main;
        dustMain.startLifetime = 9f;
        dustMain.startSpeed = 0.05f;
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        ParticleSystem.EmissionModule dustEmission = dust.emission;
        dustEmission.rateOverTime = 14f;
        ParticleSystem.NoiseModule noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.35f;
        dust.Play();

        mist = CreateSystem("Mist", 3f, 40, new Vector3(70f, 1.5f, 70f));
        ParticleSystem.MainModule mistMain = mist.main;
        mistMain.startLifetime = 18f;
        mistMain.startSpeed = 0.25f;
        mistMain.startSize = new ParticleSystem.MinMaxCurve(9f, 16f);
        mistMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.EmissionModule mistEmission = mist.emission;
        mistEmission.rateOverTime = 2f;
        mist.Play();
    }

    ParticleSystem CreateSystem(string systemName, float softDistance, int maxParticles, Vector3 area)
    {
        var systemObject = new GameObject(systemName);
        systemObject.transform.SetParent(transform, false);
        var system = systemObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colorOverLife = system.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = fade;

        var particleRenderer = systemObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = CreateParticleMaterial(softDistance);
        particleRenderer.maxParticleSize = 4f;
        return system;
    }

    void LateUpdate()
    {
        Camera view = Camera.main;
        if (view == null || dust == null)
            return;

        Vector3 position = view.transform.position;
        dust.transform.position = position;
        mist.transform.position = position + Vector3.up * MistHeightOffset;

        // Новые частицы берут цвет текущего часа: ночью туман не должен светиться дневным серым.
        Color fog = RenderSettings.fogColor * MistBrightness;
        ParticleSystem.MainModule mistMain = mist.main;
        mistMain.startColor = new Color(fog.r, fog.g, fog.b, 0.09f);

        Color motes = Color.Lerp(RenderSettings.fogColor, Color.white, 0.55f);
        ParticleSystem.MainModule dustMain = dust.main;
        dustMain.startColor = new Color(motes.r, motes.g, motes.b, DayNightCycle.IsDark ? 0.08f : 0.4f);
    }
}
