using UnityEngine;

public sealed class GridFaultClickTarget : MonoBehaviour
{
    [HideInInspector] public GridMissionLauncher launcher;
    [HideInInspector] public AiesMapTable map;
    [HideInInspector] public float[] points;

    private void Start()
    {
        if (points == null || points.Length < 4)
            return;

        var collider = gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
        for (int i = 0; i < points.Length; i += 2) {
            minX = Mathf.Min(minX, points[i]);
            maxX = Mathf.Max(maxX, points[i]);
            minY = Mathf.Min(minY, points[i + 1]);
            maxY = Mathf.Max(maxY, points[i + 1]);
        }
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float width = map.Width * (maxX - minX) + 0.12f;
        float height = map.Depth * (maxY - minY) + 0.12f;
        collider.center = new Vector3((centerX - 0.5f) * map.Width, 0f, (0.5f - centerY) * map.Depth);
        collider.size = new Vector3(width, 0.14f, height);
    }

    public void Activate()
    {
        if (launcher == null) {
            Debug.LogError("GridSight fault selected but no mission launcher is assigned.", this);
            return;
        }
        Debug.Log("GridSight fault selected: launching drone mission.", this);
        launcher.LaunchDroneMission();
    }

    private void OnMouseDown() => Activate();
}
