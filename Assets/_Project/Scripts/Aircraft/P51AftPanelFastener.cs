using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51AftPanelFastener : MonoBehaviour
    {
        private const float MinimumStrongOutwardX = -0.35f;
        private const float SurfaceStandOff = 0.024f;
        private const float BottomRowInsetFraction = 0.24f;
        private const float BottomRowInsetMaximum = 0.16f;
        private const int CurvedLowerFastenerIndex = 2;

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
            float bottomInset = Mathf.Min(BottomRowInsetMaximum, bounds.size.y * BottomRowInsetFraction);
            float bottomY = bounds.min.y + bottomInset;
            float frontZ = bounds.max.z - Mathf.Min(0.08f, bounds.size.z * 0.08f);
            float rearZ = bounds.min.z + Mathf.Min(0.08f, bounds.size.z * 0.08f);

            int normalizedIndex = Mathf.Clamp(fastenerIndex, 0, 3);
            float targetY = normalizedIndex < 2 ? topY : bottomY;
            float targetZ = (normalizedIndex & 1) == 0 ? rearZ : frontZ;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            FindStableOuterSurfacePoint(
                vertices,
                normals,
                bounds,
                targetY,
                targetZ,
                out Vector3 panelLocalPosition,
                out Vector3 panelLocalNormal);

            // Fastener 3 is on the lower curved transition. Its shaft should follow the real
            // curved-skin normal instead of being forced toward panel-local -X. The -X clamp is
            // what produced the roughly 90-degree sideways rotation visible in the Inspector.
            // Keep the established position and correct only this fastener's mount direction.
            if (normalizedIndex == CurvedLowerFastenerIndex)
            {
                panelLocalNormal = FindNaturalCurvedSurfaceNormal(
                    vertices,
                    normals,
                    bounds,
                    panelLocalPosition);
            }

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

            if (normal.x > MinimumStrongOutwardX)
            {
                normal.x = MinimumStrongOutwardX;
                normal.Normalize();
            }
        }

        private static Vector3 FindNaturalCurvedSurfaceNormal(
            Vector3[] vertices,
            Vector3[] normals,
            Bounds bounds,
            Vector3 surfacePosition)
        {
            // Use the panel's local X/Y cross-section to establish which way is out from the
            // curved fuselage. This lets a lower fastener point down/out naturally instead of
            // treating local -X as the only legal exterior direction.
            Vector3 outwardRadial = new Vector3(
                surfacePosition.x - bounds.center.x,
                surfacePosition.y - bounds.center.y,
                0f);
            if (outwardRadial.sqrMagnitude < 0.0001f)
            {
                outwardRadial = Vector3.down;
            }
            outwardRadial.Normalize();

            bool hasNormals = normals != null && normals.Length == vertices.Length;
            if (!hasNormals)
            {
                return outwardRadial;
            }

            float patchX = Mathf.Max(0.035f, bounds.size.x * 0.18f);
            float patchY = Mathf.Max(0.045f, bounds.size.y * 0.18f);
            float patchZ = Mathf.Max(0.045f, bounds.size.z * 0.12f);
            Vector3 accumulatedNormal = Vector3.zero;
            float totalWeight = 0f;

            for (int index = 0; index < vertices.Length; index++)
            {
                float dx = Mathf.Abs(vertices[index].x - surfacePosition.x);
                float dy = Mathf.Abs(vertices[index].y - surfacePosition.y);
                float dz = Mathf.Abs(vertices[index].z - surfacePosition.z);
                if (dx > patchX || dy > patchY || dz > patchZ)
                {
                    continue;
                }

                Vector3 candidateNormal = normals[index];
                if (candidateNormal.sqrMagnitude < 0.0001f)
                {
                    continue;
                }
                candidateNormal.Normalize();

                // Imported seam normals can have opposite winding. Flip only their sign so the
                // shape information is retained while every sample points away from the fuselage.
                if (Vector3.Dot(candidateNormal, outwardRadial) < 0f)
                {
                    candidateNormal = -candidateNormal;
                }

                float alignment = Vector3.Dot(candidateNormal, outwardRadial);
                if (alignment < 0.12f)
                {
                    continue;
                }

                float weight = alignment / (0.01f + dx + dy + dz);
                accumulatedNormal += candidateNormal * weight;
                totalWeight += weight;
            }

            Vector3 result = totalWeight > 0f && accumulatedNormal.sqrMagnitude > 0.0001f
                ? accumulatedNormal.normalized
                : outwardRadial;

            if (Vector3.Dot(result, outwardRadial) < 0f)
            {
                result = -result;
            }

            float finalAlignment = Vector3.Dot(result, outwardRadial);
            if (finalAlignment < 0.55f)
            {
                result = Vector3.Slerp(result, outwardRadial, 0.65f).normalized;
            }

            return result;
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
