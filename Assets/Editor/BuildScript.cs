using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Сборка только из командной строки:
// Unity.exe -quit -batchmode -projectPath . -executeMethod BuildScript.BuildAll
// Код возврата 0 — собраны все платформы, 1 — хотя бы одна не собралась.
public static class BuildScript
{
    const string ProductName = "ForestSurvival";

    // Сцена пустая: весь мир создаёт RuntimeBootstrapper при старте.
    static readonly string[] Scenes = { "Assets/Scenes/Boot.unity" };

    public static void BuildAll()
    {
        // Windows последней: после сборки редактор остаётся на основной платформе.
        bool macOS = Build(BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, "Build/macOS/" + ProductName + ".app");
        bool windows = Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Build/Windows/" + ProductName + ".exe");

        Debug.Log($"[BuildScript] Windows: {Describe(windows)}, macOS: {Describe(macOS)}");
        EditorApplication.Exit(windows && macOS ? 0 : 1);
    }

    static bool Build(BuildTarget target, BuildTargetGroup group, string path)
    {
        if (!BuildPipeline.IsBuildTargetSupported(group, target))
        {
            Debug.LogError($"[BuildScript] {target}: модуль сборки не установлен в Unity Hub");
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(Scenes, path, target, BuildOptions.None);
            BuildSummary summary = report.summary;
            Debug.Log($"[BuildScript] {target}: {summary.result}, ошибок {summary.totalErrors}, {summary.totalSize / (1024 * 1024)} МБ, {path}");
            return summary.result == BuildResult.Succeeded;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BuildScript] {target}: {exception}");
            return false;
        }
    }

    static string Describe(bool succeeded)
    {
        return succeeded ? "собрано" : "НЕ собрано";
    }
}
