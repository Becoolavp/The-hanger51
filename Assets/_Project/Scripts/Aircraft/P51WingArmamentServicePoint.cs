using System;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    /// <summary>
    /// Standalone-safe replacement for the legacy P51WingArmamentServiceTarget.
    ///
    /// This component intentionally lives in its own matching source file. Unity can resolve
    /// MonoBehaviour classes more reliably when each serialized component has an unambiguous
    /// script asset. The legacy target remains available only so existing scenes can be migrated
    /// without losing their serialized configuration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class P51WingArmamentServicePoint : MonoBehaviour
    {
        [SerializeField] private P51WingArmamentSystem system;
        [SerializeField] private P51WingArmamentServiceKind serviceKind;
        [SerializeField, Range(0, 1)] private int wingIndex;
        [SerializeField, Range(0, 5)] private int stationIndex;
        [SerializeField, Min(0.25f)] private float holdSeconds = 1.25f;
        [SerializeField] private Transform[] holdDownBolts = Array.Empty<Transform>();
        [SerializeField] private GameObject installHighlightRoot;

        private Vector3[] boltInstalledPositions = Array.Empty<Vector3>();
        private Quaternion[] boltInstalledRotations = Array.Empty<Quaternion>();
        private bool boltPoseCaptured;
        private float holdProgress;
        private bool removing;

        public P51WingArmamentSystem System => system;
        public P51WingArmamentServiceKind ServiceKind => serviceKind;
        public int WingIndex => wingIndex;
        public int StationIndex => stationIndex;

        private void Awake()
        {
            ResolveSystem();
            CaptureBoltPose();
            RefreshHighlight(null);
        }

        private void OnEnable()
        {
            ResolveSystem();
            CaptureBoltPose();
        }

        public void Configure(
            P51WingArmamentSystem configuredSystem,
            P51WingArmamentServiceKind configuredKind,
            int configuredWingIndex,
            int configuredStationIndex,
            Transform[] configuredBolts,
            GameObject configuredHighlightRoot,
            float configuredHoldSeconds = 1.25f)
        {
            system = configuredSystem;
            serviceKind = configuredKind;
            wingIndex = Mathf.Clamp(configuredWingIndex, 0, 1);
            stationIndex = Mathf.Clamp(configuredStationIndex, 0, 5);
            holdSeconds = Mathf.Max(0.25f, configuredHoldSeconds);
            holdDownBolts = configuredBolts ?? Array.Empty<Transform>();
            installHighlightRoot = configuredHighlightRoot;
            boltPoseCaptured = false;
            CaptureBoltPose();
            RefreshHighlight(null);
        }

        public string GetInteractionText(PlayerInventory inventory)
        {
            ResolveSystem();
            if (system == null) return string.Empty;

            if (serviceKind == P51WingArmamentServiceKind.WingPanel)
            {
                return system.IsPanelOpen(wingIndex)
                    ? $"E: close {(wingIndex == 0 ? "left" : "right")} wing armament panel"
                    : $"E: open {(wingIndex == 0 ? "left" : "right")} wing armament panel";
            }

            int percent = Mathf.RoundToInt(holdProgress * 100f);
            string progress = holdProgress > 0f ? $" ({percent}%)" : string.Empty;

            if (serviceKind == P51WingArmamentServiceKind.GunMount)
            {
                if (system.IsGunInstalled(stationIndex))
                {
                    return system.IsAmmoInstalled(stationIndex)
                        ? $"{system.GetStationName(stationIndex)} — remove ammunition first | X inspect"
                        : $"Hold R: unbolt and remove wing gun{progress} | X inspect";
                }

                return inventory != null && inventory.EquippedItem == system.GunItem
                    ? $"Hold E: lower equipped M2-style wing gun into mount and tighten bolts{progress} | X inspect"
                    : $"Empty {system.GetStationName(stationIndex)} — equip a P-51 M2 Wing Gun | X inspect";
            }

            if (!system.IsGunInstalled(stationIndex))
            {
                return "Install the adjacent wing gun before loading ammunition | X inspect";
            }

            if (system.IsAmmoInstalled(stationIndex))
            {
                int remaining = system.GetAmmoRemaining(stationIndex);
                if (remaining > 0 && remaining < 200)
                {
                    return $"Ammunition connected — {remaining} game rounds remain | X inspect";
                }

                return $"Hold R: remove/clear ammunition box{progress} | X inspect";
            }

            return inventory != null && inventory.EquippedItem == system.AmmoBoxItem
                ? $"Hold E: place equipped ammunition box and connect belt{progress} | X inspect"
                : "Empty ammunition bay — equip a P-51 Wing Ammunition Box | X inspect";
        }

        public bool ProcessInteraction(
            PlayerInventory inventory,
            bool pressedE,
            bool holdE,
            bool holdR,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveSystem();
            if (system == null) return false;

            if (serviceKind == P51WingArmamentServiceKind.WingPanel)
            {
                CancelHold();
                return pressedE && system.TogglePanel(wingIndex, out resultMessage);
            }

            bool wantsRemove = holdR && !holdE;
            bool wantsInstall = holdE && !holdR;
            bool valid;

            if (serviceKind == P51WingArmamentServiceKind.GunMount)
            {
                valid = wantsRemove
                    ? system.IsGunInstalled(stationIndex) && !system.IsAmmoInstalled(stationIndex)
                    : wantsInstall
                        && !system.IsGunInstalled(stationIndex)
                        && inventory != null
                        && inventory.EquippedItem == system.GunItem;
            }
            else
            {
                valid = wantsRemove
                    ? system.IsAmmoInstalled(stationIndex)
                    : wantsInstall
                        && system.IsGunInstalled(stationIndex)
                        && !system.IsAmmoInstalled(stationIndex)
                        && inventory != null
                        && inventory.EquippedItem == system.AmmoBoxItem;
            }

            if (!valid)
            {
                CancelHold();
                return false;
            }

            if (holdProgress > 0f && removing != wantsRemove)
            {
                holdProgress = 0f;
            }

            removing = wantsRemove;
            holdProgress = Mathf.Clamp01(
                holdProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.25f, holdSeconds));
            ApplyBoltPose(holdProgress, removing, true);

            if (holdProgress < 1f)
            {
                return false;
            }

            bool completed;
            if (serviceKind == P51WingArmamentServiceKind.GunMount)
            {
                completed = removing
                    ? system.TryRemoveGun(stationIndex, inventory, out resultMessage)
                    : system.TryInstallGun(stationIndex, inventory, out resultMessage);
            }
            else
            {
                completed = removing
                    ? system.TryRemoveOrClearAmmo(stationIndex, inventory, out resultMessage)
                    : system.TryInstallAmmo(stationIndex, inventory, out resultMessage);
            }

            CancelHold();
            return completed;
        }

        public string Inspect()
        {
            ResolveSystem();
            if (system == null) return "Wing armament system unavailable.";

            if (serviceKind == P51WingArmamentServiceKind.WingPanel)
            {
                return $"{(wingIndex == 0 ? "Left" : "Right")} wing armament access panel | Three gun positions and three ammunition compartments.";
            }

            return system.InspectStation(stationIndex);
        }

        public void CancelHold()
        {
            holdProgress = 0f;
            removing = false;
            ApplyBoltPose(0f, false, false);
        }

        public void RefreshHighlight(PlayerInventory inventory)
        {
            if (installHighlightRoot == null || system == null)
            {
                return;
            }

            bool show = false;
            if (system.IsPanelAccessible(wingIndex) && inventory != null)
            {
                if (serviceKind == P51WingArmamentServiceKind.GunMount)
                {
                    show = !system.IsGunInstalled(stationIndex)
                        && inventory.EquippedItem == system.GunItem;
                }
                else if (serviceKind == P51WingArmamentServiceKind.AmmoBay)
                {
                    show = system.IsGunInstalled(stationIndex)
                        && !system.IsAmmoInstalled(stationIndex)
                        && inventory.EquippedItem == system.AmmoBoxItem;
                }
            }

            installHighlightRoot.SetActive(show);
            if (show)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f;
                installHighlightRoot.transform.localScale = Vector3.one * pulse;
            }
        }

        private void CaptureBoltPose()
        {
            if (boltPoseCaptured) return;

            holdDownBolts = holdDownBolts ?? Array.Empty<Transform>();
            boltInstalledPositions = new Vector3[holdDownBolts.Length];
            boltInstalledRotations = new Quaternion[holdDownBolts.Length];

            for (int index = 0; index < holdDownBolts.Length; index++)
            {
                Transform bolt = holdDownBolts[index];
                if (bolt == null) continue;
                boltInstalledPositions[index] = bolt.localPosition;
                boltInstalledRotations[index] = bolt.localRotation;
            }

            boltPoseCaptured = true;
        }

        private void ApplyBoltPose(float progress, bool isRemoving, bool animate)
        {
            CaptureBoltPose();

            for (int index = 0; index < holdDownBolts.Length; index++)
            {
                Transform bolt = holdDownBolts[index];
                if (bolt == null) continue;

                bool installedState = serviceKind == P51WingArmamentServiceKind.GunMount
                    ? system != null && system.IsGunInstalled(stationIndex)
                    : system != null && system.IsAmmoInstalled(stationIndex);

                float t = animate
                    ? (isRemoving ? progress : 1f - progress)
                    : (installedState ? 0f : 1f);

                bolt.localPosition = boltInstalledPositions[index] + Vector3.up * (0.065f * t);
                bolt.localRotation = boltInstalledRotations[index]
                    * Quaternion.Euler(0f, 720f * (animate ? progress : 0f), 0f);
            }
        }

        private void ResolveSystem()
        {
            if (system == null) system = GetComponentInParent<P51WingArmamentSystem>();
            if (system == null) system = FindFirstObjectByType<P51WingArmamentSystem>();
        }

        private void OnDisable()
        {
            CancelHold();
            if (installHighlightRoot != null) installHighlightRoot.SetActive(false);
        }

        private void OnValidate()
        {
            holdSeconds = Mathf.Max(0.25f, holdSeconds);
            wingIndex = Mathf.Clamp(wingIndex, 0, 1);
            stationIndex = Mathf.Clamp(stationIndex, 0, 5);
        }
    }
}
