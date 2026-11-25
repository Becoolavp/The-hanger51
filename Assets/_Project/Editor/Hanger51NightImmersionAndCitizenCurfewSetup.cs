using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Hanger51.NPCs;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51NightImmersionAndCitizenCurfewSetup
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string LightingRootName = "H51 Time Calendar And Night Lighting";
        const string ExternalFolder =
            "Assets/_Project/Environment/Generated/NightSkyExternal";
        const string NightSkyMaterialPath =
            ExternalFolder + "/H51_NightSky.mat";
        const string WarmGlowMaterialPath =
            ExternalFolder + "/H51_WarmLampGlow.mat";
        const string CoolGlowMaterialPath =
            ExternalFolder + "/H51_CoolLampGlow.mat";
        const string SourceNotePath =
            ExternalFolder + "/SOURCE_LICENSE.txt";

        const string AmbientCgAssetPage =
            "https://ambientcg.com/view?id=NightSkyHDRI001";
        const string AmbientCgDownload =
            "https://ambientcg.com/get?file=NightSkyHDRI001_2K.zip";

        [MenuItem("Hanger 51/World/Current/158 - Install Starry Night Sky Lamp Glow And Citizen Curfew")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 158 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = GameObject.Find(WorldName);
            H51DayNightCycle cycle =
                UnityEngine.Object.FindFirstObjectByType<H51DayNightCycle>(
                    FindObjectsInactive.Include);

            if (!world || !cycle)
            {
                Debug.LogError(
                    "Step 158 failed. The current world or H51DayNightCycle is missing. Run Step 152 first.");
                return;
            }

            EnsureFolder(ExternalFolder);

            bool downloadedExternalSky = false;
            string importedNightTexture =
                FindExistingNightSkyTexture();

            if (string.IsNullOrWhiteSpace(importedNightTexture))
            {
                downloadedExternalSky =
                    TryDownloadAmbientCgNightSky(out importedNightTexture);
            }

            Material nightSky =
                BuildNightSkyMaterial(importedNightTexture);

            cycle.ConfigureNightSky(nightSky);

            Transform moon =
                FindDescendant(cycle.transform,"H51 Visible Moon");
            if (moon)
                cycle.ConfigureMoonVisual(moon,900f,48f);

            Material warmGlow = BuildUnlitGlowMaterial(
                WarmGlowMaterialPath,
                new Color(1f,.78f,.34f,1f));
            Material coolGlow = BuildUnlitGlowMaterial(
                CoolGlowMaterialPath,
                new Color(.72f,.88f,1f,1f));

            int lampBulbs = UpgradeLampBulbMaterials(warmGlow,coolGlow);
            int runwayBulbs = UpgradeRunwayBulbMaterials(coolGlow);
            int citizens = ApplyCitizenCurfew();

            WriteSourceNote(
                !string.IsNullOrWhiteSpace(importedNightTexture));

            EditorUtility.SetDirty(cycle);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 158 complete. Night sky=" +
                (!string.IsNullOrWhiteSpace(importedNightTexture)
                    ? "ambientCG NightSkyHDRI001 2K (CC0)"
                    : "procedural fallback") +
                ", downloaded this run=" + downloadedExternalSky +
                ", visibly self-lit town/airport lamp bulbs=" + lampBulbs +
                ", visibly self-lit runway bulbs=" + runwayBulbs +
                ", civilian curfews configured=" + citizens +
                ". Run Step 159 to validate.");
        }

        [MenuItem("Hanger 51/World/Current/159 - Validate Starry Night Sky Lamp Glow And Citizen Curfew")]
        public static void Validate()
        {
            bool passed = true;

            H51DayNightCycle cycle =
                UnityEngine.Object.FindFirstObjectByType<H51DayNightCycle>(
                    FindObjectsInactive.Include);
            Transform moon =
                cycle
                    ? FindDescendant(cycle.transform,"H51 Visible Moon")
                    : null;
            Renderer moonRenderer =
                moon ? moon.GetComponentInChildren<Renderer>(true) : null;

            if (!cycle || !cycle.HasNightSky)
            {
                Debug.LogError(
                    "Step 159 failed: no night sky material is configured on H51DayNightCycle.");
                passed = false;
            }

            if (!moon || !moonRenderer)
            {
                Debug.LogError(
                    "Step 159 failed: the visible moon object/renderer is missing.");
                passed = false;
            }

            H51NightLight[] nightLights =
                UnityEngine.Object.FindObjectsByType<H51NightLight>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int lampLenses = 0;
            int lampUnlit = 0;

            for (int i=0;i<nightLights.Length;i++)
            {
                H51NightLight light = nightLights[i];
                if (!light) continue;

                Renderer[] renderers =
                    light.GetComponentsInChildren<Renderer>(true);
                for (int r=0;r<renderers.Length;r++)
                {
                    Renderer renderer = renderers[r];
                    if (!renderer ||
                        renderer.name.IndexOf(
                            "Lens",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    lampLenses++;
                    if (renderer.sharedMaterial &&
                        renderer.sharedMaterial.shader &&
                        renderer.sharedMaterial.shader.name.IndexOf(
                            "Unlit",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        lampUnlit++;
                }
            }

            if (lampLenses == 0 || lampUnlit != lampLenses)
            {
                Debug.LogError(
                    "Step 159 failed: not every town/airport lamp lens uses a self-lit material. Unlit=" +
                    lampUnlit + "/" + lampLenses + ".");
                passed = false;
            }

            H51CitizenBrain[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int curfewReady = 0;

            for (int i=0;i<citizens.Length;i++)
            {
                SerializedObject so =
                    new SerializedObject(citizens[i]);
                SerializedProperty curfew =
                    so.FindProperty("civilianCurfewHour");
                SerializedProperty morning =
                    so.FindProperty("civilianMorningReleaseHour");

                if (curfew != null &&
                    morning != null &&
                    curfew.floatValue <= 21.5f + .01f &&
                    morning.floatValue >= 6.5f - .01f)
                    curfewReady++;
            }

            if (citizens.Length == 0 ||
                curfewReady != citizens.Length)
            {
                Debug.LogError(
                    "Step 159 failed: civilian curfew settings are incomplete. Ready=" +
                    curfewReady + "/" + citizens.Length + ".");
                passed = false;
            }

            Renderer[] runwayLenses =
                UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(r =>
                        r &&
                        r.name.IndexOf(
                            "Runway Light Lens",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

            int runwayUnlit = runwayLenses.Count(
                r =>
                    r.sharedMaterial &&
                    r.sharedMaterial.shader &&
                    r.sharedMaterial.shader.name.IndexOf(
                        "Unlit",
                        StringComparison.OrdinalIgnoreCase) >= 0);

            if (runwayLenses.Length > 0 &&
                runwayUnlit != runwayLenses.Length)
            {
                Debug.LogError(
                    "Step 159 failed: runway bulb materials are not all self-lit. Unlit=" +
                    runwayUnlit + "/" + runwayLenses.Length + ".");
                passed = false;
            }

            if (passed)
            {
                string skySource =
                    File.Exists(ToAbsolute(SourceNotePath))
                        ? "configured"
                        : "material only";

                Debug.Log(
                    "Step 159 passed. Night sky=" + skySource +
                    ", moon renderer=ready, self-lit lamp bulbs=" +
                    lampUnlit + "/" + lampLenses +
                    ", self-lit runway bulbs=" +
                    runwayUnlit + "/" + runwayLenses.Length +
                    ", civilian curfew=" +
                    curfewReady + "/" + citizens.Length + ".");
            }
        }

        static bool TryDownloadAmbientCgNightSky(
            out string importedAssetPath)
        {
            importedAssetPath = null;

            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return false;

            string downloadDirectory =
                Path.Combine(projectRoot,"Library","H51Downloads");
            Directory.CreateDirectory(downloadDirectory);

            string zipPath =
                Path.Combine(
                    downloadDirectory,
                    "NightSkyHDRI001_2K.zip");
            string targetAbsolute =
                ToAbsolute(ExternalFolder);
            Directory.CreateDirectory(targetAbsolute);

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Night Sky",
                    "Downloading ambientCG Night Sky HDRI 001 (2K, CC0)...",
                    .2f);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(90);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Hanger51-UnityEditor/1.0");

                    byte[] data =
                        client.GetByteArrayAsync(AmbientCgDownload)
                            .GetAwaiter()
                            .GetResult();

                    File.WriteAllBytes(zipPath,data);
                }

                EditorUtility.DisplayProgressBar(
                    "Hanger 51 Night Sky",
                    "Extracting HDR panorama...",
                    .65f);

                ExtractImageFiles(zipPath,targetAbsolute);

                AssetDatabase.Refresh();

                importedAssetPath =
                    FindExistingNightSkyTexture();

                if (!string.IsNullOrWhiteSpace(importedAssetPath))
                {
                    ConfigureImportedSkyTexture(importedAssetPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Step 158 could not download the optional ambientCG night HDRI. " +
                    "The procedural starry-sky fallback will be used instead. " +
                    ex.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return false;
        }

        static void ExtractImageFiles(
            string zipPath,
            string targetAbsolute)
        {
            using (FileStream stream =
                File.OpenRead(zipPath))
            using (ZipArchive archive =
                new ZipArchive(stream,ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string ext =
                        Path.GetExtension(entry.Name).ToLowerInvariant();

                    if (ext != ".exr" &&
                        ext != ".hdr" &&
                        ext != ".jpg" &&
                        ext != ".jpeg" &&
                        ext != ".png")
                        continue;

                    string destination =
                        Path.GetFullPath(
                            Path.Combine(
                                targetAbsolute,
                                entry.Name));

                    string normalizedRoot =
                        Path.GetFullPath(targetAbsolute) +
                        Path.DirectorySeparatorChar;

                    if (!destination.StartsWith(
                            normalizedRoot,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (Stream input = entry.Open())
                    using (FileStream output =
                        File.Create(destination))
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }

        static string FindExistingNightSkyTexture()
        {
            string absolute =
                ToAbsolute(ExternalFolder);
            if (!Directory.Exists(absolute))
                return null;

            string[] preference =
            {
                ".exr",
                ".hdr",
                ".png",
                ".jpg",
                ".jpeg"
            };

            for (int e=0;e<preference.Length;e++)
            {
                string file =
                    Directory.GetFiles(
                        absolute,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(
                        f =>
                            string.Equals(
                                Path.GetExtension(f),
                                preference[e],
                                StringComparison.OrdinalIgnoreCase) &&
                            Path.GetFileName(f).IndexOf(
                                "NightSky",
                                StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrWhiteSpace(file))
                    return ToAssetPath(file);
            }

            return null;
        }

        static void ConfigureImportedSkyTexture(
            string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath)
                    as TextureImporter;
            if (!importer) return;

            importer.textureShape =
                TextureImporterShape.Texture2D;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;

            string extension =
                Path.GetExtension(assetPath)
                    .ToLowerInvariant();
            if (extension == ".exr" ||
                extension == ".hdr")
                importer.sRGBTexture = false;

            importer.SaveAndReimport();
        }

        static Material BuildNightSkyMaterial(
            string importedTexturePath)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    NightSkyMaterialPath);

            Texture2D imported = null;
            if (!string.IsNullOrWhiteSpace(importedTexturePath))
                imported =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        importedTexturePath);

            Shader shader = imported
                ? Shader.Find("Skybox/Panoramic")
                : Shader.Find(
                    "Hanger51/Skybox/Procedural Night");

            if (!shader)
                shader = Shader.Find("Skybox/Procedural");

            if (!material)
            {
                material = new Material(shader)
                {
                    name = "H51 Night Sky"
                };
                AssetDatabase.CreateAsset(
                    material,
                    NightSkyMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            if (imported)
            {
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex",imported);
                if (material.HasProperty("_Tex"))
                    material.SetTexture("_Tex",imported);
                if (material.HasProperty("_Exposure"))
                    material.SetFloat("_Exposure",.72f);
                if (material.HasProperty("_Rotation"))
                    material.SetFloat("_Rotation",155f);
                if (material.HasProperty("_Tint"))
                    material.SetColor(
                        "_Tint",
                        new Color(.70f,.77f,.92f,1f));
            }
            else
            {
                if (material.HasProperty("_Exposure"))
                    material.SetFloat("_Exposure",.82f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static Material BuildUnlitGlowMaterial(
            string path,
            Color color)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
                shader = Shader.Find("Unlit/Color");
            if (!shader)
                shader = Shader.Find("Standard");

            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material,path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor",color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color",color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor",color * 4f);
                material.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static int UpgradeLampBulbMaterials(
            Material warm,
            Material cool)
        {
            H51NightLight[] lights =
                UnityEngine.Object.FindObjectsByType<H51NightLight>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int count = 0;

            for (int i=0;i<lights.Length;i++)
            {
                H51NightLight light = lights[i];
                if (!light) continue;

                bool useCool =
                    light.name.IndexOf(
                        "Apron",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                Renderer[] renderers =
                    light.GetComponentsInChildren<Renderer>(true);
                for (int r=0;r<renderers.Length;r++)
                {
                    Renderer renderer = renderers[r];
                    if (!renderer ||
                        renderer.name.IndexOf(
                            "Lens",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    renderer.sharedMaterial =
                        useCool ? cool : warm;
                    renderer.enabled = true;
                    renderer.forceRenderingOff = false;
                    EditorUtility.SetDirty(renderer);
                    count++;
                }
            }

            return count;
        }

        static int UpgradeRunwayBulbMaterials(
            Material cool)
        {
            Renderer[] all =
                UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int count = 0;
            for (int i=0;i<all.Length;i++)
            {
                Renderer renderer = all[i];
                if (!renderer ||
                    renderer.name.IndexOf(
                        "Runway Light Lens",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                renderer.sharedMaterial = cool;
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                EditorUtility.SetDirty(renderer);
                count++;
            }
            return count;
        }

        static int ApplyCitizenCurfew()
        {
            H51CitizenBrain[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int count = 0;

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenBrain citizen = citizens[i];
                if (!citizen) continue;

                SerializedObject so =
                    new SerializedObject(citizen);
                SerializedProperty curfew =
                    so.FindProperty("civilianCurfewHour");
                SerializedProperty morning =
                    so.FindProperty("civilianMorningReleaseHour");

                if (curfew != null)
                    curfew.floatValue = 21.5f;
                if (morning != null)
                    morning.floatValue = 6.5f;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(citizen);
                count++;
            }

            return count;
        }

        static void WriteSourceNote(
            bool externalAssetInstalled)
        {
            string note =
                "Hanger 51 Night Sky Source\n" +
                "==========================\n\n" +
                "Preferred external sky: ambientCG Night Sky HDRI 001 (2K)\n" +
                "Asset page: " + AmbientCgAssetPage + "\n" +
                "Download endpoint: " + AmbientCgDownload + "\n" +
                "License: CC0 / public-domain dedication. ambientCG states its assets are free for commercial use without attribution.\n\n" +
                "Installed external asset this project: " +
                externalAssetInstalled + "\n\n" +
                "If the external download is unavailable, Hanger 51 uses the repository's procedural starry-night skybox fallback shader.\n";

            File.WriteAllText(
                ToAbsolute(SourceNotePath),
                note);
            AssetDatabase.Refresh();
        }

        static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (!root) return null;

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] &&
                    all[i].name == objectName)
                    return all[i];

            return null;
        }

        static string ToAbsolute(string assetPath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName;
            return Path.Combine(
                projectRoot ?? string.Empty,
                assetPath.Replace('/',Path.DirectorySeparatorChar));
        }

        static string ToAssetPath(string absolutePath)
        {
            string normalized =
                absolutePath.Replace('\','/');
            string assets =
                Application.dataPath.Replace('\','/');

            if (!normalized.StartsWith(
                    assets,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return "Assets" +
                normalized.Substring(assets.Length);
        }

        static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) ||
                folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath)
                    ?.Replace("\\","/");
            string folder =
                Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parent) ||
                string.IsNullOrWhiteSpace(folder))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,folder);
        }
    }
}
