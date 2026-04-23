#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System;

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
            // Data is stored in ProjectRoot/ValidationCaptures for accessibility
            string projectRoot = Directory.GetCurrentDirectory();
            string outputDir = Path.Combine(projectRoot, "ValidationCaptures");

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            Debug.Log($"[Validator] Headless Scan Started. Target: {outputDir}");

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                EditorSceneManager.OpenScene(scenePath);
                Physics.SyncTransforms();

                // Setup Headless Camera
                GameObject camObj = new GameObject("ValidatorCam_Headless");
                Camera cam = camObj.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 2000f;
                
                // ANTI-GLITCH CONFIGURATION
                cam.useOcclusionCulling = false; // Prevents "flashing" or missing objects
                cam.allowMSAA = false;
                cam.allowDynamicResolution = false;

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Exclude);
                
                foreach (var p in points)
                {
                    if (p == null) continue;
                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        cam.transform.position = p.transform.position;
                        cam.transform.rotation = Quaternion.Euler(0, angle, 0);
                        
                        // Flush GPU buffers to ensure clean material rendering
                        GL.Clear(true, true, Color.black);

                        string baseName = $"{SceneManager.GetActiveScene().name}_{p.pointID}_R{angle}";
                        
                        // Frame A
                        Capture(cam, Path.Combine(outputDir, baseName + "_FrameA.png"));
                        
                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = SceneManager.GetActiveScene().name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        // Frame B (Jitter) for Z-Fighting detection
                        Vector3 originalPos = cam.transform.position;
                        cam.transform.position += cam.transform.right * 0.0002f;
                        Capture(cam, Path.Combine(outputDir, baseName + "_FrameB.png"));
                        cam.transform.position = originalPos;
                    }
                }
                UnityEngine.Object.DestroyImmediate(camObj);
            }
            
            Debug.Log("[Validator] Scan Completed Successfully.");
            EditorApplication.Exit(0);
        }

        private static void Capture(Camera cam, string path)
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 32, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            
            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
#endif