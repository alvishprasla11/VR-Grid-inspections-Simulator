using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Applies the Quest headset's local pose to the drone-parented FPV camera.
/// </summary>
public sealed class QuestHeadTracking : MonoBehaviour
{
    private InputAction position;
    private InputAction rotation;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 initialHeadPosition;
    private Quaternion initialHeadRotation;
    private bool calibrated;

    private void Awake()
    {
        position = new InputAction("QuestHeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition");
        rotation = new InputAction("QuestHeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
    }

    private void OnEnable()
    {
        position.Enable();
        rotation.Enable();
    }

    private void OnDisable()
    {
        position.Disable();
        rotation.Disable();
    }

    private void Update()
    {
        if (position.activeControl == null || rotation.activeControl == null)
            return;

        Vector3 headPosition = position.ReadValue<Vector3>();
        Quaternion headRotation = rotation.ReadValue<Quaternion>();
        if (!calibrated)
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            initialHeadPosition = headPosition;
            initialHeadRotation = headRotation;
            calibrated = true;
        }

        transform.localPosition = basePosition + (headPosition - initialHeadPosition);
        transform.localRotation = baseRotation * (Quaternion.Inverse(initialHeadRotation) * headRotation);
    }
}
