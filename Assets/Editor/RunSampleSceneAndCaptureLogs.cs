#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

// Editor utility: open SampleScene, ensure example abilities exist and scene configured,
// enter Play Mode for a fixed duration, capture Unity console logs to Assets/Logs/sample_scene_log.txt,
// then exit Play Mode and save the log file.
public static class RunSampleSceneAndCaptureLogs
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string LogFolder = "Assets/Logs";
    private const string LogPath = LogFolder + "/sample_scene_log.txt";
    private static StreamWriter _logWriter;
    private static double _stopTime = 0;
    private static readonly double RunDurationSeconds = 8.0; // seconds to run Play Mode

    [MenuItem("GAS Example/Run Sample Scene and Capture Logs")]
    public static void RunAndCapture()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"SampleScene not found at {ScenePath}. Please ensure the scene exists.");
            return;
        }

        // Ensure example assets exist (best-effort)
        var method = typeof(CreateExampleAbilities).GetMethod("CreateAssets", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (method != null)
        {
            method.Invoke(null, null);
            Debug.Log("Ensured example abilities exist.");
        }
        else
        {
            Debug.LogWarning("CreateExampleAbilities.CreateAssets not found. If example assets are missing, run GAS Example -> Create Example Abilities.");
        }

        // Configure scene (best-effort)
        var cfgMethod = typeof(ConfigureSampleSceneForGAS).GetMethod("ConfigureSampleScene", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (cfgMethod != null)
        {
            cfgMethod.Invoke(null, null);
            Debug.Log("Configured SampleScene for GAS demo (best-effort).");
        }
        else
        {
            Debug.LogWarning("ConfigureSampleSceneForGAS.ConfigureSampleScene not found. If scene is not configured, run GAS Example -> Configure Sample Scene.");
        }

        // Open scene
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"Opened scene: {scene.path}");

        // Prepare log file
        if (!Directory.Exists(LogFolder)) Directory.CreateDirectory(LogFolder);
        _logWriter = new StreamWriter(LogPath, false);
        _logWriter.AutoFlush = true;
        _logWriter.WriteLine($"SampleScene run started: {System.DateTime.Now}");

        // Subscribe to logs
        Application.logMessageReceived += HandleLog;

        // Start Play Mode
        _stopTime = EditorApplication.timeSinceStartup + RunDurationSeconds;
        EditorApplication.update += UpdateLoop;
        EditorApplication.isPlaying = true;
    }

    private static void UpdateLoop()
    {
        // Wait until play mode enters play state
        if (!EditorApplication.isPlaying) return;

        // Run until stop time
        if (EditorApplication.timeSinceStartup >= _stopTime)
        {
            // Stop Play Mode
            EditorApplication.isPlaying = false;
            EditorApplication.update -= UpdateLoop;

            // Unsubscribe and close file
            Application.logMessageReceived -= HandleLog;
            if (_logWriter != null)
            {
                _logWriter.WriteLine($"SampleScene run ended: {System.DateTime.Now}");
                _logWriter.Close();
                _logWriter = null;
            }

            Debug.Log($"SampleScene run complete. Logs written to {LogPath}");
        }
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (_logWriter == null) return;
        _logWriter.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] {type}: {condition}");
        if (!string.IsNullOrEmpty(stackTrace))
            _logWriter.WriteLine(stackTrace);
    }
}
#endif