#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using VisualValidator.Runtime;

namespace VisualValidator.Editor
{
    public static class AutomatedSceneScanner
    {
        public static void RunHeadlessScan()
        {
            bool srpState = GraphicsSettings.useScriptableRenderPipelineBatching;
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            
            // Force dynamic batching off (if supported by the pipeline)
#pragma warning disable 0618
            bool dynamicBatchingState = PlayerSettings.dynamicBatching;
            PlayerSettings.dynamicBatching = false;
#pragma warning restore 0618

            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ValidationCaptures");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            int totalScenes = SceneManager.sceneCountInBuildSettings;

            for (int i = 0; i < totalScenes; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                Physics.SyncTransforms();

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include);
                if (points.Length == 0) continue;

                GameObject camObj = new GameObject("Nuclear_ValidatorCam");
                Camera cam = camObj.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 2000f;
                cam.useOcclusionCulling = false; // Never use culling in automation
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

                        string baseName = $"{scene.name}_{p.pointID}_R{angle}";

                        // We render twice. Pass A warms up the GPU/Shaders, Pass B captures the data.
                        CaptureAndSave(cam, Path.Combine(outputDir, baseName + "_FrameA.png"));

                        // Save Metadata
                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = scene.name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        // Frame B (Jitter)
                        cam.transform.position += cam.transform.right * 0.0002f;
                        CaptureAndSave(cam, Path.Combine(outputDir, baseName + "_FrameB.png"));
                    }
                }
                UnityEngine.Object.DestroyImmediate(camObj);
            }

            // Restore engine settings
            GraphicsSettings.useScriptableRenderPipelineBatching = srpState;
#pragma warning disable 0618
            PlayerSettings.dynamicBatching = dynamicBatchingState;
#pragma warning restore 0618

            EditorApplication.Exit(0);
        }

        private static void CaptureAndSave(Camera cam, string path)
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // This forces Unity to update the internal DrawCall lists and Culling data
            cam.Render(); 
            
            // We clear the buffer and render again to ensure material correctness
            GL.Clear(true, true, Color.black);
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
            
            // Force the GPU to finish all tasks before we move to the next point
            GL.Flush();
        }
    }
}
#endif