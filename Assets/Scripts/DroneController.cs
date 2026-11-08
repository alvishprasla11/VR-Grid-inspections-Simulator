using UnityEngine;
using UnityEngine.InputSystem;

// This script requires a Rigidbody component on the same GameObject.
[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    // Enum to define the flight modes
    public enum FlightMode { Angle, Horizon, Acro }

    [Header("Current Status")]
    [Tooltip("The current active flight mode.")]
    public FlightMode currentMode = FlightMode.Angle;

    [Header("Base Flight Controls")]
    [Tooltip("The force applied upwards for throttle.")]
    public float thrustForce = 20f; // User's value
    [Tooltip("Torque for rotating left and right (yaw). This is your 'Yaw Rate'.")]
    public float yawTorque = 10f; // User's value
    [Tooltip("NEW: Actively stops yaw rotation when stick is centered. Fixes yaw drift in Angle mode.")]
    public float yawDamping = 5f; // NEW
    [Tooltip("Base torque for pitch in Acro/Horizon modes. This is your 'Pitch Rate'.")]
    public float pitchTorque = 15f; // User's value
    [Tooltip("Base torque for roll in Acro/Horizon modes. This is your 'Roll Rate'.")]
    public float rollTorque = 15f; // User's value

    [Header("Stabilization (Angle/Horizon)")]
    [Tooltip("The maximum angle (in degrees) the drone can tilt in Angle Mode.")]
    public float maxTiltAngle = 30f; // User's value
    [Tooltip("How strongly the drone tries to self-level. (Kp) Your value of 7 was a bit low.")]
    public float stabilizationStrength = 12f; // Reset to a more stable default
    [Tooltip("How much the drone dampens rotation to prevent wobbling. (Kd) Your value of 11 was too high.")]
    public float stabilizationDamping = 3f; // Reset to a more stable default

    [Header("Input Processing")]
    [Tooltip("The size of the 'dead' area in the center of the sticks to prevent drift.")]
    public float inputDeadzone = 0.07f; // User's value
    [Tooltip("The amount of exponential curve for Pitch/Roll/Yaw.")]
    [Range(0, 1)]
    public float acroExpo = 0.5f; // User's value
    [Tooltip("NEW: Flattens the throttle curve around the center to make hovering easier.")]
    [Range(0, 1)]
    public float throttleExpo = 0.4f; // NEW

    [Header("Quest Stability")]
    [Tooltip("Maximum forward/backward and sideways bank angle while moving.")]
    public float questMaxTiltAngle = 18f;
    [Tooltip("How strongly the Quest drone reaches its movement tilt.")]
    public float questTiltStrength = 30f;
    [Tooltip("Limits Quest pitch and roll angular velocity to prevent flips.")]
    public float questMaxAngularVelocity = 4f;
    [Tooltip("Damps all Quest rotation so the drone does not spin or tumble.")]
    public float questRotationDamping = 8f;
    [Tooltip("Horizontal movement force for the stable Quest flight mode.")]
    public float questMoveForce = 12f;
    [Tooltip("Yaw rotation speed for the right Quest stick.")]
    public float questYawTorque = 10f;
    [Tooltip("Damps horizontal drift when the left stick is released.")]
    public float questHorizontalDamping = 5f;
    [Tooltip("Damps vertical speed to hold altitude when triggers are released.")]
    public float questVerticalDamping = 8f;
    [Tooltip("Smooths Quest movement, throttle, and yaw changes to reduce sudden motion.")]
    public float questInputSmoothing = 7f;

    // --- PUBLIC PROPERTIES ---
    public FlightMode CurrentMode => currentMode;
    public float ThrottleInput => throttleInput;
    public Rigidbody Rb => rb;
    // --- END OF PUBLIC PROPERTIES ---

    public void SetQuestInputs(bool armed, float vertical, Vector3 movement, float yaw)
    {
        questControlActive = true;
        questArmed = armed;
        questVerticalInput = Mathf.Clamp(vertical, -1f, 1f);
        throttleInput = questArmed ? questVerticalInput : 0f;
        questMovement = Vector3.ClampMagnitude(movement, 1f);
        questYawInput = ApplyDeadzone(yaw);
        SetRotorRunning(questArmed);
    }

    private void SetRotorRunning(bool running)
    {
        if (rotorAnimators == null)
            return;

        foreach (Animator animator in rotorAnimators)
        {
            if (animator != null)
                animator.enabled = running;
        }
    }

    private void FixLegacyDroneMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("Universal Render Pipeline/Lit shader could not be found.", this);
            return;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.materials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || material.shader == null || material.shader.name != "Standard")
                    continue;

                Color color = material.HasProperty("_Color") ? material.color : Color.white;
                Material replacement = new Material(urpLit)
                {
                    name = $"{material.name} (URP)"
                };
                replacement.SetColor("_BaseColor", color);
                materials[i] = replacement;
                changed = true;
            }

            if (changed)
                renderer.materials = materials;
        }
    }

    // Private variables
    private Rigidbody rb;
    private float throttleInput, yawInput, pitchInput, rollInput;
    private bool questControlActive;
    private bool questArmed;
    private float questVerticalInput;
    private Vector3 questMovement;
    private float questYawInput;
    private float smoothedQuestVerticalInput;
    private Vector3 smoothedQuestMovement;
    private float smoothedQuestYawInput;
    private Animator[] rotorAnimators;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // We set the Rigidbody's angular drag here. 
        // 0.5 is a good value, but we add our own yawDamping on top.
        rb.angularDamping = 0.5f;
        rb.maxAngularVelocity = questMaxAngularVelocity;
        rotorAnimators = GetComponentsInChildren<Animator>(true);
        SetRotorRunning(false);
        FixLegacyDroneMaterials();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (questControlActive)
        {
            HandleQuestStableMovement();
            return;
        }

        // --- Throttle (Processed) ---
        // We now apply throttle expo to make hovering easier
        float processedThrottle = ApplyExpo(throttleInput, throttleExpo);
        rb.AddForce(transform.up * (processedThrottle * thrustForce), ForceMode.Acceleration);

        // --- Yaw (Processed + Damped) ---
        // This is the FIX for your yaw spin in Angle Mode.
        float processedYaw = ApplyExpo(yawInput, acroExpo) * yawTorque;
        
        // Get the current yaw rotation speed
        float currentYawRate = transform.InverseTransformDirection(rb.angularVelocity).y;
        // Apply a damping force to stop rotation when the stick is centered
        float yawDampingTorque = -currentYawRate * yawDamping;

        // Add the user's input torque and the damping torque together
        rb.AddRelativeTorque(Vector3.up * (processedYaw + yawDampingTorque), ForceMode.Acceleration);


        // --- Pitch and Roll (Mode-dependent) ---
        switch (currentMode)
        {
            case FlightMode.Acro:
                HandleAcroMode();
                break;
            case FlightMode.Angle:
                HandleAngleMode();
                break;
            case FlightMode.Horizon:
                HandleHorizonMode();
                break;
        }
    }

    private void HandleQuestStableMovement()
    {
        float smoothing = 1f - Mathf.Exp(-questInputSmoothing * Time.fixedDeltaTime);
        smoothedQuestVerticalInput = Mathf.Lerp(smoothedQuestVerticalInput, questVerticalInput, smoothing);
        smoothedQuestMovement = Vector3.Lerp(smoothedQuestMovement, questMovement, smoothing);
        smoothedQuestYawInput = Mathf.Lerp(smoothedQuestYawInput, questYawInput, smoothing);

        float verticalSpeed = Vector3.Dot(rb.linearVelocity, Vector3.up);
        float verticalForce = Physics.gravity.magnitude + (smoothedQuestVerticalInput * thrustForce)
            - (verticalSpeed * questVerticalDamping);
        rb.AddForce(Vector3.up * verticalForce, ForceMode.Acceleration);

        Vector3 horizontalMovement = Vector3.ProjectOnPlane(smoothedQuestMovement, Vector3.up);
        if (horizontalMovement.sqrMagnitude > 1f)
            horizontalMovement.Normalize();
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        rb.AddForce((horizontalMovement * questMoveForce) - (horizontalVelocity * questHorizontalDamping),
            ForceMode.Acceleration);

        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 dampingTorque = new Vector3(
            -localAngularVelocity.x,
            -localAngularVelocity.y,
            -localAngularVelocity.z) * questRotationDamping;
        Vector3 localMovement = transform.InverseTransformDirection(smoothedQuestMovement);
        float targetPitch = Mathf.Clamp(localMovement.z * questMaxTiltAngle, -questMaxTiltAngle, questMaxTiltAngle);
        float targetRoll = Mathf.Clamp(-localMovement.x * questMaxTiltAngle, -questMaxTiltAngle, questMaxTiltAngle);
        float currentYaw = transform.eulerAngles.y;
        Quaternion targetRotation = Quaternion.Euler(targetPitch, currentYaw, targetRoll);
        Quaternion rotationError = targetRotation * Quaternion.Inverse(transform.rotation);
        rotationError.ToAngleAxis(out float errorAngle, out Vector3 errorAxis);
        if (errorAngle > 180f)
            errorAngle -= 360f;
        Vector3 tiltTorque = transform.InverseTransformDirection(errorAxis * (errorAngle * Mathf.Deg2Rad * questTiltStrength));
        rb.AddRelativeTorque(Vector3.up * (smoothedQuestYawInput * questYawTorque), ForceMode.Acceleration);
        rb.AddRelativeTorque(tiltTorque + dampingTorque, ForceMode.Acceleration);
    }

    /// <summary>
    /// ACRO MODE: Processed stick input controls the rate of rotation.
    /// </summary>
    private void HandleAcroMode()
    {
        // Apply Expo to our deadzoned input
        float processedPitch = ApplyExpo(pitchInput, acroExpo);
        float processedRoll = ApplyExpo(rollInput, acroExpo);

        // Apply torque from *processed* input
        Vector3 acroTorque = new Vector3(processedPitch * pitchTorque, 0, -processedRoll * rollTorque);
        rb.AddRelativeTorque(acroTorque, ForceMode.Acceleration);
    }

    /// <summary>
    /// ANGLE MODE: Raw (but deadzoned) stick input controls the target angle.
    /// </summary>
    private void HandleAngleMode()
    {
        // We use the *raw* (but deadzoned) input here because we want a linear
        // mapping from stick position to target angle.
        float targetPitch = pitchInput * maxTiltAngle;
        float targetRoll = -rollInput * maxTiltAngle;

        Vector3 localEuler = transform.localEulerAngles;
        float currentPitch = ConvertAngle(localEuler.x);
        float currentRoll = ConvertAngle(localEuler.z);

        float pitchError = targetPitch - currentPitch;
        float rollError = targetRoll - currentRoll;

        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        float pitchPD = (pitchError * stabilizationStrength) - (localAngularVel.x * stabilizationDamping);
        float rollPD = (rollError * stabilizationStrength) - (localAngularVel.z * stabilizationDamping);

        rb.AddRelativeTorque(new Vector3(pitchPD, 0, rollPD), ForceMode.Acceleration);
    }

    /// <summary>
    /// HORIZON MODE: Mix of Acro and Angle.
    /// </summary>
    private void HandleHorizonMode()
    {
        // 1. Apply Acro torque (with Expo)
        float processedPitch = ApplyExpo(pitchInput, acroExpo);
        float processedRoll = ApplyExpo(rollInput, acroExpo);
        Vector3 acroTorque = new Vector3(processedPitch * pitchTorque, 0, -processedRoll * rollTorque);
        rb.AddRelativeTorque(acroTorque, ForceMode.Acceleration);

        // 2. Apply P-only stabilization (the "spring" back to center)
        Vector3 localEuler = transform.localEulerAngles;
        float currentPitch = ConvertAngle(localEuler.x);
        float currentRoll = ConvertAngle(localEuler.z);
        Vector3 stabilizingTorque = new Vector3(-currentPitch, 0, -currentRoll) * (stabilizationStrength * 0.5f);
        rb.AddRelativeTorque(stabilizingTorque, ForceMode.Acceleration);
    }

    /// <summary>
    /// Helper to convert Euler angles from 0-360 to -180-180 range.
    /// </summary>
    private float ConvertAngle(float eulerAngle)
    {
        return (eulerAngle > 180) ? eulerAngle - 360 : eulerAngle;
    }

    /// <summary>
    /// Applies a deadzone to the raw input.
    /// </summary>
    private float ApplyDeadzone(float input)
    {
        if (Mathf.Abs(input) < inputDeadzone)
        {
            return 0f;
        }
        // Rescale input to still reach 1.0 at the edge of the deadzone
        return Mathf.Sign(input) * ((Mathf.Abs(input) - inputDeadzone) / (1f - inputDeadzone));
    }

    /// <summary>
    // Applies an exponential curve to the input.
    /// </summary>
    private float ApplyExpo(float input, float expo)
    {
        // Simple cubic expo formula: (expo * input^3) + ((1-expo) * input)
        return (expo * (input * input * input)) + ((1f - expo) * input);
    }

    #region Input Handlers (Called by PlayerInput component)

    public void OnThrottle(InputValue value)
    {
        float rawInput = value.Get<float>();
        // We still remap from [-1, 1] to [0, 1]
        // But the *application* of this value is now handled by HandleMovement
        throttleInput = (rawInput + 1f) / 2f; 
    }

    public void OnYaw(InputValue value)
    {
        // Apply deadzone as soon as we get the input
        yawInput = ApplyDeadzone(value.Get<float>());
    }

    public void OnPitch(InputValue value)
    {
        // Apply deadzone as soon as we get the input
        pitchInput = ApplyDeadzone(value.Get<float>());
    }

    public void OnRoll(InputValue value)
    {
        // Apply deadzone as soon as we get the input
        rollInput = ApplyDeadzone(value.Get<float>());
    }
    
    public void OnFlightModes(InputValue value)
    {
        float rzValue = value.Get<float>();
        if (rzValue < -0.5f)
        {
            currentMode = FlightMode.Acro;
        }
        else if (rzValue > 0.5f)
        {
            currentMode = FlightMode.Angle;
        }
        else
        {
            currentMode = FlightMode.Horizon;
        }
    }

    #endregion
}
