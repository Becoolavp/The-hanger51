using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(520)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51MerlinAudioAndExhaustFxController))]
    public sealed class P51ExhaustFxCloneSanitizer : MonoBehaviour
    {
        private const string FxPrefix = "Startup Exhaust FX ";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo ExhaustAnchorsField =
            typeof(P51MerlinAudioAndExhaustFxController).GetField("exhaustAnchors", PrivateInstance);

        private P51MerlinAudioAndExhaustFxController audioFx;
        private float nextSanitizeTime;

        public int LastOwnedEmitterCount { get; private set; }
        public int LastRemovedDuplicateCount { get; private set; }

        private void Awake()
        {
            audioFx = GetComponent<P51MerlinAudioAndExhaustFxController>();
        }

        private void OnEnable()
        {
            if (audioFx == null)
            {
                audioFx = GetComponent<P51MerlinAudioAndExhaustFxController>();
            }
            nextSanitizeTime = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextSanitizeTime)
            {
                return;
            }

            nextSanitizeTime = Time.unscaledTime + 0.5f;
            SanitizeNow();
        }

        public int SanitizeNow()
        {
            LastRemovedDuplicateCount = 0;
            LastOwnedEmitterCount = 0;

            if (audioFx == null)
            {
                audioFx = GetComponent<P51MerlinAudioAndExhaustFxController>();
            }
            if (audioFx == null || ExhaustAnchorsField == null)
            {
                return 0;
            }

            List<Transform> ownedAnchors =
                ExhaustAnchorsField.GetValue(audioFx) as List<Transform>;
            if (ownedAnchors == null || ownedAnchors.Count == 0)
            {
                return 0;
            }

            HashSet<Transform> owned = new HashSet<Transform>();
            for (int index = 0; index < ownedAnchors.Count; index++)
            {
                Transform anchor = ownedAnchors[index];
                if (anchor == null)
                {
                    continue;
                }
                owned.Add(anchor);
                LastOwnedEmitterCount++;
                AlignOwnedEmitter(anchor);
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate == null
                    || !candidate.name.StartsWith(FxPrefix, StringComparison.Ordinal)
                    || owned.Contains(candidate))
                {
                    continue;
                }

                candidate.gameObject.SetActive(false);
                Destroy(candidate.gameObject);
                LastRemovedDuplicateCount++;
            }

            return LastRemovedDuplicateCount;
        }

        private void AlignOwnedEmitter(Transform anchor)
        {
            if (anchor == null || !anchor.name.StartsWith(FxPrefix, StringComparison.Ordinal))
            {
                return;
            }

            string stackName = anchor.name.Substring(FxPrefix.Length);
            Transform stack = FindNamedExhaustStack(stackName);
            if (stack == null)
            {
                return;
            }

            anchor.position = stack.TransformPoint(Vector3.up * 1.03f);
            Vector3 forward = stack.up.sqrMagnitude > 0.0001f ? stack.up : transform.forward;
            Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up : Vector3.up;
            anchor.rotation = Quaternion.LookRotation(forward, up);
        }

        private Transform FindNamedExhaustStack(string stackName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null && candidate.name == stackName)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
