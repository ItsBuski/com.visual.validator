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

            // Corregido el nombre de la propiedad en Unity 6
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

                        string baseName = $"{scene.name}_{p.pointID}_R{angle}";

                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameA.png"));

                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = scene.name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        cam.transform.position += cam.transform.right * 0.0002f;
                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameB.png"));
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
            cam.allowMSAA = false;

            if (pipeline == "HDRP")
            {
#if VISUAL_VALIDATOR_HDRP
                var hdData = obj.AddComponent<HDAdditionalCameraData>();
                
                // IMPORTANTE: En Unity 6, el tipo de cámara se asigna al componente base Camera
                cam.cameraType = CameraType.Game; 
                
                hdData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
                hdData.volumeLayerMask = -1;

                // Acceso a FrameSettings para Unity 6 / HDRP 17
                // No necesitamos el booleano 'customRenderSettings' si configuramos el mask directamente
                var frameSettings = hdData.renderingPathCustomFrameSettings;
                var mask = hdData.renderingPathCustomFrameSettingsOverrideMask;

                // Activamos explícitamente el Post-proceso en el mask de overrides
                mask.mask[(int)FrameSettingsField.Postprocess] = true;
                frameSettings.SetEnabled(FrameSettingsField.Postprocess, true);
                
                hdData.renderingPathCustomFrameSettings = frameSettings;
                hdData.renderingPathCustomFrameSettingsOverrideMask = mask;
#endif
            }
        }

        private static void ExecuteGPUCapture(Camera cam, string path)
        {
            // Forzamos sRGB y ARGB32 para que HDRP comprima el color correctamente
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            
            cam.Render();

            // Sincronización profesional con AsyncGPUReadback
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
                Debug.LogError($"[Visual Validator] GPU Readback error: {path}");
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            GL.Flush();
        }
    }
}
#endif