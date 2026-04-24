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
        private struct ScanTask
        {
            public CameraScanPoint point;
            public float rotation;
            public string sceneName;
            public bool isFrameB;
        }

        private static Queue<ScanTask> taskQueue = new Queue<ScanTask>();
        private static Camera activeCam;
        private static string currentPipeline;
        private static string outputDir;

        public static void RunStandardScan() => PrepareScan("Standard");
        public static void RunHDRPScan() => PrepareScan("HDRP");

        private static void PrepareScan(string pipeline)
        {
            currentPipeline = pipeline;
            outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ValidationCaptures");
            
            if (Directory.Exists(outputDir))
            {
                foreach (string f in Directory.GetFiles(outputDir)) try { File.Delete(f); } catch { }
            }
            else Directory.CreateDirectory(outputDir);

            GraphicsSettings.useScriptableRenderPipelineBatching = false;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                
                var points = UnityEngine.Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var p in points)
                {
                    for (int r = 0; r < p.directionalShots; r++)
                    {
                        float angle = r * (360f / p.directionalShots);
                        taskQueue.Enqueue(new ScanTask { point = p, rotation = angle, sceneName = scene.name, isFrameB = false });
                        taskQueue.Enqueue(new ScanTask { point = p, rotation = angle, sceneName = scene.name, isFrameB = true });
                    }
                }
            }

            if (taskQueue.Count > 0)
            {
                GameObject camObj = new GameObject("ValidatorCam_Core");
                activeCam = camObj.AddComponent<Camera>();
                SetupCamera(camObj, activeCam, currentPipeline);
                EditorApplication.update += ProcessNextTask;
            }
        }

        private static void ProcessNextTask()
        {
            if (taskQueue.Count == 0)
            {
                EditorApplication.update -= ProcessNextTask;
                if (activeCam != null) UnityEngine.Object.DestroyImmediate(activeCam.gameObject);
                EditorApplication.Exit(0);
                return;
            }

            var task = taskQueue.Dequeue();
            var p = task.point;
            
            activeCam.transform.position = p.transform.position;
            activeCam.transform.rotation = Quaternion.Euler(0, task.rotation, 0);

            if (task.isFrameB) activeCam.transform.position += activeCam.transform.right * 0.0002f;

            string suffix = task.isFrameB ? "_FrameB" : "_FrameA";
            string baseName = $"{task.sceneName}_{p.pointID}_R{task.rotation}";
            string path = Path.Combine(outputDir, baseName + suffix + ".png");

            ExecuteGPUCapture(activeCam, path);

            if (!task.isFrameB)
            {
                CaptureMetadata meta = new CaptureMetadata {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    scene = task.sceneName,
                    pointID = p.pointID,
                    coordinates = activeCam.transform.position,
                    rotation = activeCam.transform.rotation
                };
                File.WriteAllText(Path.Combine(outputDir, baseName + "_Meta.json"), JsonUtility.ToJson(meta, true));
            }
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
                
                // DIFERENCIA 1: Engañamos a HDRP diciéndole que somos la cámara principal del juego.
                hdData.cameraType = HDAdditionalCameraData.CameraType.Game; 
                hdData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
                
                // DIFERENCIA 2: Forzamos el uso de Post-Procesado para que el Tone Mapping se aplique.
                hdData.customRenderSettings = true;
                hdData.bypassPostProcessing = false;
                hdData.volumeLayerMask = -1;
#endif
            }
        }

        private static void ExecuteGPUCapture(Camera cam, string path)
        {
            // DIFERENCIA 3: Obligamos a que el render sea ARGB32 y sRGB. 
            // HDRP hará el ToneMapping sobre este buffer y nos dará píxeles LDR listos para PNG.
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            
            cam.Render();

            // DIFERENCIA 4: En vez de un ReadPixels bloqueante instantáneo, lanzamos una 
            // petición a la GPU y detenemos la ejecución HASTA que los Compute Shaders terminen.
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
                Debug.LogError($"[Visual Validator] GPU Readback failed for {path}");
            }

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            GL.Flush();
        }
    }
}
#endif