using System.Collections;
using Bozo.ModularCharacters;
using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51BoZoCitizenVisual : MonoBehaviour
    {
        [SerializeField] H51CitizenPresentation configuredPresentation;
        [SerializeField] string presetName;
        [SerializeField] CharacterObject characterPreset;
        [SerializeField] CharacterObject workPreset;
        [SerializeField] CharacterObject[] casualPresets = System.Array.Empty<CharacterObject>();
        [SerializeField] OutfitSystem outfitSystem;
        [SerializeField] Animator visualAnimator;

        bool vehicleVisualSuppressed;

        public H51CitizenPresentation ConfiguredPresentation => configuredPresentation;
        public string PresetName => presetName;
        public CharacterObject CharacterPreset => characterPreset;
        public CharacterObject WorkPreset => workPreset ? workPreset : characterPreset;
        public CharacterObject[] CasualPresets => casualPresets;
        public OutfitSystem OutfitSystem => outfitSystem;
        public Animator VisualAnimator => visualAnimator;

        public void Configure(
            H51CitizenPresentation presentation,
            CharacterObject preset,
            OutfitSystem system,
            Animator animator)
        {
            configuredPresentation = presentation;
            characterPreset = preset;
            workPreset = preset;
            casualPresets = System.Array.Empty<CharacterObject>();
            presetName = preset ? preset.name : string.Empty;
            outfitSystem = system;
            visualAnimator = animator;

            ConfigureBoZoRuntime();
            SubscribeToLoad();
        }

        public void ConfigureWardrobes(
            H51CitizenPresentation presentation,
            CharacterObject work,
            CharacterObject[] casual,
            OutfitSystem system,
            Animator animator)
        {
            configuredPresentation = presentation;
            workPreset = work;
            casualPresets = casual ?? System.Array.Empty<CharacterObject>();
            characterPreset = work ? work :
                (casualPresets.Length > 0 ? casualPresets[0] : null);
            presetName = characterPreset ? characterPreset.name : string.Empty;
            outfitSystem = system;
            visualAnimator = animator;

            ConfigureBoZoRuntime();
            SubscribeToLoad();
        }

        public void SetWardrobeContext(bool goingToOrAtWork, int dayIndex)
        {
            CharacterObject target = goingToOrAtWork
                ? WorkPreset
                : PickCasualPreset(dayIndex);

            if (!target || target == characterPreset)
                return;

            characterPreset = target;
            presetName = target.name;
            NormalizeV4OutfitAlpha();

            if (!outfitSystem)
                return;

            outfitSystem.characterData = target;

            // Routine changes happen after Start, so ask BoZo to rebuild only
            // when its runtime system is ready. Otherwise OnStart will load it.
            if (Application.isPlaying && outfitSystem.initalized)
                outfitSystem.LoadFromObject(target);
        }

        public void SetVehicleVisualSuppressed(bool suppressed)
        {
            vehicleVisualSuppressed = suppressed;
            ApplyVehicleVisualSuppression();
        }

        CharacterObject PickCasualPreset(int dayIndex)
        {
            if (casualPresets == null || casualPresets.Length == 0)
                return WorkPreset;

            int index = dayIndex % casualPresets.Length;
            if (index < 0)
                index += casualPresets.Length;

            CharacterObject selected = casualPresets[index];
            return selected ? selected : WorkPreset;
        }

        void LateUpdate()
        {
            if (vehicleVisualSuppressed)
                ApplyVehicleVisualSuppression();
        }

        void Awake()
        {
            ConfigureBoZoRuntime();
            SubscribeToLoad();

            H51CitizenIdentity identity =
                GetComponent<H51CitizenIdentity>();
            if (!identity || !outfitSystem)
                return;

            if (identity.Presentation != H51CitizenPresentation.Unknown &&
                identity.Presentation != configuredPresentation)
            {
                Debug.LogError(
                    "Hanger 51 BoZo visual presentation mismatch for " +
                    identity.DisplayName +
                    ". Identity=" + identity.Presentation +
                    ", visual=" + configuredPresentation +
                    ", preset=" + presetName + ".",
                    this);

                outfitSystem.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            StartCoroutine(DeferredMaterialRepair());
        }

        void OnDestroy()
        {
            if (outfitSystem)
                outfitSystem.OnCharacterLoaded -= HandleCharacterLoaded;
        }

        void ConfigureBoZoRuntime()
        {
            NormalizeV4OutfitAlpha();

            if (outfitSystem)
            {
                outfitSystem.characterData = characterPreset;
                outfitSystem.loadMode = OutfitSystem.LoadMode.OnStart;
                outfitSystem.async = false;

                // BoZo performs a final texture bake AFTER its character merge.
                // cleanAfterMerge removes the outfit sources during the merge,
                // leaving that final bake with nothing to paint and producing
                // solid-black atlases in a player build. Keep the modular sources
                // until the package has completed its full load/bake sequence.
                outfitSystem.cleanAfterMerge = false;
                outfitSystem._textureSize =
                    OutfitSystem.TextureSize._512;
            }

            if (visualAnimator)
            {
                visualAnimator.applyRootMotion = false;
                visualAnimator.cullingMode =
                    AnimatorCullingMode.CullUpdateTransforms;
                visualAnimator.updateMode =
                    AnimatorUpdateMode.Normal;
            }
        }

        void NormalizeV4OutfitAlpha()
        {
            CharacterObject preset =
                characterPreset
                    ? characterPreset
                    : (outfitSystem ? outfitSystem.characterData : null);

            CharacterData data =
                preset ? preset.GetCharacterData() : null;

            if (data == null ||
                data.versionID < 4 ||
                data.outfitDatas == null)
                return;

            int repaired = 0;

            for (int i=0;i<data.outfitDatas.Count;i++)
            {
                OutfitData item =
                    data.outfitDatas[i];

                if (item.alpha >= .999f)
                    continue;

                item.alpha = 1f;
                data.outfitDatas[i] = item;
                repaired++;
            }

            if (repaired > 0)
            {
                Debug.LogWarning(
                    "Hanger 51 repaired " + repaired +
                    " invalid BoZo v4 outfit alpha layer(s) on preset " +
                    (preset ? preset.name : presetName) +
                    ". This prevents the floating-black-head/body-loss regression from older Step 164 generated presets.",
                    this);
            }
        }

        void SubscribeToLoad()
        {
            if (!outfitSystem)
                return;

            outfitSystem.OnCharacterLoaded -= HandleCharacterLoaded;
            outfitSystem.OnCharacterLoaded += HandleCharacterLoaded;
        }

        void HandleCharacterLoaded()
        {
            RepairLoadedMaterials();
        }

        IEnumerator DeferredMaterialRepair()
        {
            yield return null;
            yield return null;
            RepairLoadedMaterials();
        }

        void RepairLoadedMaterials()
        {
            if (!outfitSystem)
                return;

            SanitizeVisualPhysics();

            Renderer[] renderers =
                outfitSystem.GetComponentsInChildren<Renderer>(true);

            for (int i=0;i<renderers.Length;i++)
            {
                Renderer renderer = renderers[i];
                if (!renderer)
                    continue;

                Material[] materials = renderer.materials;
                bool changed = false;

                for (int m=0;m<materials.Length;m++)
                {
                    Material material = materials[m];
                    if (!material)
                        continue;

                    if (material.HasProperty("_UseOutline"))
                    {
                        material.SetFloat("_UseOutline",0f);
                        material.DisableKeyword("_USEOUTLINE");
                        changed = true;
                    }

                    if (material.HasProperty("_OutlineDistance"))
                    {
                        material.SetFloat("_OutlineDistance",0f);
                        changed = true;
                    }

                    if (material.HasProperty("_Brightness"))
                    {
                        material.SetFloat(
                            "_Brightness",
                            Mathf.Max(
                                1f,
                                material.GetFloat("_Brightness")));
                        changed = true;
                    }

                    if (material.HasProperty("_UseAmbiant"))
                    {
                        material.SetFloat("_UseAmbiant",1f);
                        changed = true;
                    }

                    if (material.HasProperty("_AmbiantStrength"))
                    {
                        material.SetFloat(
                            "_AmbiantStrength",
                            Mathf.Max(
                                .65f,
                                material.GetFloat("_AmbiantStrength")));
                        changed = true;
                    }
                }

                if (changed)
                    renderer.materials = materials;
            }

            ApplyVehicleVisualSuppression();
        }

        void ApplyVehicleVisualSuppression()
        {
            if (!outfitSystem)
                return;

            Renderer[] renderers =
                outfitSystem.GetComponentsInChildren<Renderer>(true);

            for (int i=0;i<renderers.Length;i++)
            {
                Renderer renderer = renderers[i];
                if (!renderer)
                    continue;

                renderer.forceRenderingOff = vehicleVisualSuppressed;
            }
        }

        void SanitizeVisualPhysics()
        {
            Collider[] colliders =
                outfitSystem.GetComponentsInChildren<Collider>(true);
            for (int i=0;i<colliders.Length;i++)
            {
                if (colliders[i])
                    colliders[i].enabled = false;
            }

            CharacterController[] controllers =
                outfitSystem.GetComponentsInChildren<CharacterController>(true);
            for (int i=0;i<controllers.Length;i++)
            {
                if (controllers[i])
                    controllers[i].enabled = false;
            }

            Rigidbody[] bodies =
                outfitSystem.GetComponentsInChildren<Rigidbody>(true);
            for (int i=0;i<bodies.Length;i++)
            {
                Rigidbody body = bodies[i];
                if (!body)
                    continue;

                body.isKinematic = true;
                body.detectCollisions = false;
                body.useGravity = false;
            }
        }
    }
}
