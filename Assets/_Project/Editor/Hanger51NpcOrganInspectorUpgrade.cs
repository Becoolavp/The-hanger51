using Hanger51.Health;
using Hanger51.NPCs;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51NpcOrganInspectorUpgrade
    {
        [MenuItem("Hanger 51/World/Current/180 - Install NPC Internal Organs And Casualty Inspector")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying ||
                EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 180 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            if (!player)
            {
                Debug.LogError(
                    "Step 180 failed. No FirstPersonController exists in the active scene.");
                return;
            }

            int rigsAdded = 0;
            int ragdollsAdded = 0;
            int bodiesPrepared = 0;

            H51AnatomicalHealth playerHealth =
                player.GetComponent<H51AnatomicalHealth>();

            if (playerHealth)
            {
                H51AnatomicalHitboxRig playerRig =
                    player.GetComponent<H51AnatomicalHitboxRig>();

                if (!playerRig)
                {
                    playerRig =
                        Undo.AddComponent<H51AnatomicalHitboxRig>(
                            player.gameObject);
                    rigsAdded++;
                }

                playerRig.EnsureBuilt();
                EditorUtility.SetDirty(player.gameObject);
                bodiesPrepared++;
            }

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity citizen =
                    citizens[i];

                if (!citizen ||
                    !citizen.gameObject.scene.IsValid())
                    continue;

                H51AnatomicalHealth health =
                    citizen.GetComponent<H51AnatomicalHealth>();

                if (!health)
                    continue;

                H51AnatomicalHitboxRig rig =
                    citizen.GetComponent<H51AnatomicalHitboxRig>();

                if (!rig)
                {
                    rig =
                        Undo.AddComponent<H51AnatomicalHitboxRig>(
                            citizen.gameObject);
                    rigsAdded++;
                }

                if (!citizen.GetComponent<H51CitizenRagdoll>())
                {
                    Undo.AddComponent<H51CitizenRagdoll>(
                        citizen.gameObject);
                    ragdollsAdded++;
                }

                rig.EnsureBuilt();

                H51BoZoCitizenVisual visual =
                    citizen.GetComponent<H51BoZoCitizenVisual>();

                if (visual)
                    EditorUtility.SetDirty(visual);

                EditorUtility.SetDirty(citizen.gameObject);
                bodiesPrepared++;
            }

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 180 complete. Anatomical rigs added=" +
                rigsAdded +
                ", ragdoll components added=" +
                ragdollsAdded +
                ", bodies prepared=" +
                bodiesPrepared +
                ". Internal organ volumes now include brain, heart, both lungs, liver, stomach, intestines, both kidneys and spine. Run Step 181.");
        }

        [MenuItem("Hanger 51/World/Current/181 - Validate NPC Internal Organs And Casualty Inspector")]
        public static void Validate()
        {
            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            H51BodyInspectorUI inspector =
                player
                    ? player.GetComponent<H51BodyInspectorUI>()
                    : null;

            H51AnatomicalHealth playerHealth =
                player
                    ? player.GetComponent<H51AnatomicalHealth>()
                    : null;

            H51AnatomicalHitboxRig playerRig =
                player
                    ? player.GetComponent<H51AnatomicalHitboxRig>()
                    : null;

            int missingHealth = 0;
            int missingRig = 0;
            int missingRagdoll = 0;
            int badRegionHitboxes = 0;
            int badOrganHitboxes = 0;
            int citizensChecked = 0;

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity citizen =
                    citizens[i];

                if (!citizen ||
                    !citizen.gameObject.scene.IsValid())
                    continue;

                citizensChecked++;

                H51AnatomicalHealth health =
                    citizen.GetComponent<H51AnatomicalHealth>();

                if (!health)
                {
                    missingHealth++;
                    continue;
                }

                H51AnatomicalHitboxRig rig =
                    citizen.GetComponent<H51AnatomicalHitboxRig>();

                if (!rig)
                {
                    missingRig++;
                    continue;
                }

                rig.EnsureBuilt();

                if (!citizen.GetComponent<H51CitizenRagdoll>())
                    missingRagdoll++;

                H51AnatomicalHitbox[] regionHitboxes =
                    citizen.GetComponentsInChildren<H51AnatomicalHitbox>(true);

                H51OrganHitbox[] organHitboxes =
                    citizen.GetComponentsInChildren<H51OrganHitbox>(true);

                if (regionHitboxes.Length < 9)
                    badRegionHitboxes++;

                bool[] organs =
                    new bool[
                        System.Enum.GetValues(
                            typeof(H51OrganType)).Length];

                for (int h=0;h<organHitboxes.Length;h++)
                {
                    H51OrganHitbox organ =
                        organHitboxes[h];

                    if (!organ ||
                        organ.Health != health)
                        continue;

                    int index =
                        (int)organ.Organ;

                    if (index >= 0 &&
                        index < organs.Length)
                        organs[index] = true;
                }

                for (int o=0;o<organs.Length;o++)
                {
                    if (!organs[o])
                    {
                        badOrganHitboxes++;
                        break;
                    }
                }
            }

            bool playerReady =
                player &&
                playerHealth &&
                inspector &&
                playerRig;

            bool ok =
                playerReady &&
                citizensChecked > 0 &&
                missingHealth == 0 &&
                missingRig == 0 &&
                missingRagdoll == 0 &&
                badRegionHitboxes == 0 &&
                badOrganHitboxes == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 181 passed. Player casualty-inspector support is present and all " +
                    citizensChecked +
                    " citizens have 9 body-region volumes, all 10 internal organ volumes, anatomical health, and the physics ragdoll component. Direct brain/heart/lung/etc. ballistic intersections can now damage those organs independently.");
            }
            else
            {
                Debug.LogError(
                    "Step 181 failed. player=" +
                    (player ? "yes" : "MISSING") +
                    ", player health=" +
                    (playerHealth ? "yes" : "MISSING") +
                    ", inspector=" +
                    (inspector ? "yes" : "MISSING") +
                    ", player rig=" +
                    (playerRig ? "yes" : "MISSING") +
                    ", citizens checked=" +
                    citizensChecked +
                    ", missing health=" +
                    missingHealth +
                    ", missing rig=" +
                    missingRig +
                    ", missing ragdoll=" +
                    missingRagdoll +
                    ", bad region-hitbox citizens=" +
                    badRegionHitboxes +
                    ", bad organ-hitbox citizens=" +
                    badOrganHitboxes +
                    ".");
            }
        }
    }
}
