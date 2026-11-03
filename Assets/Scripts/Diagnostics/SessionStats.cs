using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

// Журнал производительности: каждую минуту строка в ForestSurvival_Performance.txt, в конце итог сессии.
// Пишется сразу на диск, поэтому переживает падение, а падение FPS к концу таблицы выдаёт перегрев.
public class SessionStats : MonoBehaviour
{
    const string FileName = "ForestSurvival_Performance.txt";
    const long MaxFileBytes = 1024 * 1024;
    const float RowSeconds = 60f;
    const int MaxFrameMs = 250;
    const float Mega = 1024f * 1024f;
    const double NanosecondsPerMs = 1000000.0;
    const string ThermalKey = "CPU_Speed_Limit";

    class Window
    {
        public readonly int[] Frames = new int[MaxFrameMs + 1];
        public int Count;
        public double Seconds;
        public double Cpu;
        public double Render;
        public double Gpu;
        public int TimingCount;
        public int GpuCount;

        public void Add(float seconds)
        {
            Frames[Mathf.Clamp(Mathf.RoundToInt(seconds * 1000f), 0, MaxFrameMs)]++;
            Count++;
            Seconds += seconds;
        }

        public void Clear()
        {
            Array.Clear(Frames, 0, Frames.Length);
            Count = 0;
            Seconds = 0.0;
            Cpu = 0.0;
            Render = 0.0;
            Gpu = 0.0;
            TimingCount = 0;
            GpuCount = 0;
        }

        public float AverageFps => Seconds > 0.0 ? (float)(Count / Seconds) : 0f;

        // Частота, хуже которой идёт самый медленный 1% кадров.
        public float LowFps
        {
            get
            {
                int left = Mathf.CeilToInt(Count * 0.01f);
                for (int ms = MaxFrameMs; ms > 0; ms--)
                {
                    left -= Frames[ms];
                    if (left <= 0)
                        return 1000f / ms;
                }
                return 0f;
            }
        }
    }

    readonly Window row = new Window();
    readonly Window played = new Window();
    readonly FrameTiming[] timing = new FrameTiming[1];

    string path;
    float rowStart;
    double lastProcessSeconds;
    float lastProcessTime;
    double processPercentSum;
    int processRows;
    volatile string thermal = "";
    ProfilerRecorder systemMemory;
    ProfilerRecorder drawCalls;
    ProfilerRecorder triangles;
    double worstCpu;
    bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var stats = new GameObject("SessionStats");
        DontDestroyOnLoad(stats);
        stats.AddComponent<SessionStats>();
    }

    void Awake()
    {
        path = Path.Combine(Application.persistentDataPath, FileName);
        TrimOldFile();

        systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        Application.quitting += Finish;

        lastProcessSeconds = ProcessSeconds();
        WriteHeader();
    }

    void OnDestroy()
    {
        Application.quitting -= Finish;
        systemMemory.Dispose();
        drawCalls.Dispose();
        triangles.Dispose();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        row.Add(dt);
        if (GameFlow.State == GameState.Playing)
            played.Add(dt);

        ReadFrameTiming();

        if (Time.unscaledTime - rowStart >= RowSeconds)
            WriteRow();
    }

    void ReadFrameTiming()
    {
        if (!FrameTimingManager.IsFeatureEnabled())
            return;

        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timing) == 0)
            return;

        // Ожидание кадра не считается: иначе при ограничении FPS процессор выглядел бы занятым полностью.
        double cpu = Math.Max(0.0, timing[0].cpuMainThreadFrameTime - timing[0].cpuMainThreadPresentWaitTime);
        row.Cpu += cpu;
        row.Render += timing[0].cpuRenderThreadFrameTime;
        row.TimingCount++;
        worstCpu = Math.Max(worstCpu, cpu);
        if (timing[0].gpuFrameTime > 0.0)
        {
            row.Gpu += timing[0].gpuFrameTime;
            row.GpuCount++;
        }
    }

    void WriteRow()
    {
        double seconds = ProcessSeconds();
        float elapsed = Time.unscaledTime - lastProcessTime;
        double percent = elapsed > 0f ? (seconds - lastProcessSeconds) / elapsed * 100.0 : 0.0;
        lastProcessSeconds = seconds;
        lastProcessTime = Time.unscaledTime;
        processPercentSum += percent;
        processRows++;

        var text = new StringBuilder();
        text.Append($"{Time.unscaledTime / 60f,6:F1} | {GameFlow.State,-8} | {row.AverageFps,5:F0} | {row.LowFps,5:F0} | ");
        text.Append($"{Average(row.Cpu, row.TimingCount),6:F1} | {Average(row.Render, row.TimingCount),6:F1} | {Average(row.Gpu, row.GpuCount),6:F1} | ");
        text.Append($"{percent,5:F0}% | {Mb(systemMemory),6:F0} | {Counter(drawCalls),6} | {Counter(triangles),10}");
        if (thermal.Length > 0)
            text.Append(" | ").Append(thermal);
        Append(text.Append('\n').ToString());

        row.Clear();
        rowStart = Time.unscaledTime;
        ReadThermalLater();
    }

    void Finish()
    {
        if (finished)
            return;
        finished = true;

        WriteRow();

        var text = new StringBuilder("Итог:\n");
        text.Append($"Длительность: {Time.unscaledTime / 60f:F1} мин, в самой игре {played.Seconds / 60.0:F1} мин\n");
        text.Append($"FPS в игре: средний {played.AverageFps:F0}, худший 1% {played.LowFps:F0}\n");
        if (processRows > 0)
            text.Append($"Процесс игры в среднем: {processPercentSum / processRows:F0}% одного ядра, ядер всего {SystemInfo.processorCount}\n");
        text.Append($"Самый тяжёлый кадр основного потока: {worstCpu:F1} мс\n");
        text.Append("Журнал не показывает температуру: на Mac её игре никто не сообщает, а падение FPS со временем говорит о перегреве косвенно.\n\n");
        Append(text.ToString());
    }

    void WriteHeader()
    {
        var text = new StringBuilder("==================================================\n");
        text.Append($"Сессия: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        text.Append($"Игра: {Application.productName} {Application.version}, Unity {Application.unityVersion}\n");
        text.Append($"Устройство: {SystemInfo.deviceModel}\n");
        text.Append($"Система: {SystemInfo.operatingSystem}\n");
        text.Append($"Процессор: {SystemInfo.processorType}, {SystemInfo.processorCount} ядер, ОЗУ {SystemInfo.systemMemorySize} МБ\n");
        text.Append($"Видеокарта: {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize} МБ, {SystemInfo.graphicsDeviceType}\n");
        text.Append($"Экран: {Screen.width}x{Screen.height}, качество {GameSettings.QualityNames[GameSettings.Quality]}, предел FPS {GameSettings.FrameLimitNames[GameSettings.FrameLimit]}\n");
        text.Append("Время ЦП, рендера и ГП: ").Append(FrameTimingManager.IsFeatureEnabled() ? "есть\n" : "недоступно\n");
        text.Append("   мин | режим    |   FPS | 1%FPS | ЦП мс  | рендер | ГП мс  |   ЦП% | ОЗУ МБ | вызовы | треугольники\n");
        Append(text.ToString());
    }

    static double Average(double sum, int count)
    {
        return count > 0 ? sum / count : 0.0;
    }

    static double Mb(ProfilerRecorder recorder)
    {
        return recorder.Valid ? recorder.LastValue / Mega : 0.0;
    }

    static string Counter(ProfilerRecorder recorder)
    {
        return recorder.Valid && recorder.LastValue > 0 ? recorder.LastValue.ToString() : "-";
    }

    static double ProcessSeconds()
    {
        try
        {
            return Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
        }
        catch (Exception)
        {
            return 0.0;
        }
    }

    // Запрос к системе идёт отдельным потоком, чтобы не дёргать кадр: pmset отвечает не мгновенно.
    void ReadThermalLater()
    {
        if (Application.platform != RuntimePlatform.OSXPlayer)
            return;

        Task.Run(() =>
        {
            try
            {
                var start = new ProcessStartInfo("/usr/bin/pmset", "-g therm") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (Process process = Process.Start(start))
                {
                    foreach (string line in process.StandardOutput.ReadToEnd().Split('\n'))
                    {
                        if (line.Contains(ThermalKey))
                            thermal = line.Trim();
                    }
                }
            }
            catch (Exception)
            {
                // Без этой строки журнал остаётся полезным.
            }
        });
    }

    void Append(string text)
    {
        try
        {
            File.AppendAllText(path, text);
        }
        catch (Exception)
        {
            // Журнал не должен ронять игру, если диск недоступен.
        }
    }

    void TrimOldFile()
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > MaxFileBytes)
                File.Delete(path);
        }
        catch (Exception)
        {
            // Старый журнал просто останется.
        }
    }
}
