using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51AftPanelFastener : MonoBehaviour
    {
        private const float MinimumStrongOutwardX = -0.35f;
        private const float SurfaceStandOff = 0.024f;

        [SerializeField] private P51AftAccessPanel panel;
        [SerializeField] private int fastenerIndex;
        [SerializeField] private bool secured = true;
        [SerializeField, Min(90f)] private float turnSpeedDegreesPerSecond = 540f;

        private Quaternion securedRotation;
        private bool rotationCaptured;
        private Quaternion targetRotation;

        public P51AftAccessPanel Panel => panel;
        public int FastenerIndex => fastenerIndex;
        public bool IsSecured => secured;

        public void Configure(P51AftAccessPanel configuredPanel, int configuredIndex, bool startsSecured)
        {
            panel = configuredPanel;
            fastenerIndex = Mathf.Clamp(configuredIndex, 0, 3);
            secured = startsSecured;
            NormalizeMountTransform();
            RefreshTarget(true);
        }

        public bool TryToggle(out string message)
        {
            message = string.Empty;
            if (panel == null)
            {
                message = "This aft-panel fastener is not connected to a panel.";
                return false;
            }
            if (!panel.IsInstalled)
            {
                message = "Reinstall the aft access panel before working its fasteners.";
                return false;
            }

            secured = !secured;
            RefreshTarget(false);
            int remaining = panel.SecuredFastenerCount;
            message = secured
                ? $"Secured aft-panel fastener {fastenerIndex + 1}. {remaining} fastener{(remaining == 1 ? string.Empty : "s")} secured."
                : remaining > 0
                    ? $"Released aft-panel fastener {fastenerIndex + 1}. {remaining} still secured."
                    : "All aft-panel fasteners released. The access panel can now be removed.";
            return true;
        }

        private void Awake()
        {
            ResolvePanel();
            NormalizeMountTransform();
            RefreshTarget(true);
        }

        private void OnEnable()
        {
            ResolvePanel();
            NormalizeMountTransform();
            RefreshTarget(true);
        }

        private void Update()
        {
            if (!rotationCaptured)
            {
                NormalizeMountTransform();
                RefreshTarget(true);
                return;
            }

            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                targetRotation,
                turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void ResolvePanel()
        {
            if (panel == null)
            {
                panel = GetComponentInParent<P51AftAccessPanel>();
            }
        }

        private void NormalizeMountTransform()
        {
            ResolvePanel();
            if (panel == null)
            {
                CaptureCurrentRotation();
                return;
            }

            MeshFilter filter = panel.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0)
            {
                CaptureCurrentRotation();
                return;
            }

            Bounds bounds = mesh.bounds;
            float topY = bounds.max.y - Mathf.Min(0.07f, bounds.size.y * 0.10f);
            float bottomY = bounds.min.y + Mathf.Min(0.07f, bounds.size.y * 0.10f);
            float frontZ = bounds.max.z - Mathf.Min(0.08f, bounds.size.z * 0.08f);
            float rearZ = bounds.min.z + Mathf.Min(0.08f, bounds.size.z * 0.08f);

            int normalizedIndex = Mathf.Clamp(fastenerIndex, 0, 3);
            float targetY = normalizedIndex < 2 ? topY : bottomY;
            float targetZ = (normalizedIndex & 1) == 0 ? rearZ : frontZ;

            FindStableOuterSurfacePoint(
                mesh.vertices,
                mesh.normals,
                bounds,
                targetY,
                targetZ,
                out Vector3 panelLocalPosition,
                out Vector3 panelLocalNormal);

            // Keep the head fully proud of the curved skin. Step 94 makes the service fasteners
            // slightly larger for visibility, so the old 14 mm offset could leave the head visibly
            // buried even when the center point itself was technically outside the mesh.
            panelLocalPosition += panelLocalNormal * SurfaceStandOff;
            Quaternion panelLocalRotation = Quaternion.FromToRotation(Vector3.up, panelLocalNormal);

            Vector3 worldPosition = panel.transform.TransformPoint(panelLocalPosition);
            Quaternion worldRotation = panel.transform.rotation * panelLocalRotation;
            transform.position = worldPosition;

            securedRotation = transform.parent != null
                ? Quaternion.Inverse(transform.parent.rotation) * worldRotation
                : worldRotation;
            rotationCaptured = true;
        }

        private static void FindStableOuterSurfacePoint(
            Vector3[] vertices,
            Vector3[] normals,
            Bounds bounds,
            float targetY,
            float targetZ,
            out Vector3 position,
            out Vector3 normal)
        {
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            int best = -1;
            float bestScore = float.PositiveInfinity;

            // First pass deliberately ignores edge/tangent vertices. The exterior of this panel is
            // on negative local X, so a useful mounting patch must have a meaningful negative-X
            // component instead of a normal that points mostly up/down or fore/aft.
            if (hasNormals)
            {
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 candidateNormal = normals[index];
                    if (candidateNormal.sqrMagnitude < 0.0001f)
                    {
                        continue;
                    }

                    candidateNormal.Normalize();
                    if (candidateNormal.x > MinimumStrongOutwardX)
                    {
                        continue;
                    }

                    float dy = vertices[index].y - targetY;
                    float dz = vertices[index].z - targetZ;
                    float score = dy * dy + dz * dz;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = index;
                    }
                }
            }

            // Fallback for a future mesh whose imported normals are missing/unusual: choose the
            // closest Y/Z vertex on the most-negative-X side of the mesh rather than accepting an
            // arbitrary inward face.
            if (best < 0)
            {
                float exteriorBand = bounds.min.x + Mathf.Max(0.01f, bounds.size.x * 0.30f);
                for (int index = 0; index < vertices.Length; index++)
                {
                    if (vertices[index].x > exteriorBand)
                    {
                        continue;
                    }

                    float dy = vertices[index].y - targetY;
                    float dz = vertices[index].z - targetZ;
                    float score = dy * dy + dz * dz;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = index;
                    }
                }
            }

            if (best < 0)
            {
                best = 0;
            }

            position = vertices[best];

            // Smooth the normal using nearby outward-facing vertices. This prevents one seam or
            // corner vertex from tipping an otherwise correctly positioned fastener into the skin.
            Vector3 accumulatedNormal = Vector3.zero;
            float normalWeight = 0f;
            float patchY = Mathf.Max(0.04f, bounds.size.y * 0.16f);
            float patchZ = Mathf.Max(0.04f, bounds.size.z * 0.12f);

            if (hasNormals)
            {
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 candidateNormal = normals[index];
                    if (candidateNormal.sqrMagnitude < 0.0001f)
                    {
                        continue;
                    }

                    candidateNormal.Normalize();
                    if (candidateNormal.x > MinimumStrongOutwardX)
                    {
                        continue;
                    }

                    float dy = Mathf.Abs(vertices[index].y - position.y);
                    float dz = Mathf.Abs(vertices[index].z - position.z);
                    if (dy > patchY || dz > patchZ)
                    {
                        continue;
                    }

                    float weight = 1f / (0.01f + dy + dz);
                    accumulatedNormal += candidateNormal * weight;
                    normalWeight += weight;
                }
            }

            normal = normalWeight > 0f && accumulatedNormal.sqrMagnitude > 0.0001f
                ? accumulatedNormal.normalized
                : Vector3.left;

            if (normal.x > 0f)
            {
                normal = -normal;
            }

            // Final safety clamp: even after smoothing, do not allow a nearly tangent normal to
            // aim the cylinder shaft sideways through the aircraft skin.
            if (normal.x > MinimumStrongOutwardX)
            {
                normal.x = MinimumStrongOutwardX;
                normal.Normalize();
            }
        }

        private void CaptureCurrentRotation()
        {
            securedRotation = transform.localRotation;
            rotationCaptured = true;
        }

        private void RefreshTarget(bool snap)
        {
            if (!rotationCaptured)
            {
                NormalizeMountTransform();
            }

            targetRotation = secured
                ? securedRotation
                : securedRotation * Quaternion.AngleAxis(90f, Vector3.up);
            if (snap)
            {
                transform.localRotation = targetRotation;
            }
        }

        private void OnValidate()
        {
            turnSpeedDegreesPerSecond = Mathf.Max(90f, turnSpeedDegreesPerSecond);
            fastenerIndex = Mathf.Clamp(fastenerIndex, 0, 3);
            ResolvePanel();
            if (panel != null)
            {
                NormalizeMountTransform();
                RefreshTarget(true);
            }
        }
    }
}
