using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisualValidator.Runtime
{
    public class ScanAreaGenerator : MonoBehaviour
    {
        [Header("Grid Configuration")]
        public Vector2 areaSize = new Vector2(50, 50);
        public float spacing = 5f;

        [Header("Environment Layers")]
        public LayerMask obstacleLayers;

        [Header("Constraints")]
        public float minHeightFromGround = 1.6f;
        public float minDistanceToCeiling = 1.0f;
        public float clearanceRadius = 0.4f;

        [Header("Safety Filter")]
        public float minVerticalSeparation = 2.0f;

        [Header("Prefab")]
        public GameObject scanPointPrefab;

        [Header("Gizmos Settings")]
        public Color areaColor = new Color(0f, 1f, 0.5f, 0.2f);

        [ContextMenu("Generate Multi-Level Points")]
        public void GeneratePoints()
        {
#if UNITY_EDITOR
            if (scanPointPrefab == null)
                scanPointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.visual.validator/Runtime/Prefabs/CameraScanPoint.prefab");

            if (scanPointPrefab == null) return;

            Undo.RecordObject(this, "Generate Points");

            float startX = transform.position.x - (areaSize.x / 2f);
            float startZ = transform.position.z - (areaSize.y / 2f);
            int count = 0;

            for (float x = 0; x <= areaSize.x; x += spacing)
            {
                for (float z = 0; z <= areaSize.y; z += spacing)
                {
                    Vector3 origin = new Vector3(startX + x, transform.position.y + 500f, startZ + z);
                    count += ProcessVerticalStack(origin);
                }
            }
            Debug.Log($"[Visual Validator] Generated {count} points.");
#endif
        }

        private int ProcessVerticalStack(Vector3 origin)
        {
            int count = 0;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 1000f, obstacleLayers);
            var sortedHits = hits.OrderByDescending(h => h.point.y).ToList();
            List<float> acceptedHeights = new List<float>();

            foreach (var hit in sortedHits)
            {
                if (Vector3.Dot(hit.normal, Vector3.up) > 0.5f)
                {
                    float targetY = hit.point.y + minHeightFromGround;
                    bool tooClose = acceptedHeights.Any(h => Mathf.Abs(targetY - h) < minVerticalSeparation);

                    if (!tooClose && IsPositionSafe(new Vector3(hit.point.x, targetY, hit.point.z)))
                    {
                        CreatePoint(new Vector3(hit.point.x, targetY, hit.point.z));
                        acceptedHeights.Add(targetY);
                        count++;
                    }
                }
            }
            return count;
        }

        private bool IsPositionSafe(Vector3 pos)
        {
            if (Physics.CheckSphere(pos, clearanceRadius, obstacleLayers)) return false;
            if (Physics.Raycast(pos, Vector3.up, out _, minDistanceToCeiling, obstacleLayers)) return false;
            return true;
        }

        private void CreatePoint(Vector3 pos)
        {
#if UNITY_EDITOR
            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(scanPointPrefab);
            obj.transform.position = pos;
            obj.transform.SetParent(this.transform);
            
            var script = obj.GetComponent<CameraScanPoint>();
            if (script != null) script.Initialize(obstacleLayers, minHeightFromGround);

            Undo.RegisterCreatedObjectUndo(obj, "Create Point");
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = areaColor;
            Gizmos.DrawCube(Vector3.zero, new Vector3(areaSize.x, 0.1f, areaSize.y));
            Gizmos.color = new Color(areaColor.r, areaColor.g, areaColor.b, 1f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, 0.1f, areaSize.y));
        }
    }
}