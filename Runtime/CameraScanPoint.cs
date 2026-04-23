using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

[DisallowMultipleComponent]
public class CameraScanPoint : MonoBehaviour
{
    [Header("ID Configuration")]
    public string pointID;
    [Range(1, 8)] public int directionalShots = 6;

    [Header("Dynamic Grounding (Sync from Generator)")]
    public float minHeightFromGround = 1.6f;
    public LayerMask groundLayer;

    public void Initialize(LayerMask layers, float height)
    {
        this.groundLayer = layers;
        this.minHeightFromGround = height;
        GenerateAutoID();
    }

    [ContextMenu("Regenerate ID")]
    public void GenerateAutoID()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled";

        var allPoints = Object.FindObjectsByType<CameraScanPoint>(FindObjectsInactive.Include);

        int maxIndex = 0;
        string pattern = $@"^{Regex.Escape(sceneName)}_(\d+)$";

        foreach (var p in allPoints)
        {
            if (p == this || string.IsNullOrEmpty(p.pointID)) continue;

            Match m = Regex.Match(p.pointID, pattern);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int idx))
            {
                if (idx > maxIndex) maxIndex = idx;
            }
        }

        pointID = $"{sceneName}_{maxIndex + 1:D2}";
    }

    public void ApplyGroundConstraint()
    {
        if (groundLayer == 0) return;

        if (Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, groundLayer))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + minHeightFromGround, transform.position.z);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawSphere(transform.position, 0.3f);

        if (groundLayer != 0 && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, minHeightFromGround + 0.5f, groundLayer))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, hit.point);
        }
    }
}