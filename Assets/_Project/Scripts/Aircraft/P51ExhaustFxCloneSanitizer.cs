using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    // This must run before P51MerlinAudioAndExhaustFxController (execution order 260).
    // A spawned aircraft is cloned from the live master, so it can inherit runtime-only
    // exhaust/audio children that already exist on the master. Those inherited objects
    // must be removed from the clone before the new Merlin controller scans for stacks
    // and creates its own runtime FX/audio set.
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51MerlinAudioAndExhaustFxController))]
    public sealed class P51ExhaustFxCloneSanitizer : MonoBehaviour
    {
        private const string FxPrefix = "Startup Exhaust FX ";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo ExhaustAnchorsField =
            typeof(P51MerlinAudioAndExhaustFxController).GetField("exhaustAnchors", PrivateInstance);
        private static readonly FieldInfo RumbleSourceField =
            typeof(P51MerlinAudioAndExhaustFxController).GetField("rumbleSource", PrivateInstance);
        private static readonly FieldInfo CombustionSourceField =
            typeof(P51MerlinAudioAndExhaustFxController).GetField("combustionSource", PrivateInstance);
        private static readonly FieldInfo RoughSourceField =
            typeof(P51MerlinAudioAndExhaustFxController).GetField("roughSource", PrivateInstance);
        private static readonly FieldInfo StarterSourceField =
            typeof(P51MerlinAudioAndExhaustFxController).GetField("starterSource", PrivateInstance);

        private P51MerlinAudioAndExhaustFxController audioFx;
        private float nextSanitizeTime;

        public int LastOwnedEmitterCount { get; private set; }
        public int LastRemovedDuplicateCount { get; private set; }
        public int LastPreAwakeRuntimeChildrenRemoved { get; private set; }

        private void Awake()
        {
            audioFx = GetComponent<P51MerlinAudioAndExhaustFxController>();
            LastPreAwakeRuntimeChildrenRemoved = PurgeInheritedRuntimeChildrenBeforeMerlinAwake();
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
                    || candidate == transform
                    || !candidate.name.StartsWith(FxPrefix, StringComparison.Ordinal)
                    || owned.Contains(candidate))
                {
                    continue;
                }

                DetachDisableAndDestroy(candidate, "Stale P-51 Startup Exhaust FX");
                LastRemovedDuplicateCount++;
            }

            return LastRemovedDuplicateCount;
        }

        private int PurgeInheritedRuntimeChildrenBeforeMerlinAwake()
        {
            if (audioFx == null)
            {
                return 0;
            }

            HashSet<Transform> ownedExhaust = GetCurrentlyOwnedExhaustAnchors();
            HashSet<Transform> ownedAudio = GetCurrentlyOwnedAudioSourceTransforms();
            int removed = 0;

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate == null || candidate == transform)
                {
                    continue;
                }

                bool inheritedExhaust = candidate.name.StartsWith(FxPrefix, StringComparison.Ordinal)
                    && !ownedExhaust.Contains(candidate);
                bool inheritedAudio = IsMerlinRuntimeAudioChildName(candidate.name)
                    && !ownedAudio.Contains(candidate);

                if (!inheritedExhaust && !inheritedAudio)
                {
                    continue;
                }

                // Destroy is deferred until the end of the frame, so first detach and
                // rename the object. That immediately removes it from this aircraft's
                // child scan and, critically, removes the words "Exhaust Stack" before
                // the Merlin FX controller performs its own Awake-time stack discovery.
                DetachDisableAndDestroy(
                    candidate,
                    inheritedExhaust
                        ? "Stale P-51 Startup Exhaust FX"
                        : "Stale P-51 Runtime Engine Audio");
                removed++;
            }

            return removed;
        }

        private HashSet<Transform> GetCurrentlyOwnedExhaustAnchors()
        {
            HashSet<Transform> owned = new HashSet<Transform>();
            if (audioFx == null || ExhaustAnchorsField == null)
            {
                return owned;
            }

            List<Transform> anchors = ExhaustAnchorsField.GetValue(audioFx) as List<Transform>;
            if (anchors == null)
            {
                return owned;
            }

            for (int index = 0; index < anchors.Count; index++)
            {
                if (anchors[index] != null)
                {
                    owned.Add(anchors[index]);
                }
            }
            return owned;
        }

        private HashSet<Transform> GetCurrentlyOwnedAudioSourceTransforms()
        {
            HashSet<Transform> owned = new HashSet<Transform>();
            AddAudioSourceTransform(owned, RumbleSourceField);
            AddAudioSourceTransform(owned, CombustionSourceField);
            AddAudioSourceTransform(owned, RoughSourceField);
            AddAudioSourceTransform(owned, StarterSourceField);
            return owned;
        }

        private void AddAudioSourceTransform(HashSet<Transform> owned, FieldInfo field)
        {
            if (owned == null || audioFx == null || field == null)
            {
                return;
            }

            AudioSource source = field.GetValue(audioFx) as AudioSource;
            if (source != null)
            {
                owned.Add(source.transform);
            }
        }

        private static bool IsMerlinRuntimeAudioChildName(string objectName)
        {
            return objectName == "Merlin Deep Rumble"
                || objectName == "Merlin Combustion"
                || objectName == "Merlin Rough Running"
                || objectName == "Merlin Starter";
        }

        private static void DetachDisableAndDestroy(Transform candidate, string replacementName)
        {
            if (candidate == null)
            {
                return;
            }

            candidate.name = replacementName;
            candidate.SetParent(null, true);
            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
        }

        private void AlignOwnedEmitter(Transform anchor)
        {
            if (anchor == null || !anchor.name.StartsWith(FxPrefix, StringComparison.Ordinal))
            {
                return;
            }

            string stackName = anchor.name.Substring(FxPrefix.Length);
            Transform stack = FindNamedPhysicalExhaustStack(stackName);
            if (stack == null)
            {
                return;
            }

            anchor.position = stack.TransformPoint(Vector3.up * 1.03f);
            Vector3 forward = stack.up.sqrMagnitude > 0.0001f ? stack.up : transform.forward;
            Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up : Vector3.up;
            anchor.rotation = Quaternion.LookRotation(forward, up);
        }

        private Transform FindNamedPhysicalExhaustStack(string stackName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate == null
                    || candidate.name != stackName
                    || candidate.name.StartsWith(FxPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate;
            }
            return null;
        }
    }
}
