#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System;

public class ValidatorControlWindow : EditorWindow
{
    private GUIStyle headerStyle;
    private GUIStyle sectionStyle;

    [MenuItem("Window/Visual Validator/Control Panel")]
    public static void ShowWindow() => GetWindow<ValidatorControlWindow>("Validator Control");

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.BeginVertical(headerStyle);
        GUILayout.Label("VISUAL VALIDATOR", EditorStyles.whiteLargeLabel);
        GUILayout.Label("Pipeline Automation Tool v1.2", EditorStyles.whiteMiniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(sectionStyle);
        GUILayout.Label("First Time Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("EXTRACT TOOLS TO PROJECT ROOT", GUILayout.Height(30)))
        {
            ExtractTools();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(15);

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("▶ EXECUTE FULL VALIDATION", GUILayout.Height(45)))
        {
            ExecuteFromProjectRoot();
        }
        GUI.backgroundColor = Color.white;
    }

    private void ExtractTools()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string targetDir = Path.Combine(projectRoot, "ValidationTools");
        string packagePath = Path.GetFullPath("Packages/com.visual.validator/Tools~");
        
        if (!Directory.Exists(packagePath))
        {
            EditorUtility.DisplayDialog("Error", "Package tools not found. Check if folder is named 'com.visual.validator'.", "OK");
            return;
        }

        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        try {
            string[] files = { "RunValidation.bat", "analyze_captures.py" };
            foreach (string f in files)
            {
                File.Copy(Path.Combine(packagePath, f), Path.Combine(targetDir, f), true);
            }
            EditorUtility.DisplayDialog("Success", "Tools extracted to /ValidationTools/.", "OK");
        } catch (Exception e) {
            UnityEngine.Debug.LogError($"[Validator] Setup Error: {e.Message}");
        }
    }

    private void ExecuteFromProjectRoot()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string batPath = Path.Combine(projectRoot, "ValidationTools", "RunValidation.bat");

        if (!File.Exists(batPath))
        {
            if (EditorUtility.DisplayDialog("Tools Missing", "Automation tools not found in project root.", "Extract Now", "Cancel"))
                ExtractTools();
            return;
        }

        EditorSceneManager.SaveOpenScenes();

        ProcessStartInfo psi = new ProcessStartInfo {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{batPath}\" \"{projectRoot}\"\"",
            WorkingDirectory = Path.GetDirectoryName(batPath),
            UseShellExecute = true
        };

        Process.Start(psi);
        EditorApplication.Exit(0);
    }

    private void InitStyles()
    {
        if (headerStyle == null) {
            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 1f));
            headerStyle.padding = new RectOffset(15, 15, 10, 10);
        }
        if (sectionStyle == null) sectionStyle = new GUIStyle(EditorStyles.helpBox);
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
#endif