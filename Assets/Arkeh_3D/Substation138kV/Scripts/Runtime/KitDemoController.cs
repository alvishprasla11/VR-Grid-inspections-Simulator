using UnityEngine;
#if KIT_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Arkeh3D.Substation138kV
{
    /// <summary>
    /// Demo-scene helper: orbit camera (right mouse drag, wheel to zoom), left click on a moving part to
    /// open/close it, O opens all, C closes all. Works with the Input System package or the legacy Input Manager.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class KitDemoController : MonoBehaviour
    {
        public Vector3 target = new Vector3(0f, 2.5f, 0f);
        public float distance = 18f;
        public float yaw = 35f;
        public float pitch = 15f;
        public float orbitSpeed = 0.25f;
        public float zoomSpeed = 2f;

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            Place();
        }

        void Update()
        {
            if (Pressed(out Vector2 clickPos)) TryToggle(clickPos);
            Vector2 drag = OrbitDelta();
            yaw += drag.x * orbitSpeed;
            pitch = Mathf.Clamp(pitch - drag.y * orbitSpeed, -5f, 85f);
            distance = Mathf.Clamp(distance - Scroll() * zoomSpeed, 2f, 120f);
            if (Key('O')) SetAll(true);
            if (Key('C')) SetAll(false);
            Place();
        }

        void Place()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }

        void TryToggle(Vector2 screenPos)
        {
            RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(screenPos), 500f);
            OperablePart best = null;
            float bestDist = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                OperablePart p = h.collider.GetComponentInParent<OperablePart>();
                if (p != null && h.distance < bestDist) { best = p; bestDist = h.distance; }
            }
            if (best != null) best.Toggle();
        }

        static void SetAll(bool open)
        {
            foreach (OperablePart p in FindObjectsByType<OperablePart>(FindObjectsSortMode.None))
                if (open) p.Open(); else p.Close();
        }

#if KIT_INPUT_SYSTEM
        static bool Pressed(out Vector2 pos)
        {
            pos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
        static Vector2 OrbitDelta() =>
            Mouse.current != null && Mouse.current.rightButton.isPressed ? Mouse.current.delta.ReadValue() : Vector2.zero;
        static float Scroll() => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
        static bool Key(char k)
        {
            if (Keyboard.current == null) return false;
            return k == 'O' ? Keyboard.current.oKey.wasPressedThisFrame : Keyboard.current.cKey.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        static bool Pressed(out Vector2 pos)
        {
            pos = Input.mousePosition;
            return Input.GetMouseButtonDown(0);
        }
        static Vector2 OrbitDelta() =>
            Input.GetMouseButton(1) ? new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f : Vector2.zero;
        static float Scroll() => Input.mouseScrollDelta.y;
        static bool Key(char k) => Input.GetKeyDown(k == 'O' ? KeyCode.O : KeyCode.C);
#else
        static bool Pressed(out Vector2 pos) { pos = Vector2.zero; return false; }
        static Vector2 OrbitDelta() => Vector2.zero;
        static float Scroll() => 0f;
        static bool Key(char k) => false;
#endif
    }
}
