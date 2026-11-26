using Bozo.ModularCharacters;
using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51BoZoCitizenVisual : MonoBehaviour
    {
        [SerializeField] H51CitizenPresentation configuredPresentation;
        [SerializeField] string presetName;
        [SerializeField] CharacterObject characterPreset;
        [SerializeField] OutfitSystem outfitSystem;
        [SerializeField] Animator visualAnimator;

        public H51CitizenPresentation ConfiguredPresentation => configuredPresentation;
        public string PresetName => presetName;
        public CharacterObject CharacterPreset => characterPreset;
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
            presetName = preset ? preset.name : string.Empty;
            outfitSystem = system;
            visualAnimator = animator;

            ConfigureBoZoRuntime();
        }

        void Awake()
        {
            ConfigureBoZoRuntime();

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

        void ConfigureBoZoRuntime()
        {
            if (outfitSystem)
            {
                outfitSystem.characterData = characterPreset;
                outfitSystem.loadMode = OutfitSystem.LoadMode.OnStart;
                outfitSystem.async = false;
                outfitSystem.cleanAfterMerge = true;
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
    }
}
