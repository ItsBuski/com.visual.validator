#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System;

namespace VisualValidator.Editor
{
    public static class AutomatedSceneScanner
    {
        public static void RunHeadlessScan()
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ValidationCaptures");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            int totalScenes = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[Validator] STARTING SCAN. Scenes in Build Settings: {totalScenes}");

            if (totalScenes == 0)
            {
                Debug.LogError("[Validator] ERROR: No scenes found in Build Settings! Add scenes to File > Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            for (int i = 0; i < totalScenes; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                Debug.Log($"[Validator] Opening Scene ({i+1}/{totalScenes}): {scenePath}");
                
                EditorSceneManager.OpenScene(scenePath);
                Physics.SyncTransforms();

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include);
                Debug.Log($"[Validator] Points found in scene: {points.Length}");

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
                        
                        byte[] bytes = CaptureToBytes(cam);
                        File.WriteAllBytes(Path.Combine(outputDir, baseName + "_FrameA.png"), bytes);

                        // Meta
                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = SceneManager.GetActiveScene().name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        // Frame B
                        Vector3 originalPos = cam.transform.position;
                        cam.transform.position += cam.transform.right * 0.0002f;
                        byte[] bytesB = CaptureToBytes(cam);
                        File.WriteAllBytes(Path.Combine(outputDir, baseName + "_FrameB.png"), bytesB);
                        cam.transform.position = originalPos;
                    }
                }
                UnityEngine.Object.DestroyImmediate(camObj);
            }
            
            Debug.Log("[Validator] BATCH SCAN FINISHED.");
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