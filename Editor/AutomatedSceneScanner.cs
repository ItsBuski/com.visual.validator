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

#if UNITY_PIPELINE_HDRP || VISUAL_VALIDATOR_HDRP
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
        public static void RunStandardScan() => InternalRun("Standard");
        public static void RunHDRPScan() => InternalRun("HDRP");

        private static void InternalRun(string pipeline)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string outputDir = Path.Combine(projectRoot, "ValidationCaptures");

            if (Directory.Exists(outputDir))
            {
                foreach (string f in Directory.GetFiles(outputDir)) try { File.Delete(f); } catch {}
            }
            else Directory.CreateDirectory(outputDir);

            bool srpState = GraphicsSettings.useScriptableRenderPipelineBatching;
            GraphicsSettings.useScriptableRenderPipelineBatching = false;

            int totalScenes = SceneManager.sceneCountInBuildSettings;

            for (int i = 0; i < totalScenes; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                Physics.SyncTransforms();

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (points.Length == 0) continue;

                GameObject camObj = new GameObject("ValidatorCam_Headless");
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

                        string baseName = $"{scene.name}_{p.pointID}_R{angle}";

                        CaptureAndSave(cam, Path.Combine(outputDir, baseName + "_FrameA.png"), pipeline == "HDRP");

                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = scene.name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        cam.transform.position += cam.transform.right * 0.0002f;
                        CaptureAndSave(cam, Path.Combine(outputDir, baseName + "_FrameB.png"), pipeline == "HDRP");
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
            cam.useOcclusionCulling = false;
            cam.allowMSAA = false;

            if (pipeline == "HDRP")
            {
#if UNITY_PIPELINE_HDRP || VISUAL_VALIDATOR_HDRP
                var hdData = obj.AddComponent<HDAdditionalCameraData>();
                hdData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                hdData.backgroundColorHDR = Color.black;

                var volObj = new GameObject("HDRP_Exposure_Fix");
                volObj.transform.SetParent(obj.transform);
                var volume = volObj.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100;
                
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var exposure = profile.Add<Exposure>();
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(13.0f);
                volume.profile = profile;
#endif
            }
        }

        private static void CaptureAndSave(Camera cam, string path, bool isHDRP)
        {
            RenderTextureFormat format = isHDRP ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, format);
            cam.targetTexture = rt;
            cam.Render(); 
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
            GL.Flush();
        }
    }
}
#endif