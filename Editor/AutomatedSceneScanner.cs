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

#if VISUAL_VALIDATOR_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

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
        // Estos son los nombres que Unity busca al ejecutar el .bat
        public static void RunStandardScan() => InternalRun("Standard");
        public static void RunHDRPScan() => InternalRun("HDRP");

        private static void InternalRun(string pipeline)
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ValidationCaptures");

            if (Directory.Exists(outputDir))
            {
                foreach (string f in Directory.GetFiles(outputDir)) try { File.Delete(f); } catch { }
            }
            else Directory.CreateDirectory(outputDir);

            bool srpState = GraphicsSettings.useScriptableRenderPipelineBatching;
            GraphicsSettings.useScriptableRenderPipelineBatching = false;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                Scene scene = EditorSceneManager.OpenScene(SceneUtility.GetScenePathByBuildIndex(i), OpenSceneMode.Single);
                Physics.SyncTransforms();

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (points.Length == 0) continue;

                GameObject camObj = new GameObject("ValidatorCam_Core");
                Camera cam = camObj.AddComponent<Camera>();
                SetupCamera(camObj, cam, pipeline);

                foreach (var p in points)
                {
                    if (p == null || !p.gameObject.activeInHierarchy) continue;

                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        cam.transform.position = p.transform.position;
                        cam.transform.rotation = Quaternion.Euler(0, angle, 0);

                        ExecuteCapture(cam, Path.Combine(outputDir, $"{scene.name}_{p.pointID}_R{angle}_FrameA.png"), pipeline == "HDRP");
                        
                        cam.transform.position += cam.transform.right * 0.0002f;
                        ExecuteCapture(cam, Path.Combine(outputDir, $"{scene.name}_{p.pointID}_R{angle}_FrameB.png"), pipeline == "HDRP");
                    }
                }
                UnityEngine.Object.DestroyImmediate(camObj);
            }

            GraphicsSettings.useScriptableRenderPipelineBatching = srpState;
            EditorApplication.Exit(0);
        }

        private static void SetupCamera(GameObject obj, Camera cam, string pipeline)
        {
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;

            if (pipeline == "HDRP")
            {
#if VISUAL_VALIDATOR_HDRP
                var hdData = obj.AddComponent<HDAdditionalCameraData>();
                hdData.cameraType = HDAdditionalCameraData.CameraType.Game;
                hdData.volumeLayerMask = -1;
#endif
            }
        }

        private static void ExecuteCapture(Camera cam, string path, bool isHDRP)
        {
            RenderTextureFormat format = isHDRP ? RenderTextureFormat.ARGB32 : RenderTextureFormat.ARGB32;
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, format, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();

            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGB24);
            request.WaitForCompletion();

            if (!request.hasError)
            {
                Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.LoadRawTextureData(request.GetData<byte>());
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
#endif