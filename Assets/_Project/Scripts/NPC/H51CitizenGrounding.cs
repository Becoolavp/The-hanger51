using System;
using UnityEngine;
using UnityEngine.AI;

namespace Hanger51.NPCs
{
    [DefaultExecutionOrder(80)]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class H51CitizenGrounding : MonoBehaviour
    {
        [SerializeField] float visualFootOffset;
        [SerializeField] float groundClearance = .025f;
        [SerializeField] float sampleInterval = .08f;
        [SerializeField] float correctionSpeed = 5f;
        [SerializeField] float maxPhysicalCorrection = .9f;

        NavMeshAgent agent;
        Renderer[] renderers;
        float nextSample;
        bool calibrated;

        public float VisualFootOffset => visualFootOffset;
        public bool Calibrated => calibrated;

        void Awake()
        {
            Cache();
        }

        void Start()
        {
            CalibrateFromCurrentPose();
            ApplyGrounding(true);
        }

        void LateUpdate()
        {
            if (Time.time < nextSample) return;
            nextSample = Time.time + sampleInterval;
            ApplyGrounding(false);
        }

        public void CalibrateFromCurrentPose()
        {
            Cache();

            bool found = false;
            float minY = float.PositiveInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;

                Bounds b = r.bounds;
                if (b.size.y < .05f) continue;
                minY = Mathf.Min(minY, b.min.y);
                found = true;
            }

            if (found)
            {
                visualFootOffset = Mathf.Clamp(
                    transform.position.y - minY,
                    -.25f,
                    .55f);
            }

            calibrated = true;

            if (agent)
                agent.baseOffset = visualFootOffset;
        }

        void Cache()
        {
            if (!agent) agent = GetComponent<NavMeshAgent>();
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
        }

        void ApplyGrounding(bool immediate)
        {
            if (!agent || !agent.enabled || !agent.isOnNavMesh) return;
            if (!calibrated) CalibrateFromCurrentPose();

            Vector3 sampleFrom = transform.position;
            if (!NavMesh.SamplePosition(sampleFrom, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
                return;

            float physicalY = navHit.position.y;
            if (TryFindPhysicalGround(out float floorY))
                physicalY = floorY;

            float physicalDelta = Mathf.Clamp(
                physicalY - navHit.position.y,
                -maxPhysicalCorrection,
                maxPhysicalCorrection);

            float target = Mathf.Clamp(
                visualFootOffset + physicalDelta + groundClearance,
                -.25f,
                1.15f);

            if (immediate)
                agent.baseOffset = target;
            else
                agent.baseOffset = Mathf.MoveTowards(
                    agent.baseOffset,
                    target,
                    correctionSpeed * sampleInterval);
        }

        bool TryFindPhysicalGround(out float y)
        {
            y = 0f;
            Vector3 origin = transform.position + Vector3.up * 1.35f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                3.75f,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0) return false;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool fallbackFound = false;
            float fallbackY = 0f;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (!collider) continue;
                if (collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;
                if (hits[i].normal.y < .45f) continue;

                if (IsPreferredWalkSurface(collider))
                {
                    y = hits[i].point.y;
                    return true;
                }

                if (!fallbackFound && hits[i].point.y <= transform.position.y + .30f)
                {
                    fallbackFound = true;
                    fallbackY = hits[i].point.y;
                }
            }

            if (!fallbackFound) return false;
            y = fallbackY;
            return true;
        }

        static bool IsPreferredWalkSurface(Collider collider)
        {
            if (collider is TerrainCollider) return true;

            Transform t = collider.transform;
            for (int depth = 0; t && depth < 4; depth++, t = t.parent)
            {
                string n = (t.name ?? string.Empty).ToLowerInvariant();
                if (n.Contains("floor") ||
                    n.Contains("ground") ||
                    n.Contains("terrain") ||
                    n.Contains("road") ||
                    n.Contains("sidewalk") ||
                    n.Contains("pavement") ||
                    n.Contains("parking") ||
                    n.Contains("driveway") ||
                    n.Contains("porch") ||
                    n.Contains("walkway") ||
                    n.Contains("pad"))
                    return true;
            }

            return false;
        }
    }
}
