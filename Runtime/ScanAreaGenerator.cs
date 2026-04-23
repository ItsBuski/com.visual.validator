using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [Tooltip("Minimum vertical distance between two points in the same column. Prevents duplicates due to overlapping floors.")]
    public float minVerticalSeparation = 2.0f;

    [Header("Prefab")]
    public GameObject scanPointPrefab;

    [ContextMenu("Generate Multi-Level Points")]
    public void GeneratePoints()
    {
#if UNITY_EDITOR
        if (scanPointPrefab == null)
            scanPointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.nieko.visualvalidator/Runtime/Prefabs/CameraScanPoint.prefab");

        if (scanPointPrefab == null) return;

        Undo.RecordObject(this, "Generate Multi-Level Points");

        float startX = transform.position.x - (areaSize.x / 2f);
        float startZ = transform.position.z - (areaSize.y / 2f);
        int spawnedCount = 0;

        for (float x = 0; x <= areaSize.x; x += spacing)
        {
            for (float z = 0; z <= areaSize.y; z += spacing)
            {
                Vector3 origin = new Vector3(startX + x, transform.position.y + 500f, startZ + z);
                spawnedCount += ProcessVerticalStack(origin);
            }
        }
        Debug.Log($"[Nieko Gen] Finished scan. {spawnedCount} unique points generated.");
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
                float candidateHeight = hit.point.y + minHeightFromGround;
                bool tooClose = false;
                foreach (float h in acceptedHeights)
                {
                    if (Mathf.Abs(candidateHeight - h) < minVerticalSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose && IsPositionSafe(new Vector3(hit.point.x, candidateHeight, hit.point.z)))
                {
                    CreatePoint(new Vector3(hit.point.x, candidateHeight, hit.point.z));
                    acceptedHeights.Add(candidateHeight);
                    count++;
                }
            }
        }
        return count;
    }

    private bool IsPositionSafe(Vector3 pos)
    {
        if (Physics.CheckSphere(pos, clearanceRadius, obstacleLayers)) return false;

        if (Physics.Raycast(pos, Vector3.up, out RaycastHit ceilingHit, minDistanceToCeiling, obstacleLayers)) return false;

        return true;
    }

    private void CreatePoint(Vector3 pos)
    {
#if UNITY_EDITOR
        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(scanPointPrefab);
        obj.transform.position = pos;
        obj.transform.SetParent(this.transform);

        CameraScanPoint pointScript = obj.GetComponent<CameraScanPoint>();
        if (pointScript != null)
        {
            pointScript.Initialize(obstacleLayers, minHeightFromGround);
        }

        Undo.RegisterCreatedObjectUndo(obj, "Create Scan Point");
#endif
    }

    [ContextMenu("Clear Points")]
    public void ClearPoints()
    {
        var children = new List<GameObject>();
        foreach (Transform child in transform) children.Add(child.gameObject);
        foreach (var child in children) Undo.DestroyObjectImmediate(child);
    }
}