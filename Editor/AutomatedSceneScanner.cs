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
            // 1. GLOBAL SRP BATCHER SAFETY
            // We disable the SRP Batcher to prevent material "ghosting" or buffer reuse 
            // during rapid camera teleportation.
            bool initialSRPState = GraphicsSettings.useScriptableRenderPipelineBatching;
            GraphicsSettings.useScriptableRenderPipelineBatching = false;

            string projectRoot = Directory.GetCurrentDirectory();
            string outputDir = Path.Combine(projectRoot, "ValidationCaptures");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            int totalScenes = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[Visual Validator] Starting Scan. SRP Batcher disabled for safety.");

            for (int i = 0; i < totalScenes; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                
                Physics.SyncTransforms();

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include);
                if (points.Length == 0) continue;

                GameObject camObj = new GameObject("ValidatorCam_Headless");
                Camera cam = camObj.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 2000f;
                
                // Ensure no post-processing or culling interference
                cam.useOcclusionCulling = false;
                cam.allowMSAA = false;
                cam.allowDynamicResolution = false;

                foreach (var p in points)
                {
                    if (p == null || !p.gameObject.activeInHierarchy) continue;

                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        cam.transform.position = p.transform.position;
                        cam.transform.rotation = Quaternion.Euler(0, angle, 0);
                        
                        // Force a clean GPU clear before rendering
                        GL.Clear(true, true, Color.black);

                        string baseName = $"{scene.name}_{p.pointID}_R{angle}";

                        // Capture Frame A
                        File.WriteAllBytes(Path.Combine(outputDir, baseName + "_FrameA.png"), CaptureToBytes(cam));

                        // Save Metadata
                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = scene.name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        // Capture Frame B (Jitter)
                        Vector3 originalPos = cam.transform.position;
                        cam.transform.position += cam.transform.right * 0.0002f;
                        File.WriteAllBytes(Path.Combine(outputDir, baseName + "_FrameB.png"), CaptureToBytes(cam));
                        cam.transform.position = originalPos;
                    }
                }
                UnityEngine.Object.DestroyImmediate(camObj);
            }
            
            // Restore SRP Batcher state
            GraphicsSettings.useScriptableRenderPipelineBatching = initialSRPState;
            
            Debug.Log("[Visual Validator] Scan completed. SRP Batcher state restored.");
            EditorApplication.Exit(0);
        }

        private static byte[] CaptureToBytes(Camera cam)
        {
            // Use 24-bit depth for high precision
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            
            // Force the camera to render immediately
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