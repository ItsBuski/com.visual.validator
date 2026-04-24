#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System;
using System.Threading.Tasks; // LIBRERÍA AÑADIDA PARA PAUSAS REALES
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
        // Los métodos de entrada ahora llaman al proceso asíncrono
        public static async void RunStandardScan() => await InternalRunAsync("Standard");
        public static async void RunHDRPScan() => await InternalRunAsync("HDRP");

        private static async Task InternalRunAsync(string pipeline)
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

                // TÁCTICA EMPRESARIAL: Pausa para dejar que la escena pesada cargue
                Debug.Log($"[Visual Validator] Escena '{scene.name}' abierta. Esperando carga de Shaders y Texturas...");
                await Task.Delay(4000); // Espera 4 segundos reales

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

                Vector3 origPos = cam.transform.position;
                Quaternion origRot = cam.transform.rotation;
                RenderTexture origTex = cam.targetTexture;
                
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
                        origAA = hdData.antialiasing;
                        hdData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                        
                        hdData.customRenderingSettings = true; 
                        var frameSettings = hdData.renderingPathCustomFrameSettings;
                        var mask = hdData.renderingPathCustomFrameSettingsOverrideMask;

                        // Aseguramos exposición y renderizado opaco
                        uint[] requiredFields = {
                            (uint)FrameSettingsField.OpaqueObjects,
                            (uint)FrameSettingsField.Postprocess,
                            (uint)FrameSettingsField.ExposureControl
                        };
                        foreach (uint field in requiredFields) {
                            mask.mask[field] = true;
                            frameSettings.SetEnabled((FrameSettingsField)field, true);
                        }
                        hdData.renderingPathCustomFrameSettings = frameSettings;
                        hdData.renderingPathCustomFrameSettingsOverrideMask = mask;
                    }

                    exposureOverrideObj = new GameObject("VisualValidator_ExposureOverride");
                    var overrideVolume = exposureOverrideObj.AddComponent<Volume>();
                    overrideVolume.isGlobal = true;
                    overrideVolume.priority = 10000; 
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    var exposure = profile.Add<Exposure>();
                    exposure.mode.Override(ExposureMode.Fixed);
                    exposure.fixedExposure.Override(11.0f);
                    overrideVolume.profile = profile;
                }
#endif

                foreach (var p in points)
                {
                    if (p == null || !p.gameObject.activeInHierarchy) continue;

                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        cam.transform.position = p.transform.position;
                        cam.transform.rotation = Quaternion.Euler(0, angle, 0);

                        // TÁCTICA EMPRESARIAL: Dejar que la Exposición/Luz se adapte a esta nueva posición
                        await Task.Delay(500); // 0.5 segundos de pausa por cada foto

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
                        await Task.Delay(200); // Pausa corta para la estéreo
                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameB.png"), pipeline == "HDRP");
                    }
                }

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
            Debug.Log("[Visual Validator] Secuencia completada. Cerrando Unity...");
            EditorApplication.Exit(0);
        }

        private static void ExecuteGPUCapture(Camera cam, string path, bool isHDRP)
        {
            RenderTextureFormat format = isHDRP ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, format);
            rt.Create();
            cam.targetTexture = rt;
            
            cam.Render();
            if (isHDRP) cam.Render(); 

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