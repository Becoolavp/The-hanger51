using Hanger51.Health;
using Hanger51.NPCs;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51AnatomicalHealthSetup
    {
        [MenuItem("Hanger 51/World/Current/168 - Install Anatomical Health System")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 168 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            if (!player)
            {
                Debug.LogError(
                    "Step 168 failed. The active scene does not contain a FirstPersonController.");
                return;
            }

            int playerHealthAdded = 0;
            int inspectorAdded = 0;
            int npcHealthAdded = 0;

            H51AnatomicalHealth playerHealth =
                player.GetComponent<H51AnatomicalHealth>();

            if (!playerHealth)
            {
                playerHealth =
                    Undo.AddComponent<H51AnatomicalHealth>(
                        player.gameObject);
                playerHealthAdded++;
            }

            H51BodyInspectorUI inspector =
                player.GetComponent<H51BodyInspectorUI>();

            if (!inspector)
            {
                inspector =
                    Undo.AddComponent<H51BodyInspectorUI>(
                        player.gameObject);
                inspectorAdded++;
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

                if (!citizen.GetComponent<H51AnatomicalHealth>())
                {
                    Undo.AddComponent<H51AnatomicalHealth>(
                        citizen.gameObject);
                    npcHealthAdded++;
                }
            }

            EditorUtility.SetDirty(
                player.gameObject);

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 168 complete. Player anatomical health added=" +
                playerHealthAdded +
                ", Body Inspector added=" +
                inspectorAdded +
                ", citizen anatomical health components added=" +
                npcHealthAdded +
                "/" + citizens.Length +
                ". Press H in a Development Build to open the player's Body Inspector. Run Step 169.");
        }

        [MenuItem("Hanger 51/World/Current/169 - Validate Anatomical Health System")]
        public static void Validate()
        {
            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            H51AnatomicalHealth playerHealth =
                player
                    ? player.GetComponent<H51AnatomicalHealth>()
                    : null;

            H51BodyInspectorUI inspector =
                player
                    ? player.GetComponent<H51BodyInspectorUI>()
                    : null;

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int missingNpcHealth = 0;
            int badNpcAnatomy = 0;

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
                {
                    missingNpcHealth++;
                    continue;
                }

                if (health.Regions.Count < 9 ||
                    health.Organs.Count < 10)
                    badNpcAnatomy++;
            }

            bool playerReady =
                player &&
                playerHealth &&
                inspector &&
                playerHealth.Regions.Count >= 9 &&
                playerHealth.Organs.Count >= 10;

            bool ok =
                playerReady &&
                citizens.Length > 0 &&
                missingNpcHealth == 0 &&
                badNpcAnatomy == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 169 passed. Player has anatomical health + Body Inspector, and all " +
                    citizens.Length +
                    " citizens have the same 9-region / 10-organ health model. The model tracks blood volume, external/internal bleeding, oxygen, pulse, pressure, pain, shock, adrenaline, consciousness, fractures and organ failure.");
            }
            else
            {
                Debug.LogError(
                    "Step 169 failed. player=" +
                    (player ? "yes" : "MISSING") +
                    ", player health=" +
                    (playerHealth ? "yes" : "MISSING") +
                    ", Body Inspector=" +
                    (inspector ? "yes" : "MISSING") +
                    ", citizens=" +
                    citizens.Length +
                    ", missing NPC health=" +
                    missingNpcHealth +
                    ", NPCs with incomplete anatomy=" +
                    badNpcAnatomy +
                    ".");
            }
        }
    }
}
