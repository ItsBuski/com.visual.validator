#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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

    private void Refresh()
    {
        ClearTextures();
        expandedReport = "";
        if (Directory.Exists(capturesPath))
            reports = Directory.GetFiles(capturesPath, "*_REPORT.json").OrderByDescending(File.GetCreationTime).ToList();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("VALIDATION REPORTS", EditorStyles.boldLabel);
        if (GUILayout.Button("REFRESH LIST", GUILayout.Height(30))) Refresh();
        EditorGUILayout.EndVertical();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        if (reports.Count == 0) EditorGUILayout.HelpBox("No bugs detected.", MessageType.Info);

        foreach (string r in reports) DrawReportRow(r);
        EditorGUILayout.EndScrollView();
    }

    private void DrawReportRow(string fullPath)
    {
        bool isExpanded = expandedReport == fullPath;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(Path.GetFileNameWithoutExtension(fullPath), EditorStyles.miniBoldLabel);
        if (GUILayout.Button(isExpanded ? "CLOSE" : "VIEW IMAGES", GUILayout.Width(100))) {
            if (isExpanded) { expandedReport = ""; ClearTextures(); }
            else { expandedReport = fullPath; LoadTextures(fullPath); }
        }
        EditorGUILayout.EndHorizontal();

        if (isExpanded && textureA != null) {
            float w = position.width - 40;
            float h = (w / 2f) * 0.5625f; 
            EditorGUILayout.BeginHorizontal();
            GUI.DrawTexture(GUILayoutUtility.GetRect(w/2, h), textureA, ScaleMode.ScaleToFit);
            GUI.DrawTexture(GUILayoutUtility.GetRect(w/2, h), textureB, ScaleMode.ScaleToFit);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    private void LoadTextures(string path) {
        ClearTextures();
        textureA = Load(path.Replace("_REPORT.json", "_FrameA.png"));
        textureB = Load(path.Replace("_REPORT.json", "_FrameB.png"));
    }

    private Texture2D Load(string p) {
        if (!File.Exists(p)) return null;
        Texture2D t = new Texture2D(2,2);
        t.LoadImage(File.ReadAllBytes(p));
        return t;
    }

    private void ClearTextures() {
        if (textureA) DestroyImmediate(textureA);
        if (textureB) DestroyImmediate(textureB);
    }
}
#endif