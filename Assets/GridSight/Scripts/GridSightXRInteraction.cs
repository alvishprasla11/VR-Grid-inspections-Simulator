using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public sealed class GridSightXRInteraction : MonoBehaviour
{
    [SerializeField] private float rayLength = 6f;
    [SerializeField] private LayerMask rayMask = Physics.DefaultRaycastLayers;

    private InputAction controllerPosition;
    private InputAction controllerRotation;
    private InputAction trigger;
    private bool previousTrigger;
    private LineRenderer ray;
    private Transform trackingOrigin;
    private void Awake()
    {
        controllerPosition = new InputAction("GridSightControllerPosition", InputActionType.Value, "<XRController>{RightHand}/devicePosition");
        controllerPosition.AddBinding("<OculusTouchController>{RightHand}/devicePosition");
        controllerRotation = new InputAction("GridSightControllerRotation", InputActionType.Value, "<XRController>{RightHand}/deviceRotation");
        controllerRotation.AddBinding("<OculusTouchController>{RightHand}/deviceRotation");
        trigger = new InputAction("GridSightControllerTrigger", InputActionType.Button, "<XRController>{RightHand}/trigger");
        trigger.AddBinding("<XRController>{RightHand}/triggerPressed");
        trigger.AddBinding("<OculusTouchController>{RightHand}/trigger");
        trigger.AddBinding("<OculusTouchController>{RightHand}/triggerPressed");

        ray = gameObject.AddComponent<LineRenderer>();
        ray.useWorldSpace = true;
        ray.positionCount = 2;
        ray.widthMultiplier = 0.002f;
        ray.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.2f, 0.8f, 1f, 0.55f) };
        var origin = new GameObject("GridSightTrackingOrigin");
        trackingOrigin = origin.transform;
        trackingOrigin.SetPositionAndRotation(transform.position, transform.rotation);
        transform.SetParent(trackingOrigin, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        var poseDriver = GetComponent<TrackedPoseDriver>() ?? gameObject.AddComponent<TrackedPoseDriver>();
        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        poseDriver.positionAction = new InputAction("GridSightHeadPosition", expectedControlType: "Vector3");
        poseDriver.positionAction.AddBinding("<XRHMD>/centerEyePosition");
        poseDriver.rotationAction = new InputAction("GridSightHeadRotation", expectedControlType: "Quaternion");
        poseDriver.rotationAction.AddBinding("<XRHMD>/centerEyeRotation");
    }

    private void OnEnable()
    {
        controllerPosition.Enable();
        controllerRotation.Enable();
        trigger.Enable();
    }

    private void OnDisable()
    {
        controllerPosition.Disable();
        controllerRotation.Disable();
        trigger.Disable();
    }

    private void Update()
    {
        if (controllerPosition.activeControl == null || controllerRotation.activeControl == null)
        {
            ray.enabled = false;
            previousTrigger = false;
            return;
        }

        Vector3 origin = trackingOrigin.TransformPoint(controllerPosition.ReadValue<Vector3>());
        Quaternion rotationValue = trackingOrigin.rotation * controllerRotation.ReadValue<Quaternion>();
        ray.enabled = true;
        ray.SetPosition(0, origin);
        ray.SetPosition(1, origin + rotationValue * Vector3.forward * rayLength);

        bool pressed = trigger.IsPressed();
        if (pressed && !previousTrigger)
        {
            var hits = Physics.SphereCastAll(origin, 0.05f, rotationValue * Vector3.forward, rayLength, rayMask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<GridFaultClickTarget>();
                if (target != null)
                {
                    target.Activate();
                    break;
                }
            }
        }
        previousTrigger = pressed;
    }

    private void OnDestroy()
    {
        if (trackingOrigin != null)
            Destroy(trackingOrigin.gameObject);
        if (ray != null && ray.material != null)
            Destroy(ray.material);
    }
}
