using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maps Quest controller triggers and thumbsticks to the drone.
/// Triggers control vertical thrust, the left stick moves relative to the drone,
/// and the right stick rotates the drone in place.
/// </summary>
public sealed class QuestDroneInput : MonoBehaviour
{
    [SerializeField] private DroneController drone;
    [SerializeField] private float deadzone = 0.1f;
    [SerializeField] private float throttleRate = 1f;

    private InputAction leftStick;
    private InputAction rightStick;
    private InputAction leftTrigger;
    private InputAction rightTrigger;
    private InputAction armButton;
    private bool armed;
    private bool previousArmButton;

    private void Awake()
    {
        drone ??= GetComponent<DroneController>() ?? FindFirstObjectByType<DroneController>();
        leftStick = new InputAction("QuestLeftStick", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
        rightStick = new InputAction("QuestRightStick", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        leftTrigger = new InputAction("QuestLeftTrigger", InputActionType.Value, "<XRController>{LeftHand}/trigger");
        rightTrigger = new InputAction("QuestRightTrigger", InputActionType.Value, "<XRController>{RightHand}/trigger");
        armButton = new InputAction("QuestArm", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
    }

    private void OnEnable()
    {
        leftStick.Enable();
        rightStick.Enable();
        leftTrigger.Enable();
        rightTrigger.Enable();
        armButton.Enable();
    }

    private void OnDisable()
    {
        leftStick.Disable();
        rightStick.Disable();
        leftTrigger.Disable();
        rightTrigger.Disable();
        armButton.Disable();
    }

    private void Update()
    {
        if (drone == null)
        {
            Debug.LogError("QuestDroneInput requires a DroneController reference.", this);
            enabled = false;
            return;
        }

        Vector2 left = ApplyDeadzone(leftStick.ReadValue<Vector2>());
        Vector2 right = ApplyDeadzone(rightStick.ReadValue<Vector2>());
        bool armPressed = armButton.IsPressed();
        if (armPressed && !previousArmButton && IsThrottleLow())
            armed = !armed;
        previousArmButton = armPressed;

        float throttle = (rightTrigger.ReadValue<float>() - leftTrigger.ReadValue<float>()) * throttleRate;
        Vector3 movement = new Vector3(left.x, 0f, left.y);
        Vector3 worldMovement = (drone.transform.right * movement.x) + (drone.transform.forward * movement.z);

        drone.SetQuestInputs(
            armed,
            armed ? Mathf.Clamp(throttle, -1f, 1f) : 0f,
            armed ? worldMovement : Vector3.zero,
            armed ? right.x : 0f);
    }

    private bool IsThrottleLow()
    {
        return rightTrigger.ReadValue<float>() < 0.05f && leftTrigger.ReadValue<float>() < 0.05f;
    }

    private Vector2 ApplyDeadzone(Vector2 value)
    {
        if (value.magnitude <= deadzone)
            return Vector2.zero;

        float magnitude = Mathf.InverseLerp(deadzone, 1f, value.magnitude);
        return value.normalized * magnitude;
    }
}
