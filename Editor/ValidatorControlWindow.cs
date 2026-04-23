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
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;

        [MenuItem("Window/Visual Validator/Control Panel")]
        public static void ShowWindow() => GetWindow<ValidatorControlWindow>("Validator Control");

        private void OnGUI()
        {
            InitStyles();

            // --- HEADER ---
            EditorGUILayout.BeginVertical(headerStyle);
            GUILayout.Label("VISUAL VALIDATOR", EditorStyles.whiteLargeLabel);
            GUILayout.Label("Automation Suite v1.2", EditorStyles.whiteMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // --- SETUP SECTION ---
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Infrastructure Setup", EditorStyles.boldLabel);
            if (GUILayout.Button("EXTRACT TOOLS TO PROJECT ROOT", GUILayout.Height(30)))
            {
                ExtractTools();
            }
            EditorGUILayout.HelpBox("Cloned packages require tool extraction to the project root to bypass OS permission restrictions.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            // --- EXECUTION SECTION ---
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("▶ RUN VALIDATION PIPELINE", GUILayout.Height(45)))
            {
                ExecutePipeline();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.HelpBox("Warning: This will save current scenes and restart Unity in Batchmode.", MessageType.Warning);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("© 2026 VISUAL VALIDATOR TOOLS", EditorStyles.centeredGreyMiniLabel);
        }

        private void ExtractTools()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string targetDir = Path.Combine(projectRoot, "ValidationTools");
            string packagePath = Path.GetFullPath("Packages/com.visual.validator/Tools~");

            if (!Directory.Exists(packagePath))
            {
                EditorUtility.DisplayDialog("Error", "Package 'Tools~' folder not found. Ensure the package name is 'com.visual.validator'.", "OK");
                return;
            }

            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            try
            {
                string[] files = { "RunValidation.bat", "analyze_captures.py" };
                foreach (string f in files)
                {
                    File.Copy(Path.Combine(packagePath, f), Path.Combine(targetDir, f), true);
                }
                EditorUtility.DisplayDialog("Setup Success", "Tools extracted to /ValidationTools/. You are ready to go.", "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Validator] Extraction failed: {e.Message}");
            }
        }

        private void ExecutePipeline()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string batPath = Path.Combine(projectRoot, "ValidationTools", "RunValidation.bat");

            if (!File.Exists(batPath))
            {
                if (EditorUtility.DisplayDialog("Missing Tools", "Tools must be extracted before running validation.", "Extract Now", "Cancel"))
                    ExtractTools();
                return;
            }

            EditorSceneManager.SaveOpenScenes();

            ProcessStartInfo psi = new ProcessStartInfo
            {
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
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.box);
                headerStyle.normal.background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.12f, 1f));
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
}
#endif