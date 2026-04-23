#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System;
using VisualValidator.Runtime;

namespace VisualValidator.Editor
{
    [Serializable]
    public class CaptureMetadata
    {
        public string timestamp;
        public string scene;
        public string pointID;
        public Vector3 coordinates;
        public Quaternion rotation;
    }

    public static class AutomatedSceneScanner
    {
        public static void RunHeadlessScan()
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ValidationCaptures");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            int totalScenes = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[Visual Validator] STARTING SCAN. Scenes in Build Settings: {totalScenes}");

            if (totalScenes == 0)
            {
                Debug.LogError("[Visual Validator] ERROR: No scenes found in Build Settings!");
                EditorApplication.Exit(1);
                return;
            }

            for (int i = 0; i < totalScenes; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                Debug.Log($"[Visual Validator] Opening Scene ({i + 1}/{totalScenes}): {scenePath}");

                EditorSceneManager.OpenScene(scenePath);
                Physics.SyncTransforms();

                // Find points (including inactive ones)
                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include);
                Debug.Log($"[Visual Validator] Points found in scene: {points.Length}");

                if (points.Length == 0) continue;

                GameObject camObj = new GameObject("ValidatorCam_Headless");
                Camera cam = camObj.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 2000f;
                cam.useOcclusionCulling = false;

                foreach (var p in points)
                {
                    if (p == null || !p.gameObject.activeInHierarchy) continue;

                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        cam.transform.position = p.transform.position;
                        cam.transform.rotation = Quaternion.Euler(0, angle, 0);
                        GL.Clear(true, true, Color.black);

                        string baseName = $"{SceneManager.GetActiveScene().name}_{p.pointID}_R{angle}";

                        // Capture Frame A
                        byte[] bytesA = CaptureToBytes(cam);
                        File.WriteAllBytes(Path.Combine(outputDir, baseName + "_FrameA.png"), bytesA);

                        // Save Metadata
                        CaptureMetadata meta = new CaptureMetadata
                        {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = SceneManager.GetActiveScene().name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        // Capture Frame B (Jitter)
                        Vector3 originalPos = cam.transform.position;
                        cam.transform.position += cam.transform.right * 0.0002f;
                        byte[] bytesB = CaptureToBytes(cam);
                        File.WriteAllBytes(Path.Combine(outputDir, baseName + "_FrameB.png"), bytesB);
                        cam.transform.position = originalPos;
                    }
                }
                UnityEngine.Object.DestroyImmediate(camObj);
            }

            Debug.Log("[Visual Validator] BATCH SCAN FINISHED.");
            EditorApplication.Exit(0);
        }

        private static byte[] CaptureToBytes(Camera cam)
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 32, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            return bytes;
        }
    }
}
#endif