using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Собирает всё, что нужно для починки, в один файл ForestSurvival_Report.txt. Файл появляется
// только при проблеме: ошибка в коде, неподдерживаемый шейдер или падение прошлого запуска.
public class ErrorReporter : MonoBehaviour
{
    const string ReportFileName = "ForestSurvival_Report.txt";
    const string RunningMarkerFileName = "session.running";
    const int MaxEntriesPerSession = 100;
    const int CrashLogTailLines = 300;
    const long MaxReportBytes = 2 * 1024 * 1024;
    const float MessageSeconds = 15f;

    static readonly string[] RequiredShaders = { "ForestLit", "CustomWindFoliage" };

    readonly object writeLock = new object();
    readonly HashSet<string> seenMessages = new HashSet<string>();

    string folder;
    string reportPath;
    string markerPath;
    bool headerWritten;
    int entries;
    volatile bool newProblem;
    float messageUntil;
    GUIStyle messageStyle;

    // До сборки сцены: ошибка в самом RuntimeBootstrapper тоже должна попасть в отчёт.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var reporter = new GameObject("ErrorReporter");
        DontDestroyOnLoad(reporter);
        reporter.AddComponent<ErrorReporter>();
    }

    void Awake()
    {
        folder = Application.persistentDataPath;
        reportPath = Path.Combine(folder, ReportFileName);
        markerPath = Path.Combine(folder, RunningMarkerFileName);

        TrimOldReport();
        Application.logMessageReceivedThreaded += OnLogMessage;
        Application.quitting += OnQuitting;

        ReportPreviousCrash();
        WriteRunningMarker();
        CheckCompatibility();
    }

    void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= OnLogMessage;
        Application.quitting -= OnQuitting;
    }

    void Update()
    {
        if (newProblem)
        {
            newProblem = false;
            messageUntil = Time.unscaledTime + MessageSeconds;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && File.Exists(reportPath))
            Application.OpenURL(new Uri(folder).AbsoluteUri);
    }

    void OnGUI()
    {
        if (Time.unscaledTime >= messageUntil || Event.current.type != EventType.Repaint)
            return;

        if (messageStyle == null)
        {
            messageStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            messageStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
        }
        messageStyle.fontSize = Mathf.RoundToInt(16f * Screen.height / 1080f);

        GUI.color = Color.white;
        GUI.Label(new Rect(20f, 20f, Screen.width - 40f, Screen.height * 0.2f),
            "Ошибка записана в отчёт. F9 - открыть папку с файлом " + ReportFileName, messageStyle);
    }

    void OnQuitting()
    {
        // Нет маркера — запуск завершился штатно. Если он остался, значит игра упала.
        TryDelete(markerPath);
    }

    void OnLogMessage(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        lock (writeLock)
        {
            if (entries >= MaxEntriesPerSession || !seenMessages.Add(message + stackTrace))
                return;
            entries++;
        }

        Write($"[{DateTime.Now:HH:mm:ss}] {type}: {message}\n{stackTrace}\n");
    }

    void CheckCompatibility()
    {
        if (!SystemInfo.supportsInstancing)
            Write("ПРОБЛЕМА: видеокарта не поддерживает GPU Instancing, лес не отрисуется\n");

        foreach (string shaderName in RequiredShaders)
        {
            Shader shader = Resources.Load<Shader>(shaderName);
            if (shader == null)
                Write($"ПРОБЛЕМА: шейдер {shaderName} отсутствует в сборке\n");
            else if (!shader.isSupported)
                Write($"ПРОБЛЕМА: шейдер {shaderName} не поддерживается этой видеокартой\n");
        }
    }

    void ReportPreviousCrash()
    {
        if (!File.Exists(markerPath))
            return;

        // Unity при старте переименовывает лог прошлого запуска в Player-prev.
        string previousLog = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath) ?? folder, "Player-prev.log");
        var text = new StringBuilder("ПРОШЛЫЙ ЗАПУСК ЗАВЕРШИЛСЯ АВАРИЙНО\n");
        try
        {
            if (File.Exists(previousLog))
            {
                string[] lines = File.ReadAllLines(previousLog);
                int start = Mathf.Max(0, lines.Length - CrashLogTailLines);
                text.Append($"--- Конец лога упавшего запуска ({lines.Length - start} строк) ---\n");
                for (int i = start; i < lines.Length; i++)
                    text.Append(lines[i]).Append('\n');
                text.Append("--- Конец лога ---\n");
            }
            else
            {
                text.Append("Лог упавшего запуска не найден: ").Append(previousLog).Append('\n');
            }
        }
        catch (Exception exception)
        {
            text.Append("Не удалось прочитать лог: ").Append(exception.Message).Append('\n');
        }
        Write(text.ToString());
    }

    void Write(string text)
    {
        // Здесь нельзя логировать: сообщение снова пришло бы сюда же и зациклилось.
        lock (writeLock)
        {
            try
            {
                if (!headerWritten)
                {
                    File.AppendAllText(reportPath, BuildHeader());
                    headerWritten = true;
                }
                File.AppendAllText(reportPath, text + "\n");
                newProblem = true;
            }
            catch (Exception)
            {
                // Отчёт не должен ронять игру, если диск недоступен.
            }
        }
    }

    string BuildHeader()
    {
        return "==================================================\n"
               + $"Запуск: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n"
               + $"Игра: {Application.productName} {Application.version}, Unity {Application.unityVersion}\n"
               + $"Система: {SystemInfo.operatingSystem}\n"
               + $"Процессор: {SystemInfo.processorType}, {SystemInfo.processorCount} ядер, ОЗУ {SystemInfo.systemMemorySize} МБ\n"
               + $"Видеокарта: {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize} МБ\n"
               + $"Графика: {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceVersion}, шейдеры {SystemInfo.graphicsShaderLevel}\n"
               + $"Инстансинг: {SystemInfo.supportsInstancing}\n"
               + $"Экран: {Screen.width}x{Screen.height}\n"
               + $"Лог движка: {Application.consoleLogPath}\n"
               + "==================================================\n";
    }

    void WriteRunningMarker()
    {
        try
        {
            File.WriteAllText(markerPath, DateTime.Now.ToString("O"));
        }
        catch (Exception)
        {
            // Без маркера не определится только падение, остальные ошибки запишутся.
        }
    }

    void TrimOldReport()
    {
        try
        {
            if (File.Exists(reportPath) && new FileInfo(reportPath).Length > MaxReportBytes)
                File.Delete(reportPath);
        }
        catch (Exception)
        {
            // Старый отчёт останется, это не мешает игре.
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Следующий запуск просто ошибочно сообщит о падении.
        }
    }
}
