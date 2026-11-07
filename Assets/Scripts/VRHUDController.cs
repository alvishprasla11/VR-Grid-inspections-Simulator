using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Places the flight OSD as a fixed world-space HUD in front of the Quest camera.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class VRHUDController : MonoBehaviour
{
    [SerializeField] private Vector3 localPosition = new(0f, 0f, 1.4f);
    [SerializeField] private Vector3 localScale = new(0.0015f, 0.0015f, 0.0015f);

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("VRHUDController could not find the Main Camera.", this);
            enabled = false;
            return;
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        RectTransform rectTransform = (RectTransform)transform;
        rectTransform.SetParent(camera.transform, false);
        rectTransform.localPosition = localPosition;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = localScale;
        rectTransform.sizeDelta = new Vector2(800f, 600f);
        canvas.sortingOrder = 10;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
    }
}
