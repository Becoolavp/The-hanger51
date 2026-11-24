using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(180)]
    [DisallowMultipleComponent]
    public sealed class P51LandingGearVisualSuspensionFollower : MonoBehaviour
    {
        private const string TailwheelStrutName = "Tailwheel Oleo Strut";
        private const string TailwheelRimRootName = "Tailwheel Rim Visual";

        [SerializeField] private P51LandingGearMaintenanceController maintenance;
        [SerializeField] private Transform[] tireRoots = new Transform[3];
        [SerializeField] private Transform[] rimRoots = new Transform[3];
        [SerializeField] private Transform[] physicsVisualProxies = new Transform[3];

        private Transform tailwheelStrut;
        private Transform tailwheelGearRoot;
        private Transform tailwheelRimRoot;
        private float tailwheelStrutTopLocalY;
        private float tailwheelStrutLocalX;
        private float tailwheelStrutLocalZ;
        private float tailwheelStrutScaleX;
        private float tailwheelStrutScaleZ;
        private bool tailwheelStrutGeometryCaptured;

        public bool TailwheelStrutConnected => tailwheelStrut != null
            && tireRoots != null
            && tireRoots.Length > 2
            && tireRoots[2] != null;

        public bool TailwheelWheelHardwareConnected => TailwheelStrutConnected
            && tailwheelRimRoot != null;

        public void Configure(
            P51LandingGearMaintenanceController configuredMaintenance,
            Transform[] configuredTires,
            Transform[] configuredProxies)
        {
            maintenance = configuredMaintenance;
            tireRoots = Copy(configuredTires);
            physicsVisualProxies = Copy(configuredProxies);
            ResolveWheelRims();
            tailwheelStrutGeometryCaptured = false;
            tailwheelRimRoot = rimRoots[2];
            ResolveTailwheelGeometry();
        }

        private void OnEnable()
        {
            ResolveWheelRims();
            tailwheelStrutGeometryCaptured = false;
            tailwheelRimRoot = rimRoots[2];
            ResolveTailwheelGeometry();
        }

        private void LateUpdate()
        {
            if (maintenance == null
                || !maintenance.GearCommandDown
                || maintenance.DeploymentFraction < 0.94f)
            {
                return;
            }

            ResolveWheelRims();

            for (int wheelIndex = 0; wheelIndex < 3; wheelIndex++)
            {
                if (!maintenance.IsGearInstalled(wheelIndex)
                    || !maintenance.IsTireInstalled(wheelIndex))
                {
                    continue;
                }

                Transform tire = wheelIndex < tireRoots.Length ? tireRoots[wheelIndex] : null;
                Transform rim = wheelIndex < rimRoots.Length ? rimRoots[wheelIndex] : null;
                Transform proxy = wheelIndex < physicsVisualProxies.Length
                    ? physicsVisualProxies[wheelIndex]
                    : null;
                if (tire == null || proxy == null)
                {
                    continue;
                }

                // A complete installed wheel is a single moving visual assembly. The tire and
                // metal rim must both follow the raycast-wheel proxy. Previously only the tire
                // followed the main-gear proxy, which allowed the reinstall animation to finish
                // while the permanent wheel visuals stayed hidden/orphaned at the strut root.
                ForceVisible(tire);
                tire.SetPositionAndRotation(proxy.position, proxy.rotation);

                if (rim != null)
                {
                    ForceVisible(rim);
                    rim.SetPositionAndRotation(proxy.position, proxy.rotation);
                }

                if (wheelIndex == 2)
                {
                    tailwheelRimRoot = rim;
                    ResolveTailwheelGeometry();
                    StretchTailwheelStrutToGroundedWheel(tire);
                }
            }
        }

        private void ResolveWheelRims()
        {
            rimRoots = Resize(rimRoots);
            tireRoots = Resize(tireRoots);

            string[] labels = { "Left Main", "Right Main", "Tailwheel" };
            for (int wheelIndex = 0; wheelIndex < 3; wheelIndex++)
            {
                if (rimRoots[wheelIndex] != null)
                {
                    continue;
                }

                Transform tire = tireRoots[wheelIndex];
                Transform gear = tire != null ? tire.parent : null;
                if (gear == null)
                {
                    continue;
                }

                Transform[] all = gear.GetComponentsInChildren<Transform>(true);
                string targetName = labels[wheelIndex] + " Rim Visual";
                for (int index = 0; index < all.Length; index++)
                {
                    if (all[index] != null && all[index].name == targetName)
                    {
                        rimRoots[wheelIndex] = all[index];
                        break;
                    }
                }
            }
        }

        private static void ForceVisible(Transform root)
        {
            if (root == null)
            {
                return;
            }

            if (!root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null && !renderers[index].enabled)
                {
                    renderers[index].enabled = true;
                }
            }
        }

        private void ResolveTailwheelGeometry()
        {
            Transform tailTire = tireRoots != null && tireRoots.Length > 2
                ? tireRoots[2]
                : null;
            if (tailTire == null || tailTire.parent == null)
            {
                return;
            }

            tailwheelGearRoot = tailTire.parent;
            Transform[] all = tailwheelGearRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate == null)
                {
                    continue;
                }

                if (tailwheelStrut == null && candidate.name == TailwheelStrutName)
                {
                    tailwheelStrut = candidate;
                }
                else if (tailwheelRimRoot == null && candidate.name == TailwheelRimRootName)
                {
                    tailwheelRimRoot = candidate;
                }
            }

            if (tailwheelStrutGeometryCaptured
                || tailwheelStrut == null
                || tailwheelStrut.parent != tailwheelGearRoot)
            {
                return;
            }

            Vector3 position = tailwheelStrut.localPosition;
            Vector3 scale = tailwheelStrut.localScale;

            // Unity's primitive cylinder is two local units tall, so localScale.y is the
            // half-height. Preserve the upper attachment point and let only the lower end
            // extend to the grounded tailwheel center.
            tailwheelStrutTopLocalY = position.y + Mathf.Abs(scale.y);
            tailwheelStrutLocalX = position.x;
            tailwheelStrutLocalZ = position.z;
            tailwheelStrutScaleX = scale.x;
            tailwheelStrutScaleZ = scale.z;
            tailwheelStrutGeometryCaptured = true;
        }

        private void StretchTailwheelStrutToGroundedWheel(Transform tailTire)
        {
            ResolveTailwheelGeometry();
            if (!tailwheelStrutGeometryCaptured
                || tailwheelStrut == null
                || tailwheelGearRoot == null
                || tailTire == null)
            {
                return;
            }

            Vector3 tireCenterInGearRoot = tailwheelGearRoot.InverseTransformPoint(tailTire.position);
            float lowerAttachmentY = tireCenterInGearRoot.y + 0.02f;
            float topY = Mathf.Max(tailwheelStrutTopLocalY, lowerAttachmentY + 0.10f);
            float centerY = (topY + lowerAttachmentY) * 0.5f;
            float halfHeight = Mathf.Max(0.05f, (topY - lowerAttachmentY) * 0.5f);

            tailwheelStrut.localPosition = new Vector3(
                tailwheelStrutLocalX,
                centerY,
                tailwheelStrutLocalZ);
            tailwheelStrut.localScale = new Vector3(
                tailwheelStrutScaleX,
                halfHeight,
                tailwheelStrutScaleZ);
        }

        private static Transform[] Resize(Transform[] source)
        {
            return source != null && source.Length == 3
                ? source
                : Copy(source);
        }

        private static Transform[] Copy(Transform[] source)
        {
            Transform[] result = new Transform[3];
            if (source != null)
            {
                System.Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            }
            return result;
        }
    }
}
