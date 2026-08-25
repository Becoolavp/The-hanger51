using System;
using System.Collections.Generic;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    public enum P51WingArmamentServiceKind
    {
        WingPanel,
        GunMount,
        AmmoBay
    }

    [DisallowMultipleComponent]
    public sealed class P51WingArmamentSystem : MonoBehaviour
    {
        public const string GunItemId = "p51-m2-wing-gun";
        public const string AmmoBoxItemId = "p51-wing-ammo-box";
        private const int WingCount = 2;
        private const int GunCount = 6;

        [Header("Aircraft")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private InventoryItemDefinition gunItem;
        [SerializeField] private InventoryItemDefinition ammoBoxItem;

        [Header("Wing Access")]
        [SerializeField] private Transform[] panelPivots = new Transform[WingCount];
        [SerializeField] private GameObject[] bayInteriorRoots = new GameObject[WingCount];
        [SerializeField] private bool[] panelOpen = { false, false };
        [SerializeField, Range(25f, 120f)] private float panelOpenAngle = 78f;
        [SerializeField, Min(1f)] private float panelAnimationSpeed = 4.5f;

        [Header("Six Gun Stations")]
        [SerializeField] private GameObject[] installedGunVisuals = new GameObject[GunCount];
        [SerializeField] private GameObject[] installedAmmoVisuals = new GameObject[GunCount];
        [SerializeField] private Transform[] muzzles = new Transform[GunCount];
        [SerializeField] private Transform[] ejectionPorts = new Transform[GunCount];
        [SerializeField] private bool[] gunInstalled = new bool[GunCount];
        [SerializeField] private bool[] ammoBoxInstalled = new bool[GunCount];
        [SerializeField] private int[] ammoRemaining = new int[GunCount];

        [Header("Game-Tuned Firing")]
        [SerializeField, Min(1)] private int gameRoundsPerAmmoBox = 200;
        [SerializeField, Min(0.04f)] private float secondsBetweenVolleys = 0.095f;
        [SerializeField, Min(50f)] private float visualRangeMeters = 850f;
        [SerializeField, Min(1f)] private float casingLifetimeSeconds = 7f;

        private readonly Quaternion[] panelClosedRotations = new Quaternion[WingCount];
        private readonly float[] panelBlend = new float[WingCount];
        private bool panelPoseCaptured;
        private float nextVolleyTime;
        private float nextEmptyMessageTime;
        private Material muzzleFlashMaterial;
        private Material tracerMaterial;
        private Material casingMaterial;
        private GUIStyle armamentHudStyle;

        public InventoryItemDefinition GunItem => gunItem;
        public InventoryItemDefinition AmmoBoxItem => ammoBoxItem;
        public bool CanService => flightController == null || !flightController.PilotPresent;
        public int InstalledGunCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < GunCount; index++)
                {
                    if (IsGunInstalled(index)) count++;
                }
                return count;
            }
        }
        public int TotalAmmo
        {
            get
            {
                int total = 0;
                for (int index = 0; index < GunCount; index++)
                {
                    if (index < ammoRemaining.Length) total += Mathf.Max(0, ammoRemaining[index]);
                }
                return total;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureArrays();
            CapturePanelPose();
            ApplyImmediateVisualState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureArrays();
            CapturePanelPose();
            ApplyImmediateVisualState();
        }

        private void Update()
        {
            ResolveReferences();
            EnsureArrays();
            CapturePanelPose();
            AnimatePanels();
            UpdateInstalledVisuals();
            HandlePilotFiring();
        }

        public void Configure(
            P51FlightController configuredFlightController,
            InventoryItemDefinition configuredGunItem,
            InventoryItemDefinition configuredAmmoBoxItem,
            Transform[] configuredPanelPivots,
            GameObject[] configuredBayInteriorRoots,
            GameObject[] configuredInstalledGunVisuals,
            GameObject[] configuredInstalledAmmoVisuals,
            Transform[] configuredMuzzles,
            Transform[] configuredEjectionPorts)
        {
            flightController = configuredFlightController;
            gunItem = configuredGunItem;
            ammoBoxItem = configuredAmmoBoxItem;
            panelPivots = Copy(configuredPanelPivots, WingCount);
            bayInteriorRoots = Copy(configuredBayInteriorRoots, WingCount);
            installedGunVisuals = Copy(configuredInstalledGunVisuals, GunCount);
            installedAmmoVisuals = Copy(configuredInstalledAmmoVisuals, GunCount);
            muzzles = Copy(configuredMuzzles, GunCount);
            ejectionPorts = Copy(configuredEjectionPorts, GunCount);
            EnsureArrays();
            panelPoseCaptured = false;
            CapturePanelPose();
            ApplyImmediateVisualState();
        }

        public bool IsPanelOpen(int wingIndex)
        {
            EnsureArrays();
            return wingIndex >= 0 && wingIndex < WingCount && panelOpen[wingIndex];
        }

        public bool IsPanelAccessible(int wingIndex)
        {
            EnsureArrays();
            return wingIndex >= 0
                && wingIndex < WingCount
                && (panelOpen[wingIndex] || panelBlend[wingIndex] >= 0.82f);
        }

        public bool TogglePanel(int wingIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            EnsureArrays();
            if (wingIndex < 0 || wingIndex >= WingCount)
            {
                resultMessage = "That wing access panel is invalid.";
                return false;
            }
            if (!CanService)
            {
                resultMessage = "Exit the cockpit before opening a wing armament panel.";
                return false;
            }

            panelOpen[wingIndex] = !panelOpen[wingIndex];
            resultMessage = panelOpen[wingIndex]
                ? $"Opened the {GetWingName(wingIndex)} armament access panel. Three gun mounts and three ammunition bays are exposed."
                : $"Closed the {GetWingName(wingIndex)} armament access panel.";
            return true;
        }

        public bool IsGunInstalled(int stationIndex)
        {
            EnsureArrays();
            return stationIndex >= 0 && stationIndex < GunCount && gunInstalled[stationIndex];
        }

        public bool IsAmmoInstalled(int stationIndex)
        {
            EnsureArrays();
            return stationIndex >= 0 && stationIndex < GunCount && ammoBoxInstalled[stationIndex];
        }

        public int GetAmmoRemaining(int stationIndex)
        {
            EnsureArrays();
            return stationIndex >= 0 && stationIndex < GunCount
                ? Mathf.Max(0, ammoRemaining[stationIndex])
                : 0;
        }

        public int GetWingForStation(int stationIndex)
        {
            return Mathf.Clamp(stationIndex, 0, GunCount - 1) < 3 ? 0 : 1;
        }

        public string GetStationName(int stationIndex)
        {
            int wing = GetWingForStation(stationIndex);
            int local = Mathf.Clamp(stationIndex, 0, GunCount - 1) % 3;
            return $"{GetWingName(wing)} gun station {local + 1}";
        }

        public bool TryInstallGun(
            int stationIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!ValidateServiceStation(stationIndex, inventory, out resultMessage))
            {
                return false;
            }
            if (IsGunInstalled(stationIndex))
            {
                resultMessage = $"A wing gun is already bolted into {GetStationName(stationIndex)}.";
                return false;
            }
            if (gunItem == null || inventory.EquippedItem != gunItem)
            {
                resultMessage = "Equip a P-51 M2 Wing Gun from inventory first.";
                return false;
            }
            if (!inventory.TryRemoveFirstItem(gunItem, out _))
            {
                resultMessage = "The equipped wing gun could not be removed from inventory.";
                return false;
            }

            gunInstalled[stationIndex] = true;
            UpdateInstalledVisuals();
            resultMessage = $"Installed and bolted down the M2-style wing gun in {GetStationName(stationIndex)}.";
            return true;
        }

        public bool TryRemoveGun(
            int stationIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!ValidateServiceStation(stationIndex, inventory, out resultMessage))
            {
                return false;
            }
            if (!IsGunInstalled(stationIndex))
            {
                resultMessage = $"{GetStationName(stationIndex)} is already empty.";
                return false;
            }
            if (IsAmmoInstalled(stationIndex))
            {
                resultMessage = "Remove or clear the ammunition box before unbolting the gun.";
                return false;
            }
            if (gunItem == null || inventory.AddItem(gunItem, 1) > 0)
            {
                resultMessage = "Inventory is full; make room before removing this gun.";
                return false;
            }

            gunInstalled[stationIndex] = false;
            UpdateInstalledVisuals();
            resultMessage = $"Unbolted and removed the wing gun from {GetStationName(stationIndex)} and returned it to inventory.";
            return true;
        }

        public bool TryInstallAmmo(
            int stationIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!ValidateServiceStation(stationIndex, inventory, out resultMessage))
            {
                return false;
            }
            if (!IsGunInstalled(stationIndex))
            {
                resultMessage = "Install the gun before loading its adjacent ammunition compartment.";
                return false;
            }
            if (IsAmmoInstalled(stationIndex))
            {
                resultMessage = $"An ammunition box is already installed here with {GetAmmoRemaining(stationIndex)} game rounds remaining.";
                return false;
            }
            if (ammoBoxItem == null || inventory.EquippedItem != ammoBoxItem)
            {
                resultMessage = "Equip a P-51 Wing Ammunition Box from inventory first.";
                return false;
            }
            if (!inventory.TryRemoveFirstItem(ammoBoxItem, out _))
            {
                resultMessage = "The equipped ammunition box could not be removed from inventory.";
                return false;
            }

            ammoBoxInstalled[stationIndex] = true;
            ammoRemaining[stationIndex] = Mathf.Max(1, gameRoundsPerAmmoBox);
            UpdateInstalledVisuals();
            resultMessage = $"Loaded the ammunition box beside {GetStationName(stationIndex)}. The belt is connected and ready.";
            return true;
        }

        public bool TryRemoveOrClearAmmo(
            int stationIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!ValidateServiceStation(stationIndex, inventory, out resultMessage))
            {
                return false;
            }
            if (!IsAmmoInstalled(stationIndex))
            {
                resultMessage = "That ammunition compartment is already empty.";
                return false;
            }

            int remaining = GetAmmoRemaining(stationIndex);
            if (remaining > 0 && remaining < gameRoundsPerAmmoBox)
            {
                resultMessage = $"This belt is partially used ({remaining} game rounds remain). For now, fire it empty before removing the box.";
                return false;
            }

            if (remaining >= gameRoundsPerAmmoBox)
            {
                if (ammoBoxItem == null || inventory.AddItem(ammoBoxItem, 1) > 0)
                {
                    resultMessage = "Inventory is full; make room before removing the unused ammunition box.";
                    return false;
                }
                resultMessage = "Removed the unused ammunition box and returned it to inventory.";
            }
            else
            {
                resultMessage = "Removed the empty ammunition box from the wing bay.";
            }

            ammoBoxInstalled[stationIndex] = false;
            ammoRemaining[stationIndex] = 0;
            UpdateInstalledVisuals();
            return true;
        }

        public string InspectStation(int stationIndex)
        {
            if (stationIndex < 0 || stationIndex >= GunCount)
            {
                return "Armament station is invalid.";
            }

            string gun = IsGunInstalled(stationIndex) ? "gun installed" : "gun missing";
            string ammo = IsAmmoInstalled(stationIndex)
                ? $"ammo box installed, {GetAmmoRemaining(stationIndex)} game rounds"
                : "ammo box missing";
            return $"{GetStationName(stationIndex)} | {gun} | {ammo}";
        }

        private bool ValidateServiceStation(
            int stationIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            EnsureArrays();
            if (stationIndex < 0 || stationIndex >= GunCount)
            {
                resultMessage = "That armament station is invalid.";
                return false;
            }
            if (inventory == null)
            {
                resultMessage = "Player inventory is unavailable.";
                return false;
            }
            if (!CanService)
            {
                resultMessage = "Exit the cockpit before servicing wing armament.";
                return false;
            }
            int wing = GetWingForStation(stationIndex);
            if (!IsPanelAccessible(wing))
            {
                resultMessage = $"Open the {GetWingName(wing)} armament access panel first.";
                return false;
            }
            return true;
        }

        private void HandlePilotFiring()
        {
            if (flightController == null || !flightController.PilotPresent)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.leftCtrlKey.isPressed)
            {
                return;
            }

            if (IsPanelOpen(0) || IsPanelOpen(1))
            {
                if (Time.unscaledTime >= nextEmptyMessageTime)
                {
                    nextEmptyMessageTime = Time.unscaledTime + 2f;
                    flightController.ShowCockpitMessage("Guns safed: close both wing armament panels before firing.", 1.7f);
                }
                return;
            }

            if (Time.time < nextVolleyTime)
            {
                return;
            }
            nextVolleyTime = Time.time + Mathf.Max(0.04f, secondsBetweenVolleys);

            bool firedAny = false;
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                if (!IsGunInstalled(stationIndex)
                    || !IsAmmoInstalled(stationIndex)
                    || GetAmmoRemaining(stationIndex) <= 0)
                {
                    continue;
                }

                ammoRemaining[stationIndex] = Mathf.Max(0, ammoRemaining[stationIndex] - 1);
                FireVisualShot(stationIndex);
                firedAny = true;
            }

            if (!firedAny && Time.unscaledTime >= nextEmptyMessageTime)
            {
                nextEmptyMessageTime = Time.unscaledTime + 2f;
                flightController.ShowCockpitMessage(
                    InstalledGunCount <= 0
                        ? "No wing guns installed."
                        : "Wing guns have no connected ammunition.",
                    1.7f);
            }
        }

        private void FireVisualShot(int stationIndex)
        {
            Transform muzzle = stationIndex < muzzles.Length ? muzzles[stationIndex] : null;
            Transform ejection = stationIndex < ejectionPorts.Length ? ejectionPorts[stationIndex] : null;
            if (muzzle != null)
            {
                SpawnMuzzleFlash(muzzle);
                SpawnTracer(muzzle);
            }
            if (ejection != null)
            {
                SpawnCasing(stationIndex, ejection);
            }
        }

        private void SpawnMuzzleFlash(Transform muzzle)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "P-51 Gun Muzzle Flash";
            flash.transform.position = muzzle.position + muzzle.forward * 0.08f;
            flash.transform.rotation = muzzle.rotation;
            flash.transform.localScale = new Vector3(0.11f, 0.07f, 0.22f);
            Collider collider = flash.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = flash.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = GetMuzzleFlashMaterial();
            Destroy(flash, 0.045f);
        }

        private void SpawnTracer(Transform muzzle)
        {
            Vector3 start = muzzle.position + muzzle.forward * 0.15f;
            Vector3 end = start + muzzle.forward * visualRangeMeters;
            RaycastHit[] hits = Physics.RaycastAll(
                start,
                muzzle.forward,
                visualRangeMeters,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int index = 0; index < hits.Length; index++)
                {
                    if (hits[index].collider != null
                        && !hits[index].collider.transform.IsChildOf(transform))
                    {
                        end = hits[index].point;
                        break;
                    }
                }
            }

            GameObject tracerObject = new GameObject("P-51 Gun Tracer");
            LineRenderer line = tracerObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.018f;
            line.endWidth = 0.006f;
            line.sharedMaterial = GetTracerMaterial();
            Destroy(tracerObject, 0.055f);
        }

        private void SpawnCasing(int stationIndex, Transform ejection)
        {
            GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            casing.name = "Spent Wing Gun Casing";
            casing.transform.SetPositionAndRotation(
                ejection.position,
                ejection.rotation * Quaternion.Euler(90f, 0f, 0f));
            casing.transform.localScale = new Vector3(0.012f, 0.027f, 0.012f);
            Renderer renderer = casing.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = GetCasingMaterial();

            Rigidbody body = casing.AddComponent<Rigidbody>();
            body.mass = 0.02f;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Vector3 outward = GetWingForStation(stationIndex) == 0 ? -transform.right : transform.right;
            Vector3 aircraftVelocity = flightController != null && flightController.AircraftBody != null
                ? flightController.AircraftBody.linearVelocity
                : Vector3.zero;
            body.linearVelocity = aircraftVelocity
                - transform.up * 2.8f
                + outward * 1.1f
                + UnityEngine.Random.insideUnitSphere * 0.6f;
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * 18f;
            Destroy(casing, Mathf.Max(1f, casingLifetimeSeconds));
        }

        private Material GetMuzzleFlashMaterial()
        {
            if (muzzleFlashMaterial == null)
            {
                muzzleFlashMaterial = CreateRuntimeMaterial(
                    "P-51 Muzzle Flash Material",
                    new Color(1f, 0.65f, 0.08f, 1f),
                    true);
            }
            return muzzleFlashMaterial;
        }

        private Material GetTracerMaterial()
        {
            if (tracerMaterial == null)
            {
                tracerMaterial = CreateRuntimeMaterial(
                    "P-51 Tracer Material",
                    new Color(1f, 0.78f, 0.18f, 1f),
                    true);
            }
            return tracerMaterial;
        }

        private Material GetCasingMaterial()
        {
            if (casingMaterial == null)
            {
                casingMaterial = CreateRuntimeMaterial(
                    "P-51 Spent Casing Material",
                    new Color(0.72f, 0.48f, 0.15f, 1f),
                    false);
            }
            return casingMaterial;
        }

        private static Material CreateRuntimeMaterial(string materialName, Color color, bool emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 4f);
                material.EnableKeyword("_EMISSION");
            }
            return material;
        }

        private void CapturePanelPose()
        {
            if (panelPoseCaptured) return;
            EnsureArrays();
            bool any = false;
            for (int wingIndex = 0; wingIndex < WingCount; wingIndex++)
            {
                if (panelPivots[wingIndex] == null) continue;
                panelClosedRotations[wingIndex] = panelPivots[wingIndex].localRotation;
                panelBlend[wingIndex] = panelOpen[wingIndex] ? 1f : 0f;
                any = true;
            }
            panelPoseCaptured = any;
        }

        private void AnimatePanels()
        {
            for (int wingIndex = 0; wingIndex < WingCount; wingIndex++)
            {
                Transform pivot = wingIndex < panelPivots.Length ? panelPivots[wingIndex] : null;
                if (pivot == null) continue;

                float target = panelOpen[wingIndex] ? 1f : 0f;
                panelBlend[wingIndex] = Mathf.MoveTowards(
                    panelBlend[wingIndex],
                    target,
                    Time.deltaTime * panelAnimationSpeed);
                Quaternion openRotation = panelClosedRotations[wingIndex]
                    * Quaternion.Euler(-panelOpenAngle, 0f, 0f);
                pivot.localRotation = Quaternion.Slerp(
                    panelClosedRotations[wingIndex],
                    openRotation,
                    panelBlend[wingIndex]);

                GameObject interior = wingIndex < bayInteriorRoots.Length
                    ? bayInteriorRoots[wingIndex]
                    : null;
                if (interior != null)
                {
                    interior.SetActive(panelBlend[wingIndex] >= 0.18f);
                }
            }
        }

        private void ApplyImmediateVisualState()
        {
            for (int wingIndex = 0; wingIndex < WingCount; wingIndex++)
            {
                if (wingIndex < bayInteriorRoots.Length && bayInteriorRoots[wingIndex] != null)
                {
                    bayInteriorRoots[wingIndex].SetActive(panelOpen[wingIndex]);
                }
            }
            UpdateInstalledVisuals();
        }

        private void UpdateInstalledVisuals()
        {
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                if (stationIndex < installedGunVisuals.Length && installedGunVisuals[stationIndex] != null)
                {
                    installedGunVisuals[stationIndex].SetActive(gunInstalled[stationIndex]);
                }
                if (stationIndex < installedAmmoVisuals.Length && installedAmmoVisuals[stationIndex] != null)
                {
                    installedAmmoVisuals[stationIndex].SetActive(ammoBoxInstalled[stationIndex]);
                }
            }
        }

        private void ResolveReferences()
        {
            if (flightController == null) flightController = GetComponent<P51FlightController>();
        }

        private void EnsureArrays()
        {
            panelPivots = Resize(panelPivots, WingCount);
            bayInteriorRoots = Resize(bayInteriorRoots, WingCount);
            panelOpen = Resize(panelOpen, WingCount, false);
            installedGunVisuals = Resize(installedGunVisuals, GunCount);
            installedAmmoVisuals = Resize(installedAmmoVisuals, GunCount);
            muzzles = Resize(muzzles, GunCount);
            ejectionPorts = Resize(ejectionPorts, GunCount);
            gunInstalled = Resize(gunInstalled, GunCount, false);
            ammoBoxInstalled = Resize(ammoBoxInstalled, GunCount, false);
            ammoRemaining = Resize(ammoRemaining, GunCount, 0);
            gameRoundsPerAmmoBox = Mathf.Max(1, gameRoundsPerAmmoBox);
        }

        private static T[] Copy<T>(T[] source, int length)
        {
            T[] result = new T[length];
            if (source != null) Array.Copy(source, result, Mathf.Min(source.Length, length));
            return result;
        }

        private static Transform[] Resize(Transform[] source, int length) => Copy(source, length);
        private static GameObject[] Resize(GameObject[] source, int length) => Copy(source, length);

        private static bool[] Resize(bool[] source, int length, bool defaultValue)
        {
            bool[] result = new bool[length];
            for (int index = 0; index < length; index++)
            {
                result[index] = source != null && index < source.Length ? source[index] : defaultValue;
            }
            return result;
        }

        private static int[] Resize(int[] source, int length, int defaultValue)
        {
            int[] result = new int[length];
            for (int index = 0; index < length; index++)
            {
                result[index] = source != null && index < source.Length ? source[index] : defaultValue;
            }
            return result;
        }

        private static string GetWingName(int wingIndex) => wingIndex == 0 ? "left wing" : "right wing";

        private void OnGUI()
        {
            if (flightController == null || !flightController.PilotPresent) return;
            if (armamentHudStyle == null)
            {
                armamentHudStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 15,
                    padding = new RectOffset(10, 10, 8, 8)
                };
            }

            string text = $"ARMAMENT\nGuns: {InstalledGunCount}/6\nAmmo: {TotalAmmo}\nLeft Ctrl: FIRE";
            GUI.Box(new Rect(18f, 258f, 190f, 90f), text, armamentHudStyle);
        }

        private void OnDestroy()
        {
            if (muzzleFlashMaterial != null) Destroy(muzzleFlashMaterial);
            if (tracerMaterial != null) Destroy(tracerMaterial);
            if (casingMaterial != null) Destroy(casingMaterial);
        }

        private void OnValidate()
        {
            EnsureArrays();
            panelOpenAngle = Mathf.Clamp(panelOpenAngle, 25f, 120f);
            panelAnimationSpeed = Mathf.Max(1f, panelAnimationSpeed);
            secondsBetweenVolleys = Mathf.Max(0.04f, secondsBetweenVolleys);
            visualRangeMeters = Mathf.Max(50f, visualRangeMeters);
            casingLifetimeSeconds = Mathf.Max(1f, casingLifetimeSeconds);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51WingArmamentServiceTarget : MonoBehaviour
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
            UpdateHighlight(null);
        }

        private void OnEnable()
        {
            ResolveSystem();
            CaptureBoltPose();
        }

        private void LateUpdate()
        {
            ResolveSystem();
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            UpdateHighlight(inventory);
            if (holdProgress <= 0f) ApplyBoltPose(0f, false, false);
        }

        public void Configure(
            P51WingArmamentSystem configuredSystem,
            P51WingArmamentServiceKind configuredKind,
            int configuredWingIndex,
            int configuredStationIndex,
            Transform[] configuredBolts,
            GameObject configuredHighlightRoot)
        {
            system = configuredSystem;
            serviceKind = configuredKind;
            wingIndex = Mathf.Clamp(configuredWingIndex, 0, 1);
            stationIndex = Mathf.Clamp(configuredStationIndex, 0, 5);
            holdDownBolts = configuredBolts ?? Array.Empty<Transform>();
            installHighlightRoot = configuredHighlightRoot;
            boltPoseCaptured = false;
            CaptureBoltPose();
            UpdateHighlight(null);
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
            bool valid = false;
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

            if (holdProgress > 0f && removing != wantsRemove) holdProgress = 0f;
            removing = wantsRemove;
            holdProgress = Mathf.Clamp01(
                holdProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.25f, holdSeconds));
            ApplyBoltPose(holdProgress, removing, true);
            if (holdProgress < 1f) return false;

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

        private void UpdateHighlight(PlayerInventory inventory)
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
                if (holdDownBolts[index] == null) continue;
                boltInstalledPositions[index] = holdDownBolts[index].localPosition;
                boltInstalledRotations[index] = holdDownBolts[index].localRotation;
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
    }

    [DefaultExecutionOrder(315)]
    [DisallowMultipleComponent]
    public sealed class P51WingArmamentPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField, Min(1f)] private float interactionDistance = 5.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        private P51WingArmamentServiceTarget currentTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeInteractor()
        {
            PlayerInventory player = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (player != null && player.GetComponent<P51WingArmamentPlayerInteractor>() == null)
            {
                player.gameObject.AddComponent<P51WingArmamentPlayerInteractor>();
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(Camera configuredCamera, InventoryUI configuredUI)
        {
            playerCamera = configuredCamera;
            inventoryUI = configuredUI;
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            if (playerCamera == null || inventory == null || inventoryUI == null || inventoryUI.IsOpen)
            {
                CancelCurrent();
                return;
            }

            P51WingArmamentServiceTarget target = FindTarget();
            if (target != currentTarget)
            {
                currentTarget?.CancelHold();
                currentTarget = target;
            }
            if (currentTarget == null) return;

            Keyboard keyboard = Keyboard.current;
            bool pressedE = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            bool holdE = keyboard != null && keyboard.eKey.isPressed;
            bool holdR = keyboard != null && keyboard.rKey.isPressed;

            if (currentTarget.ProcessInteraction(
                    inventory,
                    pressedE,
                    holdE,
                    holdR,
                    Time.deltaTime,
                    out string resultMessage)
                && !string.IsNullOrWhiteSpace(resultMessage))
            {
                inventoryUI.ShowStatusMessage(resultMessage, 3.5f);
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                inventoryUI.ShowStatusMessage(currentTarget.Inspect(), 4f);
            }

            inventoryUI.SetInteractionPrompt(currentTarget.GetInteractionText(inventory));
        }

        private P51WingArmamentServiceTarget FindTarget()
        {
            RaycastHit[] hits = Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return null;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null) continue;
                P51WingArmamentServiceTarget target =
                    collider.GetComponentInParent<P51WingArmamentServiceTarget>();
                if (target != null) return target;
            }
            return null;
        }

        private void ResolveReferences()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
            if (playerCamera == null && inventory != null)
            {
                playerCamera = inventory.GetComponentInChildren<Camera>(true);
            }
            if (inventoryUI == null) inventoryUI = FindFirstObjectByType<InventoryUI>();
        }

        private void CancelCurrent()
        {
            if (currentTarget == null) return;
            currentTarget.CancelHold();
            currentTarget = null;
            if (inventoryUI != null) inventoryUI.SetInteractionPrompt(string.Empty);
        }

        private void OnDisable()
        {
            CancelCurrent();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
        }
    }
}
