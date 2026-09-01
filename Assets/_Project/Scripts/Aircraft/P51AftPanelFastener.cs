using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51AftPanelFastener : MonoBehaviour
    {
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
                // The normal P-51 aft panel is a curved mesh. If a future replacement panel has no
                // mesh, preserve its authored transform rather than guessing at a panel axis.
                CaptureCurrentRotation();
                return;
            }

            // Match the canonical layout used by Step 92: four captive fasteners near the curved
            // panel corners, each sampled directly from the actual exterior skin. Using the mesh
            // instead of a BoxCollider avoids the axis assumption that could throw the fasteners
            // away from the fuselage on a curved/rotated panel.
            Bounds bounds = mesh.bounds;
            float topY = bounds.max.y - Mathf.Min(0.07f, bounds.size.y * 0.10f);
            float bottomY = bounds.min.y + Mathf.Min(0.07f, bounds.size.y * 0.10f);
            float frontZ = bounds.max.z - Mathf.Min(0.08f, bounds.size.z * 0.08f);
            float rearZ = bounds.min.z + Mathf.Min(0.08f, bounds.size.z * 0.08f);

            int normalizedIndex = Mathf.Clamp(fastenerIndex, 0, 3);
            float targetY = normalizedIndex < 2 ? topY : bottomY;
            float targetZ = (normalizedIndex & 1) == 0 ? rearZ : frontZ;

            FindOuterSurfacePoint(
                mesh.vertices,
                mesh.normals,
                targetY,
                targetZ,
                out Vector3 panelLocalPosition,
                out Vector3 panelLocalNormal);

            panelLocalPosition += panelLocalNormal * 0.014f;
            Quaternion panelLocalRotation = Quaternion.FromToRotation(Vector3.up, panelLocalNormal);

            Vector3 worldPosition = panel.transform.TransformPoint(panelLocalPosition);
            Quaternion worldRotation = panel.transform.rotation * panelLocalRotation;
            transform.position = worldPosition;

            securedRotation = transform.parent != null
                ? Quaternion.Inverse(transform.parent.rotation) * worldRotation
                : worldRotation;
            rotationCaptured = true;
        }

        private static void FindOuterSurfacePoint(
            Vector3[] vertices,
            Vector3[] normals,
            float targetY,
            float targetZ,
            out Vector3 position,
            out Vector3 normal)
        {
            int best = 0;
            float bestScore = float.PositiveInfinity;
            bool hasNormals = normals != null && normals.Length == vertices.Length;

            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 candidateNormal = hasNormals ? normals[index] : Vector3.left;
                float outwardPenalty = candidateNormal.x < -0.12f ? 0f : 2.0f;
                float dy = vertices[index].y - targetY;
                float dz = vertices[index].z - targetZ;
                float score = dy * dy + dz * dz + outwardPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = index;
                }
            }

            position = vertices[best];
            normal = hasNormals && normals[best].sqrMagnitude > 0.0001f
                ? normals[best].normalized
                : Vector3.left;

            // The aft access panel's exterior is on the negative local-X side. Keep the sampled
            // fastener normal pointing out of the fuselage, just like the original Step 92 builder.
            if (normal.x > 0f)
            {
                normal = -normal;
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

            // The Unity cylinder's local Y axis is its shaft. The canonical mount rotation maps
            // that Y axis to the sampled panel normal, so the release animation twists the fastener
            // in place rather than tipping it away from the skin.
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
