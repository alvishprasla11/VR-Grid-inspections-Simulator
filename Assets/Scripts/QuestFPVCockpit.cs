using UnityEngine;

/// <summary>
/// Keeps the existing camera at the drone's FPV cockpit position.
/// Headset tracking remains a local offset supplied by OpenXR.
/// </summary>
public sealed class QuestFPVCockpit : MonoBehaviour
{
    [SerializeField] private Transform drone;
    [SerializeField] private Vector3 cockpitOffset = new(0f, 0.25f, 0f);

    private void Awake()
    {
        drone ??= FindFirstObjectByType<DroneController>()?.transform;
        if (drone == null)
        {
            Debug.LogError("QuestFPVCockpit could not find the drone.", this);
            enabled = false;
            return;
        }

        transform.SetParent(drone, false);
        transform.localPosition = cockpitOffset;
        transform.localRotation = Quaternion.identity;
    }

}
