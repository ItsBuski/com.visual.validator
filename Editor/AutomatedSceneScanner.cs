#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
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
        // RUTA DE TU ESCENA BASE (Ajusta esto si no es Init.unity)
        private const string BOOTSTRAP_SCENE_PATH = "Assets/Scenes/RuntimeScenes/Demo_Lighting.unity";

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

            // 1. CARGAR ESCENA BASE (Init)
            // Aquí está la iluminación global, el volumen de HDRP y la cámara principal
            if (File.Exists(BOOTSTRAP_SCENE_PATH))
            {
                EditorSceneManager.OpenScene(BOOTSTRAP_SCENE_PATH, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogWarning($"[Visual Validator] No se encontró {BOOTSTRAP_SCENE_PATH}. Revisa la ruta en el script.");
            }

            int totalScenes = SceneManager.sceneCountInBuildSettings;

            for (int i = 0; i < totalScenes; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                
                // Ignoramos la escena base o escenas de menús que no requieran escaneo
                if (path == BOOTSTRAP_SCENE_PATH || path.Contains("MainMenu")) continue;

                // 2. CARGA ADITIVA (Igual que vuestro LevelLoader)
                Scene levelScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(levelScene);
                Physics.SyncTransforms();

                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (points.Length == 0) 
                {
                    // Si la escena no tiene puntos, la cerramos y pasamos a la siguiente
                    EditorSceneManager.CloseScene(levelScene, true);
                    continue;
                }

                Debug.Log($"[Visual Validator] Nivel '{levelScene.name}' cargado. Esperando recursos...");
                await Task.Delay(3000); // Damos tiempo a que carguen texturas y lightmaps

                // 3. SECUESTRO DE LA CÁMARA (Que ahora existirá gracias al Bootstrap)
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

#if VISUAL_VALIDATOR_HDRP
                HDAdditionalCameraData hdData = cam.GetComponent<HDAdditionalCameraData>();
                HDAdditionalCameraData.AntialiasingMode origAA = HDAdditionalCameraData.AntialiasingMode.None;

                if (pipeline == "HDRP" && hdData != null)
                {
                    origAA = hdData.antialiasing;
                    hdData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None; // TAA off
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

                        await Task.Delay(400); // Pausa para estabilizar Auto-Exposure

                        string baseName = $"{levelScene.name}_{p.pointID}_R{angle}";

                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameA.png"), pipeline == "HDRP");

                        CaptureMetadata meta = new CaptureMetadata {
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            scene = levelScene.name,
                            pointID = p.pointID,
                            coordinates = cam.transform.position,
                            rotation = cam.transform.rotation
                        };
                        File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));

                        cam.transform.position += cam.transform.right * 0.0002f;
                        await Task.Delay(100);
                        ExecuteGPUCapture(cam, Path.Combine(outputDir, baseName + "_FrameB.png"), pipeline == "HDRP");
                    }
                }

                // Restaurar cámara
                cam.transform.position = origPos;
                cam.transform.rotation = origRot;
                cam.targetTexture = origTex;
                if (cmBrain != null) cmBrain.enabled = brainState;

#if VISUAL_VALIDATOR_HDRP
                if (pipeline == "HDRP" && hdData != null) hdData.antialiasing = origAA;
#endif
                if (!isHijacked) UnityEngine.Object.DestroyImmediate(fallbackObj);

                // 4. LIMPIEZA ADITIVA: Descargamos el nivel para dejar el Bootstrap limpio
                EditorSceneManager.CloseScene(levelScene, true);
            }

            GraphicsSettings.useScriptableRenderPipelineBatching = srpState;
            Debug.Log("[Visual Validator] Secuencia completada. Cerrando Unity...");
            EditorApplication.Exit(0);
        }

        private static void WarmUpCamera(Camera cam, string pipeline)
        {
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