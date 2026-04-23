#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System;

namespace VisualValidator.Editor
{
    public class ValidatorControlWindow : EditorWindow
    {
        [MenuItem("Window/Visual Validator/Control Panel")]
        public static void ShowWindow() => GetWindow<ValidatorControlWindow>("Validator Control");

        private void OnGUI()
        {
            GUILayout.Label("INFRASTRUCTURE", EditorStyles.boldLabel);
            if (GUILayout.Button("EXTRACT TOOLS", GUILayout.Height(30))) ExtractTools();

            EditorGUILayout.Space(20);

            GUILayout.Label("EXECUTION PIPELINES", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
            if (GUILayout.Button("RUN (STANDARD / URP)", GUILayout.Height(45)))
                ExecutePipeline("VisualValidator.Editor.AutomatedSceneScanner.RunStandardScan");

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(1f, 0.5f, 0.2f);
            if (GUILayout.Button("RUN (HDRP)", GUILayout.Height(45)))
                ExecutePipeline("VisualValidator.Editor.AutomatedSceneScanner.RunHDRPScan");

            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Batch scan will save open scenes and restart Unity.", MessageType.Warning);
        }

        private void ExtractTools()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string targetDir = Path.Combine(projectRoot, "ValidationTools");
            string packagePath = Path.GetFullPath("Packages/com.visual.validator/Tools~");

            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            try
            {
                string[] files = { "RunValidation.bat", "analyze_captures.py" };
                foreach (string f in files) File.Copy(Path.Combine(packagePath, f), Path.Combine(targetDir, f), true);
                EditorUtility.DisplayDialog("Success", "Tools extracted.", "OK");
            }
            catch (Exception e) { UnityEngine.Debug.LogError($"Extraction failed: {e.Message}"); }
        }

        private void ExecutePipeline(string method)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string batPath = Path.Combine(projectRoot, "ValidationTools", "RunValidation.bat");

            if (!File.Exists(batPath)) { ExtractTools(); return; }

            EditorSceneManager.SaveOpenScenes();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\" {method}\"",
                WorkingDirectory = Path.GetDirectoryName(batPath),
                UseShellExecute = true
            };

            Process.Start(psi);
            EditorApplication.Exit(0);
        }
    }
}
#endif