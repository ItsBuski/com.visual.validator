#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace VisualValidator.Editor
{
    public class ValidatorReportWindow : EditorWindow
    {
        private List<string> reports = new List<string>();
        private Vector2 scrollPos;
        private string capturesPath;
        private string expandedReport = "";
        private Texture2D textureA, textureB;

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
            if (reports.Count == 0) EditorGUILayout.HelpBox("No issues found. Everything looks good!", MessageType.Info);

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
            if (GUILayout.Button(isExpanded ? "CLOSE" : "INSPECT IMAGES", GUILayout.Width(110)))
            {
                if (isExpanded) { expandedReport = ""; ClearTextures(); }
                else { expandedReport = fullPath; LoadTextures(fullPath); }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("TELEPORT TO SOURCE", GUILayout.Height(22))) Teleport(fullPath);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (isExpanded && textureA != null)
            {
                float w = position.width - 40;
                float h = (w / 2f) * 0.5625f; // 16:9 ratio
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box(textureA, GUILayout.Width(w/2), GUILayout.Height(h));
                GUILayout.Box(textureB, GUILayout.Width(w/2), GUILayout.Height(h));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void LoadTextures(string path)
        {
            ClearTextures();
            textureA = Load(path.Replace("_REPORT.json", "_FrameA.png"));
            textureB = Load(path.Replace("_REPORT.json", "_FrameB.png"));
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
                EditorUtility.DisplayDialog("Wrong Scene", $"Point belongs to '{data.scene}'. Open that scene first.", "OK");
                return;
            }

            SceneView view = SceneView.lastActiveSceneView ?? SceneView.sceneViews[0] as SceneView;
            if (view != null)
            {
                view.pivot = data.coordinates;
                view.rotation = data.rotation;
                view.size = 0f; // 1:1 view matching
                view.Repaint();
            }
        }
    }
}
#endif