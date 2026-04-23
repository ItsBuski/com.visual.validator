#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using VisualValidator.Runtime;

namespace VisualValidator.Editor
{
    [Serializable]
    public class ReportData : CaptureMetadata
    {
        public List<string> errors = new List<string>();
        public bool is_bug;
    }

    public class ValidatorReportWindow : EditorWindow
    {
        private List<string> reports = new List<string>();
        private Vector2 scrollPos;
        private string capturesPath;
        private string expandedReport = "";
        private Texture2D textureA, textureB;
        private ReportData currentData;

        [MenuItem("Window/Visual Validator/Report Explorer")]
        public static void ShowWindow() => GetWindow<ValidatorReportWindow>("Report Explorer");

        private void OnEnable()
        {
            capturesPath = Path.Combine(Directory.GetCurrentDirectory(), "ValidationCaptures");
            Refresh();
        }

        private void OnDisable() => ClearTextures();

        private void Refresh()
        {
            ClearTextures();
            expandedReport = "";
            if (Directory.Exists(capturesPath))
            {
                reports = Directory.GetFiles(capturesPath, "*_REPORT.json")
                    .OrderByDescending(File.GetCreationTime)
                    .ToList();
            }
        }

        private void ClearTextures()
        {
            if (textureA) DestroyImmediate(textureA);
            if (textureB) DestroyImmediate(textureB);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("DETECTION REPORTS", EditorStyles.boldLabel);
            if (GUILayout.Button("REFRESH LIST", GUILayout.Height(30))) Refresh();
            EditorGUILayout.EndVertical();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            if (reports.Count == 0) EditorGUILayout.HelpBox("No issues found.", MessageType.Info);

            foreach (string r in reports) DrawReportRow(r);
            EditorGUILayout.EndScrollView();
        }

        private void DrawReportRow(string fullPath)
        {
            bool isExpanded = expandedReport == fullPath;
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(Path.GetFileNameWithoutExtension(fullPath).Replace("_REPORT", ""), EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(isExpanded ? "CLOSE" : "INSPECT", GUILayout.Width(80)))
            {
                if (isExpanded) { expandedReport = ""; ClearTextures(); }
                else { expandedReport = fullPath; LoadReportContent(fullPath); }
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("TELEPORT TO SOURCE", GUILayout.Height(22))) Teleport(fullPath);
            GUI.backgroundColor = Color.white;

            if (isExpanded && textureA != null && currentData != null)
            {
                float w = position.width - 40;
                float h = (w / 2f) * 0.5625f;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box(textureA, GUILayout.Width(w/2), GUILayout.Height(h));
                GUILayout.Box(textureB, GUILayout.Width(w/2), GUILayout.Height(h));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("DATA INSPECTOR", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Scene:", currentData.scene);
                EditorGUILayout.LabelField("Point ID:", currentData.pointID);
                EditorGUILayout.LabelField("Timestamp:", currentData.timestamp);
                EditorGUILayout.LabelField("Position:", currentData.coordinates.ToString());
                EditorGUILayout.LabelField("Rotation:", currentData.rotation.eulerAngles.ToString());
                
                if (currentData.errors != null && currentData.errors.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    GUILayout.Label("DETECTED ISSUES:", EditorStyles.boldLabel);
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    foreach (string err in currentData.errors)
                    {
                        GUILayout.Label($"• {err}", EditorStyles.wordWrappedLabel);
                    }
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void LoadReportContent(string path)
        {
            ClearTextures();
            textureA = Load(path.Replace("_REPORT.json", "_FrameA.png"));
            textureB = Load(path.Replace("_REPORT.json", "_FrameB.png"));
            
            if (File.Exists(path))
            {
                currentData = JsonUtility.FromJson<ReportData>(File.ReadAllText(path));
            }
        }

        private Texture2D Load(string p)
        {
            if (!File.Exists(p)) return null;
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(p));
            return t;
        }

        private void Teleport(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return;
            var data = JsonUtility.FromJson<CaptureMetadata>(File.ReadAllText(jsonPath));
            
            if (SceneManager.GetActiveScene().name != data.scene)
            {
                EditorUtility.DisplayDialog("Wrong Scene", "Load the correct scene before teleporting.", "OK");
                return;
            }

            SceneView view = SceneView.lastActiveSceneView ?? SceneView.sceneViews[0] as SceneView;
            if (view != null)
            {
                view.pivot = data.coordinates;
                view.rotation = data.rotation;
                view.size = 0f;
                view.Repaint();
            }
        }
    }
}
#endif