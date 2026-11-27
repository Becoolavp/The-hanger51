using System;
using System.Collections.Generic;
using System.Linq;
using Bozo.ModularCharacters;
using Hanger51.NPCs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51BoZoCivilianWardrobeRepair
    {
        const string VisualRootName = "H51 BoZo Citizen Visual";
        const string GeneratedFolder =
            "Assets/_Project/Characters/H51Citizens/BoZoGenerated/CivilianPresets";
        const string MaleBasePath =
            "Assets/BoZo_StylizedModularCharacters/CustomCharacters/Resources/Anime/Default_Boy.asset";
        const string FemaleBasePath =
            "Assets/BoZo_StylizedModularCharacters/CustomCharacters/Resources/Anime/Default_Girl.asset";
        const int CasualVariantCount = 4;

        enum WardrobeRole
        {
            Casual,
            Farmer,
            Worker,
            Medical,
            Business,
            PublicSafety,
            Professional
        }

        readonly struct HairPair
        {
            public readonly string front;
            public readonly string back;

            public HairPair(string front, string back)
            {
                this.front = front;
                this.back = back;
            }
        }

        readonly struct WardrobeRecipe
        {
            public readonly string top;
            public readonly string bottom;
            public readonly string feet;
            public readonly Color topPrimary;
            public readonly Color topSecondary;
            public readonly Color bottomPrimary;
            public readonly Color bottomSecondary;
            public readonly Color feetPrimary;
            public readonly Color feetSecondary;

            public WardrobeRecipe(
                string top,
                string bottom,
                string feet,
                Color topPrimary,
                Color topSecondary,
                Color bottomPrimary,
                Color bottomSecondary,
                Color feetPrimary,
                Color feetSecondary)
            {
                this.top = top;
                this.bottom = bottom;
                this.feet = feet;
                this.topPrimary = topPrimary;
                this.topSecondary = topSecondary;
                this.bottomPrimary = bottomPrimary;
                this.bottomSecondary = bottomSecondary;
                this.feetPrimary = feetPrimary;
                this.feetSecondary = feetSecondary;
            }
        }

        static readonly HairPair[] MaleHair =
        {
            new HairPair(
                "HairFront/HairFront_LynxFringe",
                "HairBack/HairBack_MessyHair"),
            new HairPair(
                "HairFront/HairFront_CurtainBangs",
                "HairBack/HairBack_CasualFlow"),
            new HairPair(
                "HairFront/HairFront_SweaptBack",
                "HairBack/HairBack_ShotaCut"),
            new HairPair(
                "HairFront/HairFront_SideSwept",
                "HairBack/HairBack_PineappleCut")
        };

        static readonly HairPair[] FemaleHair =
        {
            new HairPair(
                "HairFront/HairFront_Shoujo",
                "HairBack/HairBack_LongStreight"),
            new HairPair(
                "HairFront/HairFront_CurtainBangs",
                "HairBack/HairBack_RoundBob"),
            new HairPair(
                "HairFront/HairFront_SideSwept",
                "HairBack/HairBack_LongPonyTail"),
            new HairPair(
                "HairFront/HairFront_MinorFringe",
                "HairBack/HairBack_TiedBun")
        };

        static readonly Color[][] SkinPalettes =
        {
            new[]
            {
                C(0.97f,0.80f,0.69f),
                C(0.91f,0.58f,0.44f),
                C(0.49f,0.20f,0.11f)
            },
            new[]
            {
                C(0.90f,0.68f,0.53f),
                C(0.79f,0.48f,0.34f),
                C(0.40f,0.17f,0.10f)
            },
            new[]
            {
                C(0.76f,0.54f,0.39f),
                C(0.62f,0.38f,0.26f),
                C(0.31f,0.14f,0.09f)
            },
            new[]
            {
                C(0.59f,0.39f,0.28f),
                C(0.44f,0.27f,0.18f),
                C(0.23f,0.11f,0.07f)
            },
            new[]
            {
                C(0.43f,0.28f,0.20f),
                C(0.31f,0.18f,0.12f),
                C(0.16f,0.08f,0.05f)
            }
        };

        static readonly Color[][] HairPalettes =
        {
            new[] { C(.10f,.07f,.05f), C(.18f,.11f,.07f), C(.05f,.03f,.02f) },
            new[] { C(.26f,.14f,.08f), C(.38f,.23f,.14f), C(.12f,.06f,.03f) },
            new[] { C(.48f,.30f,.17f), C(.60f,.42f,.24f), C(.25f,.14f,.07f) },
            new[] { C(.67f,.55f,.32f), C(.80f,.70f,.47f), C(.40f,.30f,.14f) },
            new[] { C(.37f,.16f,.09f), C(.55f,.25f,.13f), C(.20f,.07f,.03f) },
            new[] { C(.43f,.43f,.43f), C(.60f,.60f,.60f), C(.25f,.25f,.25f) }
        };

        static readonly Color[] EyeColors =
        {
            C(.22f,.12f,.07f),
            C(.16f,.30f,.48f),
            C(.18f,.38f,.25f),
            C(.35f,.30f,.13f),
            C(.35f,.38f,.40f)
        };

        [MenuItem("Hanger 51/World/Current/164 - Apply Human Civilian BoZo Wardrobes")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 164 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            CharacterObject maleBase =
                AssetDatabase.LoadAssetAtPath<CharacterObject>(MaleBasePath);
            CharacterObject femaleBase =
                AssetDatabase.LoadAssetAtPath<CharacterObject>(FemaleBasePath);

            if (!maleBase || !femaleBase)
            {
                Debug.LogError(
                    "Step 164 failed. Default_Boy and/or Default_Girl are missing from the imported BoZo package.");
                return;
            }

            EnsureFolder(GeneratedFolder);

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(c => c && c.gameObject.scene.IsValid())
                    .ToArray();

            int repaired = 0;
            int male = 0;
            int female = 0;
            Dictionary<WardrobeRole,int> roleCounts =
                new Dictionary<WardrobeRole,int>();

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity identity = citizens[i];
                if (!identity)
                    continue;

                H51BoZoCitizenVisual adapter =
                    identity.GetComponent<H51BoZoCitizenVisual>();
                Transform visual =
                    DirectChild(identity.transform,VisualRootName);
                OutfitSystem outfit =
                    visual ? visual.GetComponent<OutfitSystem>() : null;
                Animator animator =
                    visual ? visual.GetComponent<Animator>() : null;

                if (!adapter || !visual || !outfit || !animator)
                {
                    Debug.LogError(
                        "Step 164 skipped " + identity.DisplayName +
                        " because the Step 162 BoZo visual is missing. Run Step 162 first.",
                        identity);
                    continue;
                }

                H51CitizenPresentation presentation =
                    identity.Presentation;
                if (presentation == H51CitizenPresentation.Unknown)
                {
                    Debug.LogError(
                        "Step 164 skipped " + identity.DisplayName +
                        " because presentation is Unknown. Run Step 162 first.",
                        identity);
                    continue;
                }

                bool isFemale =
                    presentation == H51CitizenPresentation.Female;
                if (isFemale) female++;
                else male++;

                WardrobeRole role =
                    ClassifyRole(identity.Occupation);
                if (!roleCounts.ContainsKey(role))
                    roleCounts[role] = 0;
                roleCounts[role]++;

                CharacterObject basePreset =
                    isFemale ? femaleBase : maleBase;
                CharacterObject workPreset =
                    CreateCitizenPreset(
                        identity,
                        basePreset,
                        isFemale,
                        role,
                        string.Empty,
                        0);

                CharacterObject[] casualPresets =
                    new CharacterObject[CasualVariantCount];

                for (int variant=0;variant<CasualVariantCount;variant++)
                {
                    casualPresets[variant] =
                        CreateCitizenPreset(
                            identity,
                            basePreset,
                            isFemale,
                            WardrobeRole.Casual,
                            "_Casual_" + variant,
                            variant);
                }

                if (!workPreset ||
                    casualPresets.Any(p => !p))
                    continue;

                Undo.RecordObject(
                    outfit,
                    "Apply Hanger 51 civilian BoZo presets");
                outfit.characterData = workPreset;
                outfit.loadMode = OutfitSystem.LoadMode.OnStart;
                outfit.async = false;
                outfit.cleanAfterMerge = false;
                outfit._textureSize =
                    OutfitSystem.TextureSize._512;
                EditorUtility.SetDirty(outfit);

                adapter.ConfigureWardrobes(
                    presentation,
                    workPreset,
                    casualPresets,
                    outfit,
                    animator);
                EditorUtility.SetDirty(adapter);
                repaired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            string roles = string.Join(
                ", ",
                roleCounts
                    .OrderBy(p => p.Key.ToString())
                    .Select(p => p.Key + "=" + p.Value));

            Debug.Log(
                "Step 164 complete. Human civilian BoZo presets applied=" +
                repaired + "/" + citizens.Length +
                ", male=" + male +
                ", female=" + female +
                ". Each citizen now has one occupation/work preset plus " +
                CasualVariantCount +
                " deterministic off-duty presets. Work clothing is used at work/going to work; casual clothing rotates by game day. Fantasy head accessories/demo presets were removed. Every generated preset has a Top, Bottom, and Feet outfit. Roles: " +
                roles +
                ". Run Step 165.");
        }

        [MenuItem("Hanger 51/World/Current/165 - Validate Human Civilian BoZo Wardrobes")]
        public static void Validate()
        {
            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(c => c && c.gameObject.scene.IsValid())
                    .ToArray();

            int missingVisual = 0;
            int nonGeneratedPreset = 0;
            int wrongPresentationBase = 0;
            int missingTop = 0;
            int missingBottom = 0;
            int missingFeet = 0;
            int fantasyParts = 0;
            int unsafePupil = 0;
            int staleSaveVersion = 0;
            int invalidOutfitAlpha = 0;
            int missingOutfitResource = 0;
            int prematureClean = 0;
            int visualPhysics = 0;

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity identity = citizens[i];
                if (!identity)
                    continue;

                H51BoZoCitizenVisual adapter =
                    identity.GetComponent<H51BoZoCitizenVisual>();
                Transform visual =
                    DirectChild(identity.transform,VisualRootName);
                OutfitSystem outfit =
                    visual ? visual.GetComponent<OutfitSystem>() : null;

                if (!adapter || !visual || !outfit || !adapter.CharacterPreset)
                {
                    missingVisual++;
                    continue;
                }

                string presetPath =
                    AssetDatabase.GetAssetPath(adapter.CharacterPreset);
                if (string.IsNullOrWhiteSpace(presetPath) ||
                    !presetPath.StartsWith(
                        GeneratedFolder + "/",
                        StringComparison.Ordinal))
                    nonGeneratedPreset++;

                bool female =
                    identity.Presentation ==
                    H51CitizenPresentation.Female;
                string expectedPrefix =
                    female ? "H51_F_" : "H51_M_";
                if (!adapter.CharacterPreset.name.StartsWith(
                        expectedPrefix,
                        StringComparison.Ordinal))
                    wrongPresentationBase++;

                CharacterData data =
                    adapter.CharacterPreset.GetCharacterData();

                if (data == null || data.versionID < 4)
                    staleSaveVersion++;

                if (data != null && data.outfitDatas != null)
                {
                    for (int d=0;d<data.outfitDatas.Count;d++)
                    {
                        OutfitData item =
                            data.outfitDatas[d];

                        if (item.alpha < .999f)
                            invalidOutfitAlpha++;

                        if (!string.IsNullOrWhiteSpace(item.outfit) &&
                            !Resources.Load<Outfit>(item.outfit))
                            missingOutfitResource++;
                    }
                }

                List<string> paths =
                    data != null && data.outfitDatas != null
                        ? data.outfitDatas
                            .Where(d => !string.IsNullOrWhiteSpace(d.outfit))
                            .Select(d => d.outfit)
                            .ToList()
                        : new List<string>();

                if (!paths.Any(p => p.StartsWith("Top/",StringComparison.Ordinal)))
                    missingTop++;
                if (!paths.Any(p => p.StartsWith("Bottom/",StringComparison.Ordinal)))
                    missingBottom++;
                if (!paths.Any(p => p.StartsWith("Feet/",StringComparison.Ordinal)))
                    missingFeet++;

                if (paths.Any(IsFantasyOrDemoOnlyPath))
                    fantasyParts++;

                if (paths.Any(p =>
                    p.StartsWith("Pupil/",StringComparison.Ordinal) &&
                    !string.Equals(
                        p,
                        "Pupil/Pupil_Round",
                        StringComparison.Ordinal)))
                    unsafePupil++;

                if (outfit.cleanAfterMerge)
                    prematureClean++;

                Collider[] colliders =
                    visual.GetComponentsInChildren<Collider>(true);
                visualPhysics += colliders.Count(c => c && c.enabled);

                CharacterController[] controllers =
                    visual.GetComponentsInChildren<CharacterController>(true);
                visualPhysics += controllers.Count(c => c && c.enabled);

                Rigidbody[] bodies =
                    visual.GetComponentsInChildren<Rigidbody>(true);
                visualPhysics += bodies.Count(b =>
                    b && (!b.isKinematic || b.detectCollisions || b.useGravity));
            }

            bool ok =
                citizens.Length > 0 &&
                missingVisual == 0 &&
                nonGeneratedPreset == 0 &&
                wrongPresentationBase == 0 &&
                missingTop == 0 &&
                missingBottom == 0 &&
                missingFeet == 0 &&
                fantasyParts == 0 &&
                unsafePupil == 0 &&
                staleSaveVersion == 0 &&
                invalidOutfitAlpha == 0 &&
                missingOutfitResource == 0 &&
                prematureClean == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 165 passed. Citizens=" + citizens.Length +
                    ". All use generated Hanger 51 civilian presets with correct male/female bases, mandatory Top/Bottom/Feet clothing, human-only accessories/pupils, BoZo v4 alpha=1 on every outfit layer, valid Resources paths, and safe texture-bake settings. Scene-time visual physics needing runtime neutralization=" +
                    visualPhysics + ".");
            }
            else
            {
                Debug.LogError(
                    "Step 165 failed. Citizens=" + citizens.Length +
                    ", missing visual/preset=" + missingVisual +
                    ", non-generated demo preset=" + nonGeneratedPreset +
                    ", wrong presentation base=" + wrongPresentationBase +
                    ", missing Top=" + missingTop +
                    ", missing Bottom=" + missingBottom +
                    ", missing Feet=" + missingFeet +
                    ", fantasy/demo parts=" + fantasyParts +
                    ", unsafe pupils=" + unsafePupil +
                    ", stale save versions=" + staleSaveVersion +
                    ", invalid outfit alpha layers=" + invalidOutfitAlpha +
                    ", missing outfit Resources=" + missingOutfitResource +
                    ", premature cleanAfterMerge=" + prematureClean +
                    ", scene-time visual physics=" + visualPhysics + ".");
            }
        }

        static CharacterObject CreateCitizenPreset(
            H51CitizenIdentity identity,
            CharacterObject basePreset,
            bool female,
            WardrobeRole role,
            string assetSuffix,
            int wardrobeSeedOffset)
        {
            string id =
                !string.IsNullOrWhiteSpace(identity.CitizenId)
                    ? identity.CitizenId
                    : identity.DisplayName;
            string assetName =
                (female ? "H51_F_" : "H51_M_") +
                SafeName(id) +
                (assetSuffix ?? string.Empty);
            string assetPath =
                GeneratedFolder + "/" + assetName + ".asset";

            if (AssetDatabase.LoadAssetAtPath<CharacterObject>(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            CharacterData source =
                basePreset.GetCharacterData();
            CharacterData data =
                CloneCharacterData(source);

            int seed =
                identity.AppearanceSeed;
            int skinIndex =
                PositiveMod(seed / 7,SkinPalettes.Length);
            int hairIndex =
                identity.Age >= 58
                    ? HairPalettes.Length - 1
                    : PositiveMod(seed / 11,HairPalettes.Length - 1);
            int eyeIndex =
                PositiveMod(seed / 13,EyeColors.Length);

            Color[] skin =
                SkinPalettes[skinIndex];
            Color[] hair =
                HairPalettes[hairIndex];
            Color eye =
                EyeColors[eyeIndex];

            data.versionID = 4;
            data.characterName = assetName;
            data.material = "BSMC_AnimeShader";

            List<OutfitData> retained =
                new List<OutfitData>();

            if (data.outfitDatas != null)
            {
                for (int i=0;i<data.outfitDatas.Count;i++)
                {
                    OutfitData item =
                        CloneOutfitData(data.outfitDatas[i]);

                    // BoZo version 4 changed outfit alpha semantics. Its own
                    // Update4 patch upgrades every legacy outfit to alpha=1.
                    // Because Hanger 51 writes version 4 presets directly, we
                    // must perform that normalization ourselves or the merged
                    // body/clothing texture layers render black/invisible.
                    item.alpha = 1f;

                    string category =
                        Category(item.outfit);

                    if (category == "HairFront" ||
                        category == "HairBack" ||
                        category == "Pupil" ||
                        category == "Top" ||
                        category == "Bottom" ||
                        category == "Feet" ||
                        category == "Overall" ||
                        category == "HeadAcc" ||
                        category == "Hat" ||
                        category == "Gloves" ||
                        category == "UpperFace" ||
                        category == "LowerFace" ||
                        category == "Neck" ||
                        category == "Waist" ||
                        category == "Shoulders" ||
                        category == "Leggings" ||
                        category.StartsWith("MakeUp",StringComparison.Ordinal))
                        continue;

                    if (category == "Head" ||
                        category == "Body")
                        ApplySkin(item.colors,skin);

                    if (category == "Eyes")
                        ApplyEyeColor(item.colors,eye);

                    if (category == "EyeBrows")
                        ApplyHairColor(item.colors,hair);

                    retained.Add(item);
                }
            }

            HairPair[] hairPool =
                female ? FemaleHair : MaleHair;
            HairPair pair =
                hairPool[PositiveMod(seed,hairPool.Length)];

            retained.Add(
                MakeOutfit(
                    pair.front,
                    hair[0],hair[1],hair[2]));
            retained.Add(
                MakeOutfit(
                    pair.back,
                    hair[0],hair[1],hair[2]));
            retained.Add(
                MakeOutfit(
                    "Pupil/Pupil_Round",
                    Color.white,Color.black,Color.black));

            WardrobeRecipe recipe =
                SelectWardrobe(
                    female,
                    role,
                    seed + wardrobeSeedOffset);

            retained.Add(
                MakeWardrobeOutfit(
                    recipe.top,
                    recipe.topPrimary,
                    recipe.topSecondary));
            retained.Add(
                MakeWardrobeOutfit(
                    recipe.bottom,
                    recipe.bottomPrimary,
                    recipe.bottomSecondary));
            retained.Add(
                MakeWardrobeOutfit(
                    recipe.feet,
                    recipe.feetPrimary,
                    recipe.feetSecondary));

            data.outfitDatas = retained;

            CharacterObject generated =
                ScriptableObject.CreateInstance<CharacterObject>();
            generated.name = assetName;
            generated.icon = basePreset.icon;
            generated.data = data;

            AssetDatabase.CreateAsset(
                generated,
                assetPath);
            EditorUtility.SetDirty(generated);
            return generated;
        }

        static WardrobeRole ClassifyRole(string occupation)
        {
            string job =
                (occupation ?? string.Empty)
                    .Trim()
                    .ToLowerInvariant();

            if (ContainsAny(
                job,
                "farm","farmer","ranch","agricultur"))
                return WardrobeRole.Farmer;

            if (ContainsAny(
                job,
                "doctor","nurse","medical","medic",
                "hospital","health"))
                return WardrobeRole.Medical;

            if (ContainsAny(
                job,
                "police","security","firefighter",
                "fire fighter","sheriff","deputy"))
                return WardrobeRole.PublicSafety;

            if (ContainsAny(
                job,
                "mechanic","maintenance","technician",
                "utility","power","plant","airport",
                "ground","construction","worker"))
                return WardrobeRole.Worker;

            if (ContainsAny(
                job,
                "manager","office","bank","clerk",
                "administrator","reception","business"))
                return WardrobeRole.Business;

            if (ContainsAny(
                job,
                "teacher","professor","school","instructor"))
                return WardrobeRole.Professional;

            return WardrobeRole.Casual;
        }

        static WardrobeRecipe SelectWardrobe(
            bool female,
            WardrobeRole role,
            int seed)
        {
            Color navy = C(.10f,.16f,.25f);
            Color charcoal = C(.18f,.19f,.21f);
            Color denim = C(.18f,.30f,.43f);
            Color khaki = C(.43f,.39f,.28f);
            Color olive = C(.24f,.31f,.18f);
            Color brown = C(.23f,.13f,.08f);
            Color tan = C(.49f,.36f,.23f);
            Color gray = C(.35f,.37f,.39f);
            Color lightGray = C(.70f,.72f,.73f);
            Color white = C(.84f,.84f,.80f);
            Color blue = C(.19f,.38f,.58f);
            Color teal = C(.18f,.48f,.48f);
            Color burgundy = C(.38f,.10f,.13f);
            Color forest = C(.15f,.31f,.20f);

            switch (role)
            {
                case WardrobeRole.Farmer:
                    return new WardrobeRecipe(
                        "Top/Top_Overall",
                        "Bottom/Bottom_SimpleKhakies",
                        "Feet/Feet_WorkBoots",
                        denim,white,
                        khaki,brown,
                        brown,tan);

                case WardrobeRole.Worker:
                    return new WardrobeRecipe(
                        PositiveMod(seed,2) == 0
                            ? "Top/Top_HardJacket"
                            : "Top/Top_ThickHenslay",
                        "Bottom/Bottom_BeltedPants",
                        "Feet/Feet_WorkBoots",
                        PositiveMod(seed,3) == 0 ? olive : navy,
                        lightGray,
                        charcoal,gray,
                        brown,tan);

                case WardrobeRole.Medical:
                    return new WardrobeRecipe(
                        "Top/Top_ShortSleeveDressShirt",
                        "Bottom/Bottom_TightDressPants",
                        female
                            ? "Feet/Feet_OfficeLoafer"
                            : "Feet/Feet_ClassyLoafers",
                        PositiveMod(seed,2) == 0 ? teal : blue,
                        white,
                        navy,gray,
                        charcoal,gray);

                case WardrobeRole.Business:
                    if (female && PositiveMod(seed,2) == 0)
                    {
                        return new WardrobeRecipe(
                            "Top/Top_ComfyCartagan",
                            "Bottom/Bottom_LongSimpleSkirt",
                            "Feet/Feet_OfficeLoafer",
                            burgundy,white,
                            charcoal,gray,
                            charcoal,gray);
                    }

                    return new WardrobeRecipe(
                        "Top/Top_FullSuit",
                        "Bottom/Bottom_TightDressPants",
                        female
                            ? "Feet/Feet_OfficeLoafer"
                            : "Feet/Feet_ClassyLoafers",
                        PositiveMod(seed,2) == 0 ? navy : charcoal,
                        white,
                        charcoal,gray,
                        charcoal,brown);

                case WardrobeRole.PublicSafety:
                    return new WardrobeRecipe(
                        "Top/Top_HardJacket",
                        "Bottom/Bottom_TightDressPants",
                        "Feet/Feet_WorkBoots",
                        navy,lightGray,
                        navy,charcoal,
                        charcoal,brown);

                case WardrobeRole.Professional:
                    if (female)
                    {
                        return new WardrobeRecipe(
                            "Top/Top_ComfyCartagan",
                            "Bottom/Bottom_LongSimpleSkirt",
                            "Feet/Feet_OfficeLoafer",
                            forest,white,
                            charcoal,gray,
                            brown,charcoal);
                    }

                    return new WardrobeRecipe(
                        "Top/Top_DressShirt",
                        "Bottom/Bottom_TightDressPants",
                        "Feet/Feet_ClassyLoafers",
                        white,blue,
                        charcoal,gray,
                        brown,charcoal);
            }

            int casual =
                PositiveMod(seed,4);

            if (female)
            {
                if (casual == 0)
                    return new WardrobeRecipe(
                        "Top/Top_SimpleSweater",
                        "Bottom/Bottom_SkinnyJeans",
                        "Feet/Feet_SimpleSneakers",
                        burgundy,white,
                        denim,navy,
                        white,gray);

                if (casual == 1)
                    return new WardrobeRecipe(
                        "Top/Top_ComfyCartagan",
                        "Bottom/Bottom_LongSimpleSkirt",
                        "Feet/Feet_BalletFlats",
                        forest,white,
                        charcoal,gray,
                        brown,tan);

                if (casual == 2)
                    return new WardrobeRecipe(
                        "Top/Top_DressShirt",
                        "Bottom/Bottom_TightDressPants",
                        "Feet/Feet_OfficeLoafer",
                        blue,white,
                        charcoal,gray,
                        charcoal,brown);

                return new WardrobeRecipe(
                    "Top/Top_SimpleHoodie",
                    "Bottom/Bottom_SimpleKhakies",
                    "Feet/Feet_SimpleSneakers",
                    gray,white,
                    khaki,brown,
                    white,gray);
            }

            if (casual == 0)
                return new WardrobeRecipe(
                    "Top/Top_Tshirt",
                    "Bottom/Bottom_SkinnyJeans",
                    "Feet/Feet_SimpleSneakers",
                    blue,white,
                    denim,navy,
                    white,gray);

            if (casual == 1)
                return new WardrobeRecipe(
                    "Top/Top_SimpleHoodie",
                    "Bottom/Bottom_SimpleKhakies",
                    "Feet/Feet_SimpleSneakers",
                    gray,white,
                    khaki,brown,
                    white,charcoal);

            if (casual == 2)
                return new WardrobeRecipe(
                    "Top/Top_ThickHenslay",
                    "Bottom/Bottom_BeltedPants",
                    "Feet/Feet_WorkBoots",
                    forest,white,
                    charcoal,gray,
                    brown,tan);

            return new WardrobeRecipe(
                "Top/Top_DressShirt",
                "Bottom/Bottom_TightDressPants",
                "Feet/Feet_ClassyLoafers",
                white,blue,
                charcoal,gray,
                brown,charcoal);
        }

        static CharacterData CloneCharacterData(
            CharacterData source)
        {
            CharacterData data =
                new CharacterData();

            data.versionID =
                source != null ? source.versionID : 4;
            data.characterName =
                source != null ? source.characterName : string.Empty;
            data.bodyIDs =
                source?.bodyIDs != null
                    ? new List<string>(source.bodyIDs)
                    : new List<string>();
            data.bodyShapes =
                source?.bodyShapes != null
                    ? new List<float>(source.bodyShapes)
                    : new List<float>();
            data.faceIDs =
                source?.faceIDs != null
                    ? new List<string>(source.faceIDs)
                    : new List<string>();
            data.faceShapes =
                source?.faceShapes != null
                    ? new List<float>(source.faceShapes)
                    : new List<float>();
            data.bodyModsKeys =
                source?.bodyModsKeys != null
                    ? new List<string>(source.bodyModsKeys)
                    : new List<string>();
            data.bodyMods =
                source?.bodyMods != null
                    ? new List<BodyModData>(source.bodyMods)
                    : new List<BodyModData>();
            data.outfitDatas =
                source?.outfitDatas != null
                    ? source.outfitDatas
                        .Select(CloneOutfitData)
                        .ToList()
                    : new List<OutfitData>();
            data.bodyData =
                source != null
                    ? CloneOutfitData(source.bodyData)
                    : new OutfitData();
            data.stance =
                source != null ? source.stance : 0f;
            data.material =
                source != null &&
                !string.IsNullOrWhiteSpace(source.material)
                    ? source.material
                    : "BSMC_AnimeShader";

            return data;
        }

        static OutfitData CloneOutfitData(
            OutfitData source)
        {
            return new OutfitData
            {
                outfit = source.outfit,
                colors =
                    source.colors != null
                        ? new List<Color>(source.colors)
                        : new List<Color>(),
                alpha = source.alpha,
                decal = source.decal,
                decalColors =
                    source.decalColors != null
                        ? new List<Color>(source.decalColors)
                        : new List<Color>(),
                decalScale = source.decalScale,
                pattern = source.pattern,
                patternColors =
                    source.patternColors != null
                        ? new List<Color>(source.patternColors)
                        : new List<Color>(),
                patternScale = source.patternScale,
                partVisibility =
                    source.partVisibility != null
                        ? (bool[])source.partVisibility.Clone()
                        : Array.Empty<bool>(),
                color = source.color,
                swatch = source.swatch,
                decalDatas =
                    source.decalDatas != null
                        ? new List<DecalData>(source.decalDatas)
                        : new List<DecalData>()
            };
        }

        static OutfitData MakeWardrobeOutfit(
            string path,
            Color primary,
            Color secondary)
        {
            Color metal = C(.42f,.44f,.46f);
            Color dark = Darken(primary,.38f);
            Color darker = Darken(primary,.58f);
            Color light = Lighten(primary,.28f);
            Color warmBrown = C(.22f,.12f,.07f);
            Color white = C(.88f,.88f,.84f);
            Color accent = secondary;

            Color[] channels =
                Enumerable.Repeat(Color.black,9).ToArray();

            switch (path)
            {
                case "Top/Top_Overall":
                    channels[0] = secondary;
                    channels[1] = primary;
                    channels[2] = metal;
                    channels[3] = secondary;
                    channels[4] = primary;
                    break;

                case "Top/Top_HardJacket":
                    channels[0] = primary;
                    channels[1] = dark;
                    channels[2] = metal;
                    channels[3] = secondary;
                    channels[4] = Lighten(secondary,.12f);
                    break;

                case "Top/Top_ThickHenslay":
                    channels[0] = primary;
                    channels[1] = dark;
                    channels[2] = secondary;
                    break;

                case "Top/Top_ShortSleeveDressShirt":
                case "Top/Top_DressShirt":
                    channels[0] = primary;
                    channels[1] = secondary;
                    channels[2] = light;
                    break;

                case "Top/Top_ComfyCartagan":
                    channels[0] = primary;
                    channels[1] = secondary;
                    channels[2] = Lighten(primary,.20f);
                    break;

                case "Top/Top_FullSuit":
                    channels[0] = primary;
                    channels[1] = primary;
                    channels[2] = metal;
                    channels[3] = dark;
                    channels[4] = white;
                    channels[5] = accent;
                    break;

                case "Top/Top_SimpleSweater":
                case "Top/Top_SimpleHoodie":
                case "Top/Top_Tshirt":
                    channels[0] = primary;
                    channels[1] = secondary;
                    break;

                case "Bottom/Bottom_SimpleKhakies":
                    channels[0] = primary;
                    channels[1] = dark;
                    channels[2] = metal;
                    break;

                case "Bottom/Bottom_BeltedPants":
                    channels[0] = primary;
                    channels[1] = dark;
                    channels[2] = metal;
                    channels[3] = warmBrown;
                    break;

                case "Bottom/Bottom_TightDressPants":
                    channels[0] = primary;
                    channels[1] = dark;
                    channels[2] = dark;
                    channels[3] = primary;
                    channels[4] = darker;
                    channels[5] = metal;
                    break;

                case "Bottom/Bottom_LongSimpleSkirt":
                    channels[0] = primary;
                    channels[1] = secondary;
                    break;

                case "Bottom/Bottom_SkinnyJeans":
                    channels[0] = primary;
                    channels[1] = dark;
                    channels[2] = metal;
                    break;

                case "Feet/Feet_WorkBoots":
                    channels[0] = primary;
                    channels[1] = secondary;
                    channels[2] = darker;
                    channels[3] = darker;
                    break;

                case "Feet/Feet_OfficeLoafer":
                case "Feet/Feet_ClassyLoafers":
                    channels[0] = primary;
                    channels[1] = darker;
                    channels[2] = secondary;
                    break;

                case "Feet/Feet_SimpleSneakers":
                    channels[0] = primary;
                    channels[1] = secondary;
                    channels[2] = light;
                    break;

                case "Feet/Feet_BalletFlats":
                    channels[0] = primary;
                    channels[1] = darker;
                    channels[2] = metal;
                    break;

                default:
                    channels[0] = primary;
                    channels[1] = secondary;
                    channels[2] = light;
                    break;
            }

            return MakeOutfit(
                path,
                channels);
        }

        static OutfitData MakeOutfit(
            string path,
            params Color[] suppliedColors)
        {
            List<Color> colors =
                Enumerable.Repeat(Color.black,9).ToList();

            if (suppliedColors != null)
            {
                for (int i=0;i<suppliedColors.Length && i<9;i++)
                    colors[i] = suppliedColors[i];
            }

            return new OutfitData
            {
                outfit = path,
                colors = colors,
                alpha = 1f,
                decal = string.Empty,
                decalColors = new List<Color>(),
                decalScale = Vector4.zero,
                pattern = string.Empty,
                patternColors = new List<Color>(),
                patternScale = Vector4.zero,
                partVisibility = Array.Empty<bool>(),
                color = colors[0],
                swatch = 0,
                decalDatas = new List<DecalData>()
            };
        }

        static void ApplySkin(
            List<Color> colors,
            Color[] skin)
        {
            EnsureNineColors(colors);
            colors[0] = skin[0];
            colors[1] = skin[1];
            colors[2] = skin[2];
            colors[3] =
                Color.Lerp(skin[0],C(.78f,.30f,.28f),.12f);
            colors[4] =
                Color.Lerp(skin[0],C(.70f,.22f,.24f),.10f);
        }

        static void ApplyHairColor(
            List<Color> colors,
            Color[] hair)
        {
            EnsureNineColors(colors);
            colors[0] = hair[0];
            colors[1] = hair[1];
            colors[2] = hair[2];
        }

        static void ApplyEyeColor(
            List<Color> colors,
            Color eye)
        {
            EnsureNineColors(colors);
            colors[0] = eye;
            colors[1] = Color.black;
            colors[2] = Darken(eye,.35f);
        }

        static void EnsureNineColors(
            List<Color> colors)
        {
            if (colors == null)
                return;

            while (colors.Count < 9)
                colors.Add(Color.black);
        }

        static bool IsFantasyOrDemoOnlyPath(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return
                path.StartsWith("HeadAcc/",StringComparison.Ordinal) ||
                path.IndexOf("Devil",StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Alien",StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Kitty",StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Yokai",StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("HeartPupil",StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("StarPupil",StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ContainsAny(
            string value,
            params string[] terms)
        {
            for (int i=0;i<terms.Length;i++)
                if (value.Contains(terms[i]))
                    return true;
            return false;
        }

        static string Category(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            int slash = path.IndexOf('/');
            return slash > 0
                ? path.Substring(0,slash)
                : path;
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Citizen";

            char[] chars =
                value.Select(c =>
                    char.IsLetterOrDigit(c) ||
                    c == '_' ||
                    c == '-'
                        ? c
                        : '_')
                    .ToArray();

            string safe =
                new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(safe)
                ? "Citizen"
                : safe;
        }

        static int PositiveMod(
            int value,
            int modulus)
        {
            if (modulus <= 0)
                return 0;
            int result = value % modulus;
            return result < 0
                ? result + modulus
                : result;
        }

        static Color C(
            float r,
            float g,
            float b)
        {
            return new Color(r,g,b,1f);
        }

        static Color Darken(
            Color color,
            float amount)
        {
            return Color.Lerp(
                color,
                Color.black,
                Mathf.Clamp01(amount));
        }

        static Color Lighten(
            Color color,
            float amount)
        {
            return Color.Lerp(
                color,
                Color.white,
                Mathf.Clamp01(amount));
        }

        static Transform DirectChild(
            Transform root,
            string childName)
        {
            if (!root)
                return null;

            for (int i=0;i<root.childCount;i++)
            {
                Transform child =
                    root.GetChild(i);
                if (child && child.name == childName)
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
                System.IO.Path
                    .GetDirectoryName(path)
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
