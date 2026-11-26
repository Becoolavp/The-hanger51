using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using Hanger51.NPCs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51BoZoCitizenMigration
    {
        const string VisualRootName = "H51 BoZo Citizen Visual";
        const string BoZoBasePath =
            "Assets/BoZo_StylizedModularCharacters/Prefabs/BSMC_CharacterBase.prefab";
        const string BoZoBodyRigPath =
            "Assets/BoZo_StylizedModularCharacters/Outfits/Common/Body/BodyRig.fbx";

        const string MaleIdlePath =
            "Assets/BoZo_StylizedModularCharacters/Animations/BMAC_M_Idle.anim";
        const string MaleWalkPath =
            "Assets/BoZo_StylizedModularCharacters/Animations/BMAC_M_Walk.anim";
        const string FemaleIdlePath =
            "Assets/BoZo_StylizedModularCharacters/Animations/BMAC_F_Idle.anim";
        const string FemaleWalkPath =
            "Assets/BoZo_StylizedModularCharacters/Animations/BMAC_F_Walk.anim";

        const string GeneratedFolder =
            "Assets/_Project/Characters/H51Citizens/BoZoGenerated";
        const string MaleControllerPath =
            GeneratedFolder + "/H51_BoZo_Male.controller";
        const string FemaleControllerPath =
            GeneratedFolder + "/H51_BoZo_Female.controller";

        static readonly string[] MalePresetNames =
        {
            "Default_Boy",
            "Jackal",
            "Kenji",
            "Zell"
        };

        static readonly string[] FemalePresetNames =
        {
            "Default_Girl",
            "Glover",
            "Hana",
            "Jayda"
        };

        static readonly HashSet<string> MaleFirstNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Aaron","Adam","Andrew","Ben","Brandon","Charles","Daniel","David",
                "Derek","Ethan","Henry","Jack","James","Jason","John","Kevin","Luke",
                "Matthew","Michael","Nathan","Patrick","Robert","Ryan","Scott","Thomas",
                "William","Caleb","Eric","Noah","Logan","Cole","Drew","Gavin","Nicholas",
                "Nick","Alex","Alexander","Christopher","Chris","Joshua","Josh","Jacob",
                "Jake","Samuel","Sam","George","Edward","Brian","Steven","Stephen","Mark",
                "Paul","Peter","Anthony","Joseph","Joe","Tyler","Kyle","Justin","Austin"
            };

        static readonly HashSet<string> FemaleFirstNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Abigail","Alicia","Anna","Caroline","Claire","Emily","Emma","Evelyn",
                "Grace","Hannah","Julia","Laura","Leah","Maria","Megan","Natalie",
                "Nicole","Olivia","Rachel","Rebecca","Sarah","Sophia","Victoria","Zoe",
                "Molly","Jenny","Brooke","Erin","Amanda","Jessica","Ashley","Elizabeth",
                "Katie","Katherine","Kate","Jennifer","Jen","Michelle","Melissa","Amy",
                "Angela","Samantha","Sophie","Chloe","Isabella","Isabelle","Madison"
            };

        [MenuItem("Hanger 51/World/Current/162 - Replace Citizen Bodies With BoZo Characters")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 162 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject basePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(BoZoBasePath);
            Avatar avatar =
                AssetDatabase.LoadAllAssetsAtPath(BoZoBodyRigPath)
                    .OfType<Avatar>()
                    .FirstOrDefault(a => a && a.isValid && a.isHuman);

            AnimationClip maleIdle =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(MaleIdlePath);
            AnimationClip maleWalk =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(MaleWalkPath);
            AnimationClip femaleIdle =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(FemaleIdlePath);
            AnimationClip femaleWalk =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(FemaleWalkPath);

            CharacterObject[] malePresets =
                LoadPresets(MalePresetNames);
            CharacterObject[] femalePresets =
                LoadPresets(FemalePresetNames);

            if (!basePrefab ||
                !avatar ||
                !maleIdle ||
                !maleWalk ||
                !femaleIdle ||
                !femaleWalk ||
                malePresets.Any(p => !p) ||
                femalePresets.Any(p => !p))
            {
                Debug.LogError(
                    "Step 162 failed. Required BoZo base prefab, humanoid Avatar, animation clips, or vetted male/female preset assets are missing. Reimport the purchased BoZo package first.");
                return;
            }

            ConfigureBoZoBuildShaders();
            DisableBoZoNpcOutlineOnSourceMaterial();

            EnsureFolder(GeneratedFolder);
            AnimatorController maleController =
                BuildController(
                    MaleControllerPath,
                    "H51 BoZo Male",
                    maleIdle,
                    maleWalk);
            AnimatorController femaleController =
                BuildController(
                    FemaleControllerPath,
                    "H51 BoZo Female",
                    femaleIdle,
                    femaleWalk);

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(c => c && c.gameObject.scene.IsValid())
                    .ToArray();

            if (citizens.Length == 0)
            {
                Debug.LogError(
                    "Step 162 failed. No scene citizens were found.");
                return;
            }

            int male = 0;
            int female = 0;
            int inferred = 0;
            int migrated = 0;
            int oldRenderersDisabled = 0;
            int oldAnimatorsRemoved = 0;

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity identity = citizens[i];
                if (!identity) continue;

                H51CitizenPresentation presentation =
                    identity.Presentation;

                if (presentation == H51CitizenPresentation.Unknown)
                {
                    presentation =
                        PresentationForName(
                            identity.DisplayName,
                            identity.AppearanceSeed);
                    identity.SetPresentation(presentation);
                    inferred++;
                }

                bool isFemale =
                    presentation == H51CitizenPresentation.Female;

                CharacterObject[] pool =
                    isFemale ? femalePresets : malePresets;
                CharacterObject preset =
                    pool[
                        PositiveMod(
                            identity.AppearanceSeed,
                            pool.Length)];

                RuntimeAnimatorController controller =
                    isFemale
                        ? femaleController
                        : maleController;

                if (isFemale) female++;
                else male++;

                Transform existing =
                    DirectChild(
                        identity.transform,
                        VisualRootName);
                if (existing)
                    Undo.DestroyObjectImmediate(
                        existing.gameObject);

                Renderer[] oldRenderers =
                    identity.GetComponentsInChildren<Renderer>(true);
                for (int r=0;r<oldRenderers.Length;r++)
                {
                    Renderer renderer = oldRenderers[r];
                    if (!renderer) continue;
                    Undo.RecordObject(
                        renderer,
                        "Disable legacy citizen renderer");
                    renderer.enabled = false;
                    renderer.forceRenderingOff = true;
                    EditorUtility.SetDirty(renderer);
                    oldRenderersDisabled++;
                }

                Animator[] oldAnimators =
                    identity.GetComponentsInChildren<Animator>(true);
                for (int a=0;a<oldAnimators.Length;a++)
                {
                    Animator oldAnimator = oldAnimators[a];
                    if (!oldAnimator) continue;
                    Undo.DestroyObjectImmediate(oldAnimator);
                    oldAnimatorsRemoved++;
                }

                H51CitizenVisualVariation legacyVariation =
                    identity.GetComponent<H51CitizenVisualVariation>();
                if (legacyVariation)
                {
                    Undo.RecordObject(
                        legacyVariation,
                        "Disable legacy citizen color variation");
                    legacyVariation.enabled = false;
                    EditorUtility.SetDirty(legacyVariation);
                }

                GameObject visual =
                    PrefabUtility.InstantiatePrefab(
                        basePrefab,
                        identity.transform) as GameObject;

                if (!visual)
                {
                    Debug.LogError(
                        "Step 162 could not instantiate the BoZo base for " +
                        identity.DisplayName + ".",
                        identity);
                    continue;
                }

                visual.name = VisualRootName;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                OutfitSystem outfit =
                    visual.GetComponent<OutfitSystem>();
                if (!outfit)
                {
                    Debug.LogError(
                        "Step 162 found no BoZo OutfitSystem on the base prefab for " +
                        identity.DisplayName + ".",
                        visual);
                    Undo.DestroyObjectImmediate(visual);
                    continue;
                }

                Undo.RecordObject(
                    outfit,
                    "Configure BoZo citizen preset");
                outfit.characterData = preset;
                outfit.loadMode = OutfitSystem.LoadMode.OnStart;
                outfit.async = false;
                outfit.cleanAfterMerge = false;
                outfit._textureSize =
                    OutfitSystem.TextureSize._512;
                EditorUtility.SetDirty(outfit);

                Animator animator =
                    Undo.AddComponent<Animator>(visual);
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode =
                    AnimatorCullingMode.CullUpdateTransforms;
                animator.updateMode =
                    AnimatorUpdateMode.Normal;
                EditorUtility.SetDirty(animator);

                H51BoZoCitizenVisual adapter =
                    identity.GetComponent<H51BoZoCitizenVisual>();
                if (!adapter)
                    adapter =
                        Undo.AddComponent<H51BoZoCitizenVisual>(
                            identity.gameObject);

                adapter.Configure(
                    presentation,
                    preset,
                    outfit,
                    animator);

                EditorUtility.SetDirty(adapter);
                EditorUtility.SetDirty(identity);
                migrated++;
            }

            PatchCitizenBrainAnimationReferences(citizens);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 162 complete. BoZo citizens migrated=" + migrated +
                "/" + citizens.Length +
                ", male=" + male +
                ", female=" + female +
                ", previously Unknown presentations assigned=" + inferred +
                ", legacy renderers disabled=" + oldRenderersDisabled +
                ", legacy animators removed=" + oldAnimatorsRemoved +
                ". Male citizens use only Default_Boy/Jackal/Kenji/Zell; female citizens use only Default_Girl/Glover/Hana/Jayda. Run Step 163.");
        }

        [MenuItem("Hanger 51/World/Current/163 - Validate BoZo Citizen Migration")]
        public static void Validate()
        {
            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(c => c && c.gameObject.scene.IsValid())
                    .ToArray();

            int unknown = 0;
            int missingVisual = 0;
            int missingOutfitSystem = 0;
            int missingAnimator = 0;
            int badPresetSex = 0;
            int legacyRendererStillVisible = 0;
            int legacyVariationEnabled = 0;
            int prematureCleanAfterMerge = 0;
            int male = 0;
            int female = 0;

            HashSet<string> maleAllowed =
                new HashSet<string>(
                    MalePresetNames,
                    StringComparer.OrdinalIgnoreCase);
            HashSet<string> femaleAllowed =
                new HashSet<string>(
                    FemalePresetNames,
                    StringComparer.OrdinalIgnoreCase);

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity identity = citizens[i];
                if (!identity) continue;

                if (identity.Presentation ==
                    H51CitizenPresentation.Unknown)
                {
                    unknown++;
                    continue;
                }

                if (identity.Presentation ==
                    H51CitizenPresentation.Female)
                    female++;
                else
                    male++;

                H51BoZoCitizenVisual adapter =
                    identity.GetComponent<H51BoZoCitizenVisual>();
                Transform visual =
                    DirectChild(
                        identity.transform,
                        VisualRootName);

                if (!adapter || !visual)
                {
                    missingVisual++;
                    continue;
                }

                OutfitSystem outfit =
                    visual.GetComponent<OutfitSystem>();
                Animator animator =
                    visual.GetComponent<Animator>();

                if (!outfit) missingOutfitSystem++;
                else if (outfit.cleanAfterMerge)
                    prematureCleanAfterMerge++;
                if (!animator ||
                    !animator.avatar ||
                    !animator.avatar.isValid ||
                    !animator.avatar.isHuman ||
                    !animator.runtimeAnimatorController)
                    missingAnimator++;

                string preset =
                    adapter.CharacterPreset
                        ? adapter.CharacterPreset.name
                        : adapter.PresetName;

                bool allowed =
                    identity.Presentation ==
                    H51CitizenPresentation.Female
                        ? femaleAllowed.Contains(preset)
                        : maleAllowed.Contains(preset);

                if (!allowed ||
                    adapter.ConfiguredPresentation !=
                        identity.Presentation)
                    badPresetSex++;

                Renderer[] renderers =
                    identity.GetComponentsInChildren<Renderer>(true);
                for (int r=0;r<renderers.Length;r++)
                {
                    Renderer renderer = renderers[r];
                    if (!renderer ||
                        renderer.transform == visual ||
                        renderer.transform.IsChildOf(visual))
                        continue;

                    if (renderer.enabled &&
                        !renderer.forceRenderingOff)
                        legacyRendererStillVisible++;
                }

                H51CitizenVisualVariation variation =
                    identity.GetComponent<H51CitizenVisualVariation>();
                if (variation && variation.enabled)
                    legacyVariationEnabled++;
            }

            bool controllersReady =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    MaleControllerPath) &&
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    FemaleControllerPath);

            Material animeSourceMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/BoZo_StylizedModularCharacters/Materials/BSMC_AnimeShader.mat");
            bool npcOutlineDisabled =
                animeSourceMaterial &&
                (!animeSourceMaterial.HasProperty("_UseOutline") ||
                 animeSourceMaterial.GetFloat("_UseOutline") < .5f) &&
                !animeSourceMaterial.IsKeywordEnabled("_USEOUTLINE");

            bool shadersRetained =
                BoZoShadersRetained();

            bool ok =
                citizens.Length > 0 &&
                unknown == 0 &&
                missingVisual == 0 &&
                missingOutfitSystem == 0 &&
                missingAnimator == 0 &&
                badPresetSex == 0 &&
                legacyRendererStillVisible == 0 &&
                legacyVariationEnabled == 0 &&
                prematureCleanAfterMerge == 0 &&
                controllersReady &&
                shadersRetained &&
                npcOutlineDisabled;

            if (ok)
            {
                Debug.Log(
                    "Step 163 passed. Citizens=" + citizens.Length +
                    ", male=" + male +
                    ", female=" + female +
                    ", Unknown=0, BoZo visuals ready, Humanoid animators ready, texture-bake timing safe, runtime shaders retained, NPC outline disabled, legacy renderers hidden, and male/female preset separation verified.");
            }
            else
            {
                Debug.LogError(
                    "Step 163 failed. Citizens=" + citizens.Length +
                    ", male=" + male +
                    ", female=" + female +
                    ", Unknown=" + unknown +
                    ", missing visual=" + missingVisual +
                    ", missing OutfitSystem=" + missingOutfitSystem +
                    ", missing/invalid Animator=" + missingAnimator +
                    ", male/female preset violations=" + badPresetSex +
                    ", visible legacy renderers=" + legacyRendererStillVisible +
                    ", legacy variation still enabled=" + legacyVariationEnabled +
                    ", premature cleanAfterMerge=" + prematureCleanAfterMerge +
                    ", controllers ready=" + controllersReady +
                    ", runtime shaders retained=" + shadersRetained +
                    ", NPC outline disabled=" + npcOutlineDisabled + ".");
            }
        }

        static void ConfigureBoZoBuildShaders()
        {
            string[] requiredNames =
            {
                "Hidden/TextureLayerer",
                "Hidden/BoZo_Decal"
            };

            UnityEngine.Object graphicsSettings =
                GraphicsSettings.GetGraphicsSettings();
            if (!graphicsSettings)
            {
                Debug.LogWarning(
                    "Step 162 could not access Unity Graphics Settings to retain BoZo runtime shaders.");
                return;
            }

            SerializedObject serialized =
                new SerializedObject(graphicsSettings);
            SerializedProperty alwaysIncluded =
                serialized.FindProperty("m_AlwaysIncludedShaders");

            if (alwaysIncluded == null || !alwaysIncluded.isArray)
            {
                Debug.LogWarning(
                    "Step 162 could not find Unity's Always Included Shaders list.");
                return;
            }

            for (int i=0;i<requiredNames.Length;i++)
            {
                Shader shader = Shader.Find(requiredNames[i]);
                if (!shader)
                {
                    Debug.LogWarning(
                        "Step 162 could not resolve BoZo runtime shader '" +
                        requiredNames[i] +
                        "'.");
                    continue;
                }

                bool found = false;
                for (int index=0;index<alwaysIncluded.arraySize;index++)
                {
                    SerializedProperty element =
                        alwaysIncluded.GetArrayElementAtIndex(index);
                    if (element.objectReferenceValue == shader)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;

                int newIndex = alwaysIncluded.arraySize;
                alwaysIncluded.InsertArrayElementAtIndex(newIndex);
                alwaysIncluded.GetArrayElementAtIndex(newIndex)
                    .objectReferenceValue = shader;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static bool BoZoShadersRetained()
        {
            UnityEngine.Object graphicsSettings =
                GraphicsSettings.GetGraphicsSettings();
            if (!graphicsSettings)
                return false;

            SerializedObject serialized =
                new SerializedObject(graphicsSettings);
            SerializedProperty alwaysIncluded =
                serialized.FindProperty("m_AlwaysIncludedShaders");

            if (alwaysIncluded == null || !alwaysIncluded.isArray)
                return false;

            HashSet<string> retained =
                new HashSet<string>(StringComparer.Ordinal);

            for (int index=0;index<alwaysIncluded.arraySize;index++)
            {
                Shader shader =
                    alwaysIncluded.GetArrayElementAtIndex(index)
                        .objectReferenceValue as Shader;
                if (shader)
                    retained.Add(shader.name);
            }

            Material animeMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/BoZo_StylizedModularCharacters/Materials/BSMC_AnimeShader.mat");

            bool animeShaderReferenced =
                animeMaterial &&
                animeMaterial.shader &&
                animeMaterial.shader.name == "BoZo/BMAC_Toon";

            return
                retained.Contains("Hidden/TextureLayerer") &&
                retained.Contains("Hidden/BoZo_Decal") &&
                animeShaderReferenced;
        }

        static void DisableBoZoNpcOutlineOnSourceMaterial()
        {
            const string animeMaterialPath =
                "Assets/BoZo_StylizedModularCharacters/Materials/BSMC_AnimeShader.mat";

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    animeMaterialPath);
            if (!material)
                return;

            if (material.HasProperty("_UseOutline"))
                material.SetFloat("_UseOutline",0f);
            if (material.HasProperty("_OutlineDistance"))
                material.SetFloat("_OutlineDistance",0f);

            material.DisableKeyword("_USEOUTLINE");
            EditorUtility.SetDirty(material);
        }

        static CharacterObject[] LoadPresets(
            string[] names)
        {
            CharacterObject[] result =
                new CharacterObject[names.Length];

            for (int i=0;i<names.Length;i++)
            {
                string path =
                    "Assets/BoZo_StylizedModularCharacters/CustomCharacters/Resources/Anime/" +
                    names[i] + ".asset";

                result[i] =
                    AssetDatabase.LoadAssetAtPath<CharacterObject>(
                        path);
            }

            return result;
        }

        static AnimatorController BuildController(
            string path,
            string displayName,
            AnimationClip idle,
            AnimationClip walk)
        {
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    path);
            if (existing)
                AssetDatabase.DeleteAsset(path);

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    path);
            controller.name = displayName;

            controller.AddParameter(
                "Speed",
                AnimatorControllerParameterType.Float);
            controller.AddParameter(
                "Wave",
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                "Interact",
                AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine =
                controller.layers[0].stateMachine;

            AnimatorState idleState =
                machine.AddState("Idle");
            idleState.motion = idle;
            idleState.writeDefaultValues = true;

            AnimatorState walkState =
                machine.AddState("Walk");
            walkState.motion = walk;
            walkState.speed = 1f;
            walkState.writeDefaultValues = true;

            machine.defaultState = idleState;

            AnimatorStateTransition toWalk =
                idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = .16f;
            toWalk.AddCondition(
                AnimatorConditionMode.Greater,
                .10f,
                "Speed");

            AnimatorStateTransition toIdle =
                walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = .16f;
            toIdle.AddCondition(
                AnimatorConditionMode.Less,
                .08f,
                "Speed");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void PatchCitizenBrainAnimationReferences(
            H51CitizenIdentity[] citizens)
        {
            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity identity = citizens[i];
                if (!identity) continue;

                H51CitizenBrain brain =
                    identity.GetComponent<H51CitizenBrain>();
                if (brain)
                    EditorUtility.SetDirty(brain);
            }
        }

        static H51CitizenPresentation PresentationForName(
            string displayName,
            int seed)
        {
            string first =
                FirstName(displayName);

            if (FemaleFirstNames.Contains(first))
                return H51CitizenPresentation.Female;
            if (MaleFirstNames.Contains(first))
                return H51CitizenPresentation.Male;

            // Existing Hanger 51 citizens did not persist presentation in the
            // original population pass. For any uncommon name not covered by
            // the deterministic first-name map, choose once from the persistent
            // appearance seed and immediately store the result on the identity.
            return (seed & 1) == 0
                ? H51CitizenPresentation.Male
                : H51CitizenPresentation.Female;
        }

        static string FirstName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.Empty;

            string trimmed = displayName.Trim();
            int space = trimmed.IndexOf(' ');
            return space > 0
                ? trimmed.Substring(0,space)
                : trimmed;
        }

        static int PositiveMod(int value,int modulus)
        {
            if (modulus <= 0) return 0;
            int result = value % modulus;
            return result < 0
                ? result + modulus
                : result;
        }

        static Transform DirectChild(
            Transform root,
            string objectName)
        {
            if (!root) return null;

            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child && child.name == objectName)
                    return child;
            }

            return null;
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path == "Assets" ||
                AssetDatabase.IsValidFolder(path))
                return;

            string parent =
                System.IO.Path.GetDirectoryName(path)
                    ?.Replace("\\","/");
            string folder =
                System.IO.Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) ||
                string.IsNullOrWhiteSpace(folder))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,folder);
        }
    }
}
