using System;
using System.Collections.Generic;
using System.Linq;
using Hanger51.NPCs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenSocialGroundingPass
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";

        static readonly string[] FemaleFirstNames =
        {
            "Abigail","Alicia","Anna","Caroline","Claire","Emily","Emma","Evelyn","Grace","Hannah",
            "Julia","Laura","Leah","Maria","Megan","Natalie","Nicole","Olivia","Rachel","Rebecca",
            "Sarah","Sophia","Victoria","Zoe","Molly","Jenny","Brooke","Erin","Amanda","Chloe",
            "Ella","Lily","Madison","Samantha","Katherine","Kayla","Brianna","Hailey","Morgan","Allison",
            "Danielle","Autumn","Paige","Vanessa","Savannah","Jenna","Maya","Lauren","Audrey","Nora"
        };

        static readonly string[] MaleFirstNames =
        {
            "Aaron","Adam","Andrew","Ben","Brandon","Charles","Daniel","David","Derek","Ethan",
            "Henry","Jack","James","Jason","John","Kevin","Luke","Matthew","Michael","Nathan",
            "Patrick","Robert","Ryan","Scott","Thomas","William","Caleb","Eric","Noah","Logan",
            "Cole","Jacob","Samuel","Benjamin","Tyler","Connor","Dylan","Evan","Cameron","Owen",
            "Lucas","Austin","Trevor","Garrett","Hunter","Jordan","Cody","Alex","Marcus","Isaac"
        };

        [MenuItem("Hanger 51/World/Current/142 - Ground Citizens Match Names And Expand Social Life")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 142 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51CitizenWorldLifeRefinement.Build();

            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            H51CitizenRegistry registry = citizenRoot ? citizenRoot.GetComponent<H51CitizenRegistry>() : null;

            if (!world || !citizenRoot || !registry)
            {
                Debug.LogError("Step 142 failed. The current citizen system was not found. Run the current citizen build chain first.");
                return;
            }

            H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            if (citizens.Length == 0)
            {
                Debug.LogError("Step 142 failed. No citizens were found.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Calibrating real feet to floors, roads and terrain", .12f);
                int groundingAdded = 0;
                for (int i = 0; i < citizens.Length; i++)
                {
                    H51CitizenIdentity citizen = citizens[i];
                    if (!citizen) continue;

                    H51CitizenGrounding grounding = citizen.GetComponent<H51CitizenGrounding>();
                    if (!grounding)
                    {
                        grounding = citizen.gameObject.AddComponent<H51CitizenGrounding>();
                        groundingAdded++;
                    }

                    grounding.CalibrateFromCurrentPose();
                    EditorUtility.SetDirty(grounding);
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Matching citizen names to their visible character model", .35f);
                int presentationDetected = 0;
                int renamed = RepairPresentationAndNames(citizens, ref presentationDetected);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building household and coworker relationships", .58f);
                int socialLinks = EstablishExistingSocialLinks(citizens);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Enabling town gossip and reputation spread", .74f);
                H51CitizenSocialLifeController social = citizenRoot.GetComponent<H51CitizenSocialLifeController>();
                if (!social) social = citizenRoot.gameObject.AddComponent<H51CitizenSocialLifeController>();
                EditorUtility.SetDirty(social);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Refreshing citizen registry and saving", .91f);
                registry.RefreshSceneReferences();
                EditorUtility.SetDirty(registry);
                EditorUtility.SetDirty(citizenRoot);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();

                Debug.Log(
                    $"Step 142 complete. citizens={citizens.Length}, grounding components added={groundingAdded}, visual presentations detected={presentationDetected}/{citizens.Length}, names corrected={renamed}, household/coworker relationship links initialized={socialLinks}, town gossip/reputation controller=present. Citizens now continuously align their visible feet with the real physical floor while walking or standing. Run Step 143.",
                    citizenRoot.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/143 - Validate Citizen Grounding Names And Social Life")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            if (!world || !citizenRoot)
            {
                Debug.LogError("Step 143 failed. Run Step 142 first.");
                return;
            }

            H51CitizenIdentity[] citizens = citizenRoot.GetComponentsInChildren<H51CitizenIdentity>(true);
            int missingGrounding = 0;
            int knownPresentation = 0;
            int unknownPresentation = 0;
            int nameMismatches = 0;
            int socialRelationships = 0;

            for (int i = 0; i < citizens.Length; i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (!citizen) continue;

                if (!citizen.GetComponent<H51CitizenGrounding>())
                    missingGrounding++;

                if (citizen.Presentation == H51CitizenPresentation.Unknown)
                {
                    unknownPresentation++;
                }
                else
                {
                    knownPresentation++;
                    if (!NameMatchesPresentation(citizen.DisplayName, citizen.Presentation))
                        nameMismatches++;
                }

                for (int r = 0; r < citizen.Relationships.Count; r++)
                {
                    H51CitizenRelationship relationship = citizen.Relationships[r];
                    if (relationship != null &&
                        !string.IsNullOrWhiteSpace(relationship.otherId) &&
                        relationship.otherId != "PLAYER")
                        socialRelationships++;
                }
            }

            H51CitizenSocialLifeController social = citizenRoot.GetComponent<H51CitizenSocialLifeController>();

            bool ok =
                citizens.Length > 0 &&
                missingGrounding == 0 &&
                nameMismatches == 0 &&
                social &&
                socialRelationships > 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 143 passed. citizens={citizens.Length}, grounding failures=0, model-matched names={knownPresentation}, visual sex not encoded strongly enough to classify={unknownPresentation}, known name mismatches=0, NPC-to-NPC relationship links={socialRelationships}, gossip/reputation controller=present.",
                    citizenRoot.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 143 failed. citizens={citizens.Length}, missing grounding={missingGrounding}, known presentation={knownPresentation}, unknown presentation={unknownPresentation}, known name mismatches={nameMismatches}, NPC relationship links={socialRelationships}, social controller={(social ? "present" : "MISSING")}.",
                    citizenRoot.gameObject);
            }
        }

        static int RepairPresentationAndNames(H51CitizenIdentity[] citizens, ref int detected)
        {
            HashSet<string> usedNames = new HashSet<string>(
                citizens.Where(c => c && !string.IsNullOrWhiteSpace(c.DisplayName)).Select(c => c.DisplayName),
                StringComparer.OrdinalIgnoreCase);

            int renamed = 0;

            for (int i = 0; i < citizens.Length; i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (!citizen) continue;

                H51CitizenPresentation presentation = DetectPresentation(citizen.transform);
                citizen.SetPresentation(presentation);

                if (presentation != H51CitizenPresentation.Unknown)
                    detected++;

                if (presentation == H51CitizenPresentation.Unknown ||
                    NameMatchesPresentation(citizen.DisplayName, presentation))
                {
                    EditorUtility.SetDirty(citizen);
                    continue;
                }

                usedNames.Remove(citizen.DisplayName);
                string corrected = BuildPresentationMatchedName(citizen, presentation, usedNames);
                citizen.SetDisplayName(corrected);
                citizen.name = "Citizen - " + corrected + " - " + citizen.Occupation;
                usedNames.Add(corrected);
                renamed++;

                EditorUtility.SetDirty(citizen);
                EditorUtility.SetDirty(citizen.gameObject);
            }

            return renamed;
        }

        static H51CitizenPresentation DetectPresentation(Transform root)
        {
            int female = 0;
            int male = 0;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                ScoreText(renderer.name, ref female, ref male);

                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                    ScoreText(skinned.sharedMesh.name, ref female, ref male);

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh)
                    ScoreText(filter.sharedMesh.name, ref female, ref male);

                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                    if (materials[m]) ScoreText(materials[m].name, ref female, ref male);

                Transform t = renderer.transform;
                for (int depth = 0; t && depth < 5; depth++, t = t.parent)
                {
                    ScoreText(t.name, ref female, ref male);
                    if (t == root) break;
                }
            }

            if (female >= male + 2) return H51CitizenPresentation.Female;
            if (male >= female + 2) return H51CitizenPresentation.Male;

            return DetectPresentationByShape(root);
        }

        static H51CitizenPresentation DetectPresentationByShape(Transform root)
        {
            List<Vector3> points = new List<Vector3>(4096);
            SkinnedMeshRenderer[] meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            for (int i = 0; i < meshes.Length; i++)
            {
                SkinnedMeshRenderer smr = meshes[i];
                if (!smr || !smr.enabled || !smr.gameObject.activeInHierarchy || !smr.sharedMesh)
                    continue;

                Vector3[] vertices = smr.sharedMesh.vertices;
                int stride = Mathf.Max(1, vertices.Length / 3500);
                for (int v = 0; v < vertices.Length; v += stride)
                {
                    Vector3 world = smr.transform.TransformPoint(vertices[v]);
                    points.Add(root.InverseTransformPoint(world));
                }
            }

            if (points.Count < 40) return H51CitizenPresentation.Unknown;

            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                minY = Mathf.Min(minY, points[i].y);
                maxY = Mathf.Max(maxY, points[i].y);
            }

            float height = maxY - minY;
            if (height < .8f) return H51CitizenPresentation.Unknown;

            float hipWidth = SectionWidth(points, minY + height * .45f, minY + height * .61f);
            float shoulderWidth = SectionWidth(points, minY + height * .68f, minY + height * .82f);

            if (hipWidth < .18f || shoulderWidth < .18f)
                return H51CitizenPresentation.Unknown;

            float ratio = shoulderWidth / hipWidth;

            // Only use shape as a conservative fallback when the modular asset itself
            // did not expose male/female naming tokens. Ambiguous bodies stay Unknown.
            if (ratio <= 1.08f) return H51CitizenPresentation.Female;
            if (ratio >= 1.23f) return H51CitizenPresentation.Male;
            return H51CitizenPresentation.Unknown;
        }

        static float SectionWidth(List<Vector3> points, float minY, float maxY)
        {
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            bool found = false;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                if (p.y < minY || p.y > maxY) continue;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                found = true;
            }

            return found ? maxX - minX : 0f;
        }

        static void ScoreText(string value, ref int female, ref int male)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            string lower = value.ToLowerInvariant();
            char[] chars = lower.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = ' ';

            string padded = " " + new string(chars) + " ";

            string[] femaleTokens = { "female", "woman", "women", "girl", "lady", "fem", "feminine", "fbody", "fhead" };
            string[] maleTokens = { "male", "man", "men", "boy", "gentleman", "masc", "masculine", "mbody", "mhead" };

            for (int i = 0; i < femaleTokens.Length; i++)
                if (padded.Contains(" " + femaleTokens[i] + " ")) female += 4;

            for (int i = 0; i < maleTokens.Length; i++)
                if (padded.Contains(" " + maleTokens[i] + " ")) male += 4;

            bool bodyContext =
                padded.Contains(" body ") ||
                padded.Contains(" head ") ||
                padded.Contains(" human ") ||
                padded.Contains(" character ");

            if (bodyContext && padded.Contains(" f ")) female += 2;
            if (bodyContext && padded.Contains(" m ")) male += 2;
        }

        static bool NameMatchesPresentation(string displayName, H51CitizenPresentation presentation)
        {
            string first = FirstName(displayName);
            if (presentation == H51CitizenPresentation.Female)
                return FemaleFirstNames.Any(n => n.Equals(first, StringComparison.OrdinalIgnoreCase));
            if (presentation == H51CitizenPresentation.Male)
                return MaleFirstNames.Any(n => n.Equals(first, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        static string BuildPresentationMatchedName(
            H51CitizenIdentity citizen,
            H51CitizenPresentation presentation,
            HashSet<string> used)
        {
            string[] names = presentation == H51CitizenPresentation.Female
                ? FemaleFirstNames
                : MaleFirstNames;

            string last = LastName(citizen.DisplayName);
            int start = (StableHash(citizen.CitizenId + "|" + citizen.VisualArchetype) & 0x7fffffff) % names.Length;

            for (int i = 0; i < names.Length; i++)
            {
                string candidate = names[(start + i) % names.Length] + " " + last;
                if (!used.Contains(candidate))
                    return candidate;
            }

            return names[start] + " " + last + " " + ((StableHash(citizen.CitizenId) & 0x7fffffff) % 99).ToString("00");
        }

        static int EstablishExistingSocialLinks(H51CitizenIdentity[] citizens)
        {
            int links = 0;

            for (int i = 0; i < citizens.Length; i++)
            {
                H51CitizenIdentity a = citizens[i];
                if (!a || string.IsNullOrWhiteSpace(a.CitizenId)) continue;

                for (int j = i + 1; j < citizens.Length; j++)
                {
                    H51CitizenIdentity b = citizens[j];
                    if (!b || string.IsNullOrWhiteSpace(b.CitizenId)) continue;
                    if (!string.Equals(a.TownName, b.TownName, StringComparison.Ordinal)) continue;

                    bool sameHome =
                        !string.IsNullOrWhiteSpace(a.HomeAnchorId) &&
                        a.HomeAnchorId == b.HomeAnchorId;

                    bool sameWork =
                        !string.IsNullOrWhiteSpace(a.WorkAnchorId) &&
                        a.WorkAnchorId == b.WorkAnchorId;

                    if (!sameHome && !sameWork) continue;

                    H51CitizenRelationship ab = a.Relationship(b.CitizenId);
                    H51CitizenRelationship ba = b.Relationship(a.CitizenId);

                    if (sameHome)
                    {
                        RaiseRelationship(ab, 38f, 22f, 34f);
                        RaiseRelationship(ba, 38f, 22f, 34f);
                    }

                    if (sameWork)
                    {
                        RaiseRelationship(ab, 14f, 28f, 7f);
                        RaiseRelationship(ba, 14f, 28f, 7f);
                    }

                    EditorUtility.SetDirty(a);
                    EditorUtility.SetDirty(b);
                    links += 2;
                }
            }

            return links;
        }

        static void RaiseRelationship(
            H51CitizenRelationship relationship,
            float trust,
            float respect,
            float affection)
        {
            relationship.trust = Mathf.Max(relationship.trust, trust);
            relationship.respect = Mathf.Max(relationship.respect, respect);
            relationship.affection = Mathf.Max(relationship.affection, affection);
        }

        static string FirstName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;
            string trimmed = displayName.Trim();
            int split = trimmed.IndexOf(' ');
            return split < 0 ? trimmed : trimmed.Substring(0, split);
        }

        static string LastName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "Resident";
            string trimmed = displayName.Trim();
            int split = trimmed.IndexOf(' ');
            return split < 0 || split >= trimmed.Length - 1
                ? "Resident"
                : trimmed.Substring(split + 1);
        }

        static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
                return hash;
            }
        }

        static GameObject Find(string name)
        {
            GameObject direct = GameObject.Find(name);
            if (direct) return direct;

            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].gameObject.scene.IsValid() && all[i].name == name)
                    return all[i].gameObject;

            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (!root) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].name == name)
                    return all[i];
            return null;
        }
    }
}
