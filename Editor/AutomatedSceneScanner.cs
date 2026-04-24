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
        public static void RunStandardScan() => InternalRun("Standard");
        public static void RunHDRPScan() => InternalRun("HDRP");

        private static void InternalRun(string pipeline)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string outputDir = Path.Combine(projectRoot, "ValidationCaptures");

            if (Directory.Exists(outputDir))
            {
                foreach (string f in Directory.GetFiles(outputDir)) try { File.Delete(f); } catch { }
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

                // TÁCTICA EMPRESARIAL: Secuestramos tu cámara principal
                Camera cam = Camera.main;
                if (cam == null)
                {
                    var allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    if (allCams.Length > 0) cam = allCams[0];
                }

                bool isHijacked = cam != null;
                GameObject fallbackObj = null;

                if (!isHijacked)
                {
                    fallbackObj = new GameObject("ValidatorCam_Fallback");
                    cam = fallbackObj.AddComponent<Camera>();
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 2000f;
                }

                // Guardamos el estado original para no romper tu proyecto
                Vector3 origPos = cam.transform.position;
                Quaternion origRot = cam.transform.rotation;
                RenderTexture origTex = cam.targetTexture;
                
                // Desactivamos Cinemachine temporalmente
                Behaviour cmBrain = cam.GetComponent("CinemachineBrain") as Behaviour;
                bool brainState = false;
                if (cmBrain != null) { brainState = cmBrain.enabled; cmBrain.enabled = false; }

                GameObject exposureOverrideObj = null;
#if VISUAL_VALIDATOR_HDRP
                HDAdditionalCameraData hdData = cam.GetComponent<HDAdditionalCameraData>();
                HDAdditionalCameraData.AntialiasingMode origAA = HDAdditionalCameraData.AntialiasingMode.None;

                if (pipeline == "HDRP")
                {
                    if (hdData != null)
                    {
                        // Apagamos el TAA porque genera imágenes fantasma al teletransportarnos
                        origAA = hdData.antialiasing;
                        hdData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                    }

                    // INYECCIÓN DE EMERGENCIA: Forzamos la exposición anulando el tiempo congelado del Editor
                    exposureOverrideObj = new GameObject("VisualValidator_ExposureOverride");
                    var overrideVolume = exposureOverrideObj.AddComponent<Volume>();
                    overrideVolume.isGlobal = true;
                    overrideVolume.priority = 10000; // Prioridad Absoluta, sobreescribe toda tu escena
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    var exposure = profile.Add<Exposure>();
                    exposure.mode.Override(ExposureMode.Fixed);
                    exposure.fixedExposure.Override(11.0f); // 11.0f es un valor estándar para interiores iluminados/exteriores
                    overrideVolume.profile = profile;
                }
#endif

                WarmUpCamera(cam, pipeline);

                foreach (var p in points)
                {
                    if (p == null || !p.gameObject.activeInHierarchy) continue;

                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        cam.transform.position = p.transform.position;
                        cam.transform.rotation = Quaternion.Euler(0, angle, 0);

                        string baseName = $"{scene.name}_{p.pointID}_R{angle}";

                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameA.png"), pipeline == "HDRP");

                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = scene.name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        cam.transform.position += cam.transform.right * 0.0002f;
                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameB.png"), pipeline == "HDRP");
                    }
                }

                // Restauramos tu cámara a su estado natural
                cam.transform.position = origPos;
                cam.transform.rotation = origRot;
                cam.targetTexture = origTex;
                if (cmBrain != null) cmBrain.enabled = brainState;

#if VISUAL_VALIDATOR_HDRP
                if (pipeline == "HDRP")
                {
                    if (hdData != null) hdData.antialiasing = origAA;
                    if (exposureOverrideObj != null) UnityEngine.Object.DestroyImmediate(exposureOverrideObj);
                }
#endif
                if (!isHijacked) UnityEngine.Object.DestroyImmediate(fallbackObj);
            }

            GraphicsSettings.useScriptableRenderPipelineBatching = srpState;
            EditorApplication.Exit(0);
        }

        private static void WarmUpCamera(Camera cam, string pipeline)
        {
            // CRÍTICO: HDRP necesita DefaultHDR para no cortar la luz. ARGB32 falla.
            RenderTextureFormat format = pipeline == "HDRP" ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, format);
            rt.Create();
            cam.targetTexture = rt;

            int warmUpFrames = pipeline == "HDRP" ? 8 : 2;
            for (int i = 0; i < warmUpFrames; i++) { cam.Render(); }

            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGB24);
            request.WaitForCompletion();

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            GL.Flush();
        }

        private static void ExecuteGPUCapture(Camera cam, string path, bool isHDRP)
        {
            RenderTextureFormat format = isHDRP ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, format);
            rt.Create();
            cam.targetTexture = rt;
            
            cam.Render();
            if (isHDRP) cam.Render(); // Doble renderizado para asentar el Post-Processing

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
            else
            {
                Debug.LogError($"[Visual Validator] GPU Readback failed: {path}");
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            GL.Flush();
        }
    }
}
#endif