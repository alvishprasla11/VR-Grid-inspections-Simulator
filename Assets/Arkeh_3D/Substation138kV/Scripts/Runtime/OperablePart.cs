using UnityEngine;

namespace Arkeh3D.Substation138kV
{
    /// <summary>
    /// A moving part of a substation device (disconnector blade, cabinet door...).
    /// The transform is the pivot: the part rotates around <see cref="localAxis"/> from the closed pose
    /// (0) to <see cref="openAngle"/> degrees (1). Call Open(), Close(), Toggle() or SetOpenAmount() from your code.
    /// </summary>
    [DisallowMultipleComponent]
    public class OperablePart : MonoBehaviour
    {
        [Tooltip("Rotation axis in the local space of this pivot.")]
        public Vector3 localAxis = Vector3.up;

        [Tooltip("Angle in degrees when fully open (sign gives the direction).")]
        public float openAngle = 90f;

        [Tooltip("Rotation speed in degrees per second.")]
        public float speed = 45f;

        [Tooltip("Start the scene with this part open.")]
        public bool startOpen;

        [SerializeField, HideInInspector] Quaternion closedLocalRotation = Quaternion.identity;
        [SerializeField, Range(0f, 1f)] float openAmount;
        float target;

        /// <summary>0 = closed, 1 = open.</summary>
        public float OpenAmount => openAmount;
        public bool IsOpen => target > 0.5f;
        public bool IsMoving => !Mathf.Approximately(openAmount, target);

        void Awake()
        {
            target = startOpen ? 1f : openAmount;
            if (startOpen) SetOpenAmount(1f);
        }

        public void Open() => target = 1f;
        public void Close() => target = 0f;
        public void Toggle() => target = IsOpen ? 0f : 1f;

        /// <summary>Sets the pose immediately (0 = closed, 1 = open), without animation.</summary>
        public void SetOpenAmount(float amount)
        {
            openAmount = target = Mathf.Clamp01(amount);
            Apply();
        }

        /// <summary>Stores the current local rotation as the closed pose (used by the kit build).</summary>
        public void CaptureClosedPose()
        {
            closedLocalRotation = transform.localRotation;
            openAmount = target = 0f;
        }

        void Update()
        {
            if (!IsMoving) return;
            float step = Mathf.Abs(openAngle) > 0.01f ? speed / Mathf.Abs(openAngle) * Time.deltaTime : 1f;
            openAmount = Mathf.MoveTowards(openAmount, target, step);
            Apply();
        }

        void Apply()
        {
            transform.localRotation = closedLocalRotation * Quaternion.AngleAxis(openAngle * openAmount, localAxis);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying) Apply();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 axis = transform.parent ? transform.parent.TransformDirection(closedLocalRotation * localAxis)
                                            : closedLocalRotation * localAxis;
            Gizmos.DrawLine(transform.position - axis * 0.5f, transform.position + axis * 0.5f);
        }
#endif
    }
}
