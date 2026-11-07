using UnityEngine;

namespace Arkeh3D.Substation138kV
{
    /// <summary>
    /// A conductor (wire or tube) hanging between two points with a given sag, built as a mesh at edit time and at
    /// runtime. Drop it on an empty GameObject, assign the two ends (any Transform; the ConnectionPoint of a kit prefab is
    /// the usual choice) and the sag. The mesh is rebuilt whenever an end moves or a field changes, so it works with
    /// prefabs you place by hand and with objects you spawn or move from code (call <see cref="Rebuild"/> after moving an
    /// end at runtime, or enable <see cref="followEnds"/>).
    /// The curve is the parabolic approximation of the catenary, which is accurate for sags up to about 10 % of the span.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ConductorSpan : MonoBehaviour
    {
        [Tooltip("First end of the conductor (a ConnectionPoint, or any Transform).")]
        public Transform endA;

        [Tooltip("Second end of the conductor.")]
        public Transform endB;

        [Tooltip("Sag at mid-span, in metres (0 = straight). Positive hangs down.")]
        [Min(0f)] public float sag = 0.3f;

        [Tooltip("Conductor radius in metres (0.01 to 0.03 for stranded conductors, 0.03 to 0.06 for tubular busbars).")]
        [Range(0.002f, 0.2f)] public float radius = 0.015f;

        [Tooltip("Segments along the span. 1 gives a straight conductor.")]
        [Range(1, 64)] public int segments = 16;

        [Tooltip("Sides around the conductor.")]
        [Range(3, 24)] public int sides = 8;

        [Tooltip("Rebuild every frame while an end moves (costs a mesh update; leave off for static scenes).")]
        public bool followEnds;

        Mesh mesh;
        Vector3 lastA, lastB;

        /// <summary>Straight-line distance between the ends, in metres.</summary>
        public float Span => endA && endB ? Vector3.Distance(endA.position, endB.position) : 0f;

        /// <summary>Point on the conductor at t in [0, 1] (0 = end A), in world space.</summary>
        public Vector3 PointAt(float t)
        {
            var a = endA.position; var b = endB.position;
            return Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t));
        }

        void OnEnable() { Rebuild(); }
        void OnValidate() { if (isActiveAndEnabled) Rebuild(); }
        void OnDisable() { if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); mesh = null; } }

        void Update()
        {
            if (!followEnds || !endA || !endB) return;
            if (endA.position != lastA || endB.position != lastB) Rebuild();
        }

        /// <summary>Rebuilds the mesh from the current ends and settings.</summary>
        public void Rebuild()
        {
            var mf = GetComponent<MeshFilter>();
            if (!endA || !endB) { if (mf) mf.sharedMesh = null; return; }
            if (mesh == null)
            {
                mesh = new Mesh { name = "ConductorSpan" };
                mesh.hideFlags = HideFlags.DontSave;     // generated: not saved in the scene, rebuilt on load
            }
            lastA = endA.position; lastB = endB.position;
            int n = Mathf.Max(1, segments), s = Mathf.Max(3, sides);
            var verts = new Vector3[(n + 1) * (s + 1)];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[n * s * 6];
            float along = 0f;
            Vector3 prev = PointAt(0f);
            var local = transform.worldToLocalMatrix;
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                var p = PointAt(t);
                var tan = (PointAt(Mathf.Min(1f, t + 0.001f)) - PointAt(Mathf.Max(0f, t - 0.001f))).normalized;
                if (i > 0) along += Vector3.Distance(p, prev);
                prev = p;
                var u = Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var v = Vector3.Cross(tan, u);
                for (int k = 0; k <= s; k++)
                {
                    float ang = 2f * Mathf.PI * k / s;
                    var nrm = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                    int idx = i * (s + 1) + k;
                    verts[idx] = local.MultiplyPoint3x4(p + nrm * radius);
                    norms[idx] = local.MultiplyVector(nrm);
                    uvs[idx] = new Vector2(2f * Mathf.PI * radius * k / s, along);   // UV in metres, like the kit textures
                }
            }
            int q = 0;
            for (int i = 0; i < n; i++)
                for (int k = 0; k < s; k++)
                {
                    int a = i * (s + 1) + k, b = a + s + 1;
                    tris[q++] = a; tris[q++] = b; tris[q++] = a + 1;
                    tris[q++] = a + 1; tris[q++] = b; tris[q++] = b + 1;
                }
            mesh.Clear();
            mesh.vertices = verts; mesh.normals = norms; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

        /// <summary>Creates a conductor between two points, as a child of <paramref name="parent"/>.</summary>
        public static ConductorSpan Create(Transform a, Transform b, Material material, float sag = 0.3f, float radius = 0.015f,
                                           Transform parent = null, string name = null)
        {
            var go = new GameObject(name ?? $"Conductor_{a.name}_{b.name}");
            if (parent) go.transform.SetParent(parent, false);
            var span = go.AddComponent<ConductorSpan>();
            span.endA = a; span.endB = b; span.sag = sag; span.radius = radius;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            span.Rebuild();
            return span;
        }
    }
}
