using UnityEngine;

namespace Arkeh3D.Substation138kV
{
    /// <summary>
    /// A conductor terminal of a device: where a busbar, cable or jumper connects.
    /// The position is the connection point; <see cref="localDirection"/> points out of the terminal.
    /// Use it to snap conductors between devices from your own tools.
    /// </summary>
    public class ConnectionPoint : MonoBehaviour
    {
        [Tooltip("Direction out of the terminal, in the local space of this transform.")]
        public Vector3 localDirection = Vector3.forward;

        public Vector3 WorldDirection => transform.TransformDirection(localDirection).normalized;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireSphere(transform.position, 0.04f);
            Gizmos.DrawLine(transform.position, transform.position + WorldDirection * 0.25f);
        }
#endif
    }
}
