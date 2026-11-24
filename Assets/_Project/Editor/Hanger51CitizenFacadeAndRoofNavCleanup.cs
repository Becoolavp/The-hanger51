using System;
using System.Collections.Generic;
using System.Linq;
using Hanger51.NPCs;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenFacadeAndRoofNavCleanup
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string CleanupRootName = "H51 Facade And Roof Nav Cleanup";
        const string Generated = "Assets/_Project/Characters/H51Citizens/Generated/FacadeCleanup";
        const string DepthTextShaderPath = "Assets/_Project/Shaders/Hanger51DepthWorldText.shader";

        struct Mats
        {
            public Material trim;
            public Material door;
            public Material brass;
            public Material awning;
            public Material sign;
            public Material fireDoor;
        }

        [MenuItem("Hanger 51/World/Current/148 - Fix Roof NavMesh And Legacy Building Facades")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 148 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform,CitizenRootName) : null;
            if (!world || !citizenRoot)
            {
                Debug.LogError("Step 148 failed. The current countryside or citizen system is missing.");
                return;
            }

            try
            {
                Ensure(Generated + "/Materials");
                Mats mats = BuildMaterials();

                Transform old = FindChild(world.transform,CleanupRootName);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform cleanupRoot = New(CleanupRootName,world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Facade Cleanup","Removing building roofs from pedestrian NavMesh",.12f);
                int roofModifiers = MarkEnterableRoofsIgnoredFromNav(world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Facade Cleanup","Repairing older workplace fronts and storefront proportions",.34f);
                int facades = RepairLegacyWorkplaceFacades(world.transform,cleanupRoot,mats);

                EditorUtility.DisplayProgressBar("Hanger 51 Facade Cleanup","Normalizing theater, clinic and civic labels",.60f);
                int labels = NormalizeCivicLabels(world.transform);

                Physics.SyncTransforms();

                EditorUtility.DisplayProgressBar("Hanger 51 Facade Cleanup","Rebaking citizen NavMeshes without rooftop walk surfaces",.78f);
                int navMeshes = RebuildNavMeshes(citizenRoot);

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = cleanupRoot.gameObject;

                Debug.Log(
                    $"Step 148 complete. roof colliders excluded from pedestrian NavMesh={roofModifiers}, legacy commercial facades repaired={facades}, civic/theater labels normalized={labels}, NavMeshes rebaked={navMeshes}. Roofs remain physical colliders but are no longer walkable NPC surfaces. Run Step 149.",
                    cleanupRoot.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/149 - Validate Roof NavMesh And Building Facades")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Transform citizenRoot = world ? FindChild(world.transform,CitizenRootName) : null;
            Transform cleanupRoot = world ? FindChild(world.transform,CleanupRootName) : null;

            if (!world || !citizenRoot || !cleanupRoot)
            {
                Debug.LogError("Step 149 failed. Run Step 148 first.");
                return;
            }

            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            int roofColliderCount = 0;
            int roofModifierFailures = 0;
            int roofNavFailures = 0;
            int legacyWorkplaces = 0;
            int missingFacadeCleanup = 0;
            int farmSupplyBadDoors = 0;
            int signAboveRoof = 0;

            for (int i=0;i<buildings.Length;i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building) continue;

                Transform[] all = building.GetComponentsInChildren<Transform>(true);
                for (int t=0;t<all.Length;t++)
                {
                    Transform tr = all[t];
                    if (!tr || !IsRoofName(tr.name)) continue;
                    Collider col = tr.GetComponent<Collider>();
                    if (!col || !col.enabled) continue;

                    roofColliderCount++;
                    NavMeshModifier modifier = tr.GetComponent<NavMeshModifier>();
                    if (!modifier || !modifier.ignoreFromBuild)
                        roofModifierFailures++;

                    Renderer rr = tr.GetComponent<Renderer>();
                    if (rr)
                    {
                        Vector3 probe = rr.bounds.center;
                        probe.y = rr.bounds.max.y + .18f;
                        if (NavMesh.SamplePosition(probe,out NavMeshHit hit,.65f,NavMesh.AllAreas) &&
                            Mathf.Abs(hit.position.y - rr.bounds.max.y) < .65f)
                            roofNavFailures++;
                    }
                }

                H51CitizenWorkplace workplace = building.GetComponent<H51CitizenWorkplace>();
                Transform enterable = FindChild(building.transform,"H51 Enterable Interior");
                if (!workplace || !enterable) continue;

                legacyWorkplaces++;
                Transform cleanup = DirectChild(building.transform,"H51 Facade Cleanup");
                if (!cleanup)
                {
                    missingFacadeCleanup++;
                    continue;
                }

                string lower = building.DisplayName.ToLowerInvariant();
                if (lower.Contains("farm supply"))
                {
                    Transform door = FindChild(cleanup,"Facade Door");
                    if (!door ||
                        door.localScale.y < 2.0f ||
                        door.localScale.y > 2.75f ||
                        Mathf.Min(Mathf.Abs(door.localScale.x),Mathf.Abs(door.localScale.z)) > .25f)
                        farmSupplyBadDoors++;
                }

                Transform signBoard = FindChild(cleanup,"Facade Sign Board");
                Transform roof = FindBestRoof(building.transform);
                Renderer sr = signBoard ? signBoard.GetComponent<Renderer>() : null;
                Renderer roofRenderer = roof ? roof.GetComponent<Renderer>() : null;
                if (sr && roofRenderer && sr.bounds.max.y > roofRenderer.bounds.max.y + .08f)
                    signAboveRoof++;
            }

            NavMeshSurface[] surfaces = citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);
            int navReady = surfaces.Count(s => s && s.navMeshData);

            bool ok =
                roofColliderCount > 0 &&
                roofModifierFailures == 0 &&
                roofNavFailures == 0 &&
                legacyWorkplaces > 0 &&
                missingFacadeCleanup == 0 &&
                farmSupplyBadDoors == 0 &&
                signAboveRoof == 0 &&
                navReady == surfaces.Length;

            if (ok)
            {
                Debug.Log(
                    $"Step 149 passed. roof colliders={roofColliderCount}, roof modifier failures=0, rooftop NavMesh failures=0, repaired legacy workplaces={legacyWorkplaces}, missing facade cleanup=0, bad Farm Supply doors=0, signs above roofs=0, NavMeshes={navReady}/{surfaces.Length}.",
                    cleanupRoot.gameObject);
            }
            else
            {
                Debug.LogError(
                    $"Step 149 failed. roof colliders={roofColliderCount}, roof modifier failures={roofModifierFailures}, rooftop NavMesh failures={roofNavFailures}, legacy workplaces={legacyWorkplaces}, missing facade cleanup={missingFacadeCleanup}, bad Farm Supply doors={farmSupplyBadDoors}, signs above roofs={signAboveRoof}, NavMeshes={navReady}/{surfaces.Length}.",
                    cleanupRoot.gameObject);
            }
        }

        static int MarkEnterableRoofsIgnoredFromNav(Transform world)
        {
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            HashSet<GameObject> handled = new HashSet<GameObject>();
            int count = 0;

            for (int i=0;i<buildings.Length;i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building) continue;

                Transform[] all = building.GetComponentsInChildren<Transform>(true);
                for (int t=0;t<all.Length;t++)
                {
                    Transform tr = all[t];
                    if (!tr || !IsRoofName(tr.name)) continue;
                    Collider col = tr.GetComponent<Collider>();
                    if (!col || !col.enabled || !handled.Add(tr.gameObject)) continue;

                    NavMeshModifier modifier = tr.GetComponent<NavMeshModifier>();
                    if (!modifier) modifier = tr.gameObject.AddComponent<NavMeshModifier>();
                    modifier.ignoreFromBuild = true;
                    EditorUtility.SetDirty(modifier);
                    count++;
                }
            }
            return count;
        }

        static int RepairLegacyWorkplaceFacades(Transform world, Transform cleanupRoot, Mats mats)
        {
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            int repaired = 0;

            for (int i=0;i<buildings.Length;i++)
            {
                H51EnterableBuilding building = buildings[i];
                if (!building || building.Kind == H51EnterableBuildingKind.Home) continue;

                H51CitizenWorkplace workplace = building.GetComponent<H51CitizenWorkplace>();
                Transform interior = FindChild(building.transform,"H51 Enterable Interior");
                Transform floor = interior ? FindChild(interior,"Interior Floor") : null;
                Transform backWall = interior ? DirectChild(interior,"Back Wall") : null;
                Transform leftWall = interior ? DirectChild(interior,"Left Wall") : null;
                Transform frontLeft = interior ? DirectChild(interior,"Front Wall Left") : null;
                Transform frontRight = interior ? DirectChild(interior,"Front Wall Right") : null;
                Transform header = interior ? DirectChild(interior,"Door Header") : null;

                if (!workplace || !interior || !floor || !backWall || !leftWall || !frontLeft || !frontRight || !header)
                    continue;

                Transform old = DirectChild(building.transform,"H51 Facade Cleanup");
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform facade = New("H51 Facade Cleanup",building.transform);

                DisableLegacyFacadeDecor(building.transform,facade);

                Vector3 center = floor.localPosition;
                float floorY = center.y + floor.localScale.y*.5f;
                float width = Mathf.Abs(backWall.localScale.x);
                float height = Mathf.Abs(backWall.localScale.y);
                float wallY = backWall.localPosition.y;
                float frontZ = frontLeft.localPosition.z;
                float wallTop = wallY + height*.5f;

                string lower = building.DisplayName.ToLowerInvariant();
                bool actualFarm = building.Kind == H51EnterableBuildingKind.Farm &&
                                  !lower.Contains("supply");
                bool serviceBay =
                    lower.Contains("fire station") ||
                    lower.Contains("repair garage") ||
                    lower == "garage" ||
                    actualFarm;

                Material wallMaterial = RendererMaterial(frontLeft) ?? mats.trim;

                if (!serviceBay)
                {
                    const float doorWidth = 1.55f;
                    const float doorHeight = 2.45f;
                    RebuildFrontOpening(
                        frontLeft,frontRight,header,
                        center,width,height,wallY,frontZ,
                        doorWidth,doorHeight);

                    BuildHumanDoor(
                        facade,
                        center.x,
                        floorY,
                        frontZ,
                        doorWidth,
                        doorHeight,
                        mats);

                    float awningWidth = Mathf.Clamp(width*.52f,4.2f,7.6f);
                    float awningY = Mathf.Min(floorY+2.82f,wallTop-.95f);
                    Box(facade,"Facade Awning",
                        new Vector3(center.x,awningY,frontZ+.55f),
                        new Vector3(awningWidth,.14f,.95f),
                        mats.awning,
                        false);
                }
                else
                {
                    float openingWidth = Mathf.Abs(frontRight.localPosition.x-frontLeft.localPosition.x)
                                         - Mathf.Abs(frontRight.localScale.x+frontLeft.localScale.x)*.5f;
                    openingWidth = Mathf.Clamp(openingWidth,3.8f,6.0f);

                    Box(facade,"Service Bay Raised Door",
                        new Vector3(center.x,wallTop-.48f,frontZ+.10f),
                        new Vector3(openingWidth,.72f,.14f),
                        mats.fireDoor,
                        false);

                    for (int s=-2;s<=2;s++)
                        Box(facade,"Service Bay Slat "+s,
                            new Vector3(center.x,wallTop-.48f+s*.12f,frontZ+.185f),
                            new Vector3(openingWidth-.12f,.025f,.02f),
                            mats.trim,
                            false);
                }

                string label = ShortLabel(building.DisplayName);
                float boardWidth = Mathf.Clamp(
                    1.55f + label.Length*.34f,
                    3.6f,
                    Mathf.Min(width*.78f,8.6f));
                float signY = Mathf.Clamp(wallTop-.62f,floorY+3.05f,wallTop-.42f);

                Box(facade,"Facade Sign Board",
                    new Vector3(center.x,signY,frontZ+.17f),
                    new Vector3(boardWidth,.82f,.14f),
                    mats.sign,
                    false);

                WorldText(
                    facade,
                    label,
                    new Vector3(center.x,signY,frontZ+.255f),
                    Quaternion.Euler(0,180f,0),
                    label.Length > 15 ? .115f : label.Length > 10 ? .135f : .155f);

                Transform marker = New("Facade Cleanup Record",cleanupRoot);
                marker.position = building.transform.position;
                marker.rotation = building.transform.rotation;

                repaired++;
            }

            return repaired;
        }

        static void RebuildFrontOpening(
            Transform left,
            Transform right,
            Transform header,
            Vector3 center,
            float width,
            float height,
            float wallY,
            float frontZ,
            float doorWidth,
            float doorHeight)
        {
            float sideWidth = Mathf.Max(.6f,(width-doorWidth)*.5f);
            float sideOffset = doorWidth*.5f + sideWidth*.5f;

            Vector3 ls = left.localScale;
            ls.x = sideWidth;
            left.localScale = ls;
            Vector3 lp = left.localPosition;
            lp.x = center.x-sideOffset;
            lp.z = frontZ;
            left.localPosition = lp;

            Vector3 rs = right.localScale;
            rs.x = sideWidth;
            right.localScale = rs;
            Vector3 rp = right.localPosition;
            rp.x = center.x+sideOffset;
            rp.z = frontZ;
            right.localPosition = rp;

            float bottom = wallY-height*.5f;
            float headerHeight = Mathf.Max(.35f,height-doorHeight);
            Vector3 hs = header.localScale;
            hs.x = doorWidth;
            hs.y = headerHeight;
            header.localScale = hs;
            Vector3 hp = header.localPosition;
            hp.x = center.x;
            hp.y = bottom+doorHeight+headerHeight*.5f;
            hp.z = frontZ;
            header.localPosition = hp;

            EditorUtility.SetDirty(left);
            EditorUtility.SetDirty(right);
            EditorUtility.SetDirty(header);
        }

        static void BuildHumanDoor(
            Transform root,
            float centerX,
            float floorY,
            float frontZ,
            float width,
            float height,
            Mats mats)
        {
            float jamb=.12f;

            Box(root,"Facade Door Frame Left",
                new Vector3(centerX-width*.5f-jamb*.5f,floorY+height*.5f,frontZ+.08f),
                new Vector3(jamb,height+.12f,.20f),mats.trim,true);
            Box(root,"Facade Door Frame Right",
                new Vector3(centerX+width*.5f+jamb*.5f,floorY+height*.5f,frontZ+.08f),
                new Vector3(jamb,height+.12f,.20f),mats.trim,true);
            Box(root,"Facade Door Frame Header",
                new Vector3(centerX,floorY+height+.055f,frontZ+.08f),
                new Vector3(width+jamb*2f,.11f,.20f),mats.trim,true);

            float leafWidth=width-.10f;
            Quaternion openRotation=Quaternion.Euler(0f,-82f,0f);
            Vector3 hinge=new Vector3(centerX-width*.5f+.05f,floorY+height*.5f,frontZ-.02f);
            Vector3 doorCenter=hinge+openRotation*Vector3.right*(leafWidth*.5f);

            Transform door=Box(root,"Facade Door",doorCenter,
                new Vector3(leafWidth,height-.12f,.075f),mats.door,true,openRotation);

            Sphere(door,"Facade Door Knob",
                new Vector3(leafWidth*.36f,0f,-.07f),.055f,mats.brass);
        }

        static void DisableLegacyFacadeDecor(Transform building, Transform preserve)
        {
            Transform[] all=building.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(!t || t==building || IsDescendantOf(t,preserve)) continue;

                string n=t.name;
                bool doorPart =
                    n.StartsWith("Door Opening Infill",StringComparison.Ordinal) ||
                    n.StartsWith("Door Frame",StringComparison.Ordinal) ||
                    n=="Door Threshold" ||
                    n.StartsWith("Open Service Door",StringComparison.Ordinal) ||
                    n=="Open Door" ||
                    n=="Door Knob" ||
                    n.StartsWith("Safety Bollard",StringComparison.Ordinal);

                bool oldFacade =
                    n=="Awning" ||
                    n.StartsWith("Sign -",StringComparison.Ordinal) ||
                    n.StartsWith("World Label -",StringComparison.Ordinal);

                if(doorPart || oldFacade)
                    t.gameObject.SetActive(false);
            }

            TextMesh[] labels=building.GetComponentsInChildren<TextMesh>(true);
            for(int i=0;i<labels.Length;i++)
            {
                TextMesh tm=labels[i];
                if(!tm || IsDescendantOf(tm.transform,preserve)) continue;
                tm.gameObject.SetActive(false);
            }
        }

        static int NormalizeCivicLabels(Transform world)
        {
            H51CitizenWorkplace[] workplaces=world.GetComponentsInChildren<H51CitizenWorkplace>(true);
            int changed=0;

            for(int i=0;i<workplaces.Length;i++)
            {
                H51CitizenWorkplace workplace=workplaces[i];
                if(!workplace) continue;
                string category=(workplace.Category??string.Empty).ToLowerInvariant();

                if(category!="entertainment" && category!="medical" && category!="police")
                    continue;

                TextMesh[] texts=workplace.GetComponentsInChildren<TextMesh>(true);
                if(texts.Length==0) continue;

                string label;
                if(category=="entertainment")
                    label=workplace.DisplayName.IndexOf("Cinema",StringComparison.OrdinalIgnoreCase)>=0
                        ? "CINEMA"
                        : "COMMUNITY THEATER";
                else if(category=="medical")
                    label=workplace.DisplayName.IndexOf("Hospital",StringComparison.OrdinalIgnoreCase)>=0
                        ? "REGIONAL HOSPITAL"
                        : "CLINIC";
                else
                    label="POLICE";

                for(int t=0;t<texts.Length;t++)
                {
                    TextMesh tm=texts[t];
                    if(!tm || !tm.gameObject.activeSelf) continue;
                    if(tm.text==label) continue;
                    tm.text=label;
                    tm.characterSize=label.Length>15?.115f:label.Length>10?.14f:.18f;
                    EditorUtility.SetDirty(tm);
                    changed++;
                }
            }

            return changed;
        }

        static int RebuildNavMeshes(Transform citizenRoot)
        {
            NavMeshSurface[] surfaces=citizenRoot.GetComponentsInChildren<NavMeshSurface>(true);
            int built=0;
            for(int i=0;i<surfaces.Length;i++)
            {
                NavMeshSurface surface=surfaces[i];
                if(!surface) continue;
                surface.RemoveData();
                surface.BuildNavMesh();
                if(surface.navMeshData) built++;
                EditorUtility.SetDirty(surface);
            }
            return built;
        }

        static Transform FindBestRoof(Transform root)
        {
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            Transform best=null;
            float bestY=float.NegativeInfinity;
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(!t || !IsRoofName(t.name) || !t.GetComponent<Renderer>()) continue;
                if(t.position.y<=bestY) continue;
                bestY=t.position.y;
                best=t;
            }
            return best;
        }

        static bool IsRoofName(string name)
        {
            if(string.IsNullOrWhiteSpace(name)) return false;
            string lower=name.ToLowerInvariant();
            return lower=="roof" ||
                   lower.EndsWith(" roof") ||
                   lower.StartsWith("roof ") ||
                   lower.Contains("pitched roof") ||
                   lower.Contains("flat roof");
        }

        static Material RendererMaterial(Transform t)
        {
            Renderer r=t?t.GetComponent<Renderer>():null;
            return r?r.sharedMaterial:null;
        }

        static string ShortLabel(string value)
        {
            string lower=(value??string.Empty).ToLowerInvariant();
            if(lower.Contains("farm supply")) return "FARM SUPPLY";
            if(lower.Contains("post office")) return "POST OFFICE";
            if(lower.Contains("fire station")) return "FIRE STATION";
            if(lower.Contains("repair garage")) return "REPAIR GARAGE";
            if(lower.Contains("general store")) return "GENERAL STORE";
            if(lower.Contains("community store")) return "COMMUNITY STORE";
            if(lower.Contains("town hall")) return "TOWN HALL";
            if(lower.Contains("community hall")) return "COMMUNITY HALL";
            if(lower.Contains("clinic")) return "CLINIC";
            if(lower.Contains("cafe")) return "CAFE";
            return (value??"WORKPLACE").ToUpperInvariant();
        }

        static Transform Box(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool collider,
            Quaternion? localRotation=null)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localRotation=localRotation??Quaternion.identity;
            go.transform.localScale=localScale;
            Renderer r=go.GetComponent<Renderer>();
            if(r) r.sharedMaterial=material;
            Collider c=go.GetComponent<Collider>();
            if(!collider && c) UnityEngine.Object.DestroyImmediate(c);
            return go.transform;
        }

        static Transform Sphere(
            Transform parent,
            string name,
            Vector3 localPosition,
            float scale,
            Material material)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            go.transform.localScale=Vector3.one*scale;
            Renderer r=go.GetComponent<Renderer>();
            if(r) r.sharedMaterial=material;
            Collider c=go.GetComponent<Collider>();
            if(c) UnityEngine.Object.DestroyImmediate(c);
            return go.transform;
        }

        static void WorldText(
            Transform parent,
            string text,
            Vector3 localPosition,
            Quaternion localRotation,
            float size)
        {
            Transform t=New("World Label - "+text,parent);
            t.localPosition=localPosition;
            t.localRotation=localRotation;

            TextMesh mesh=t.gameObject.AddComponent<TextMesh>();
            mesh.text=text;
            mesh.anchor=TextAnchor.MiddleCenter;
            mesh.alignment=TextAlignment.Center;
            mesh.fontSize=64;
            mesh.characterSize=size;
            mesh.color=Color.white;

            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(font) mesh.font=font;

            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(DepthTextShaderPath)
                        ?? Shader.Find("Hanger51/Depth World Text");
            if(!shader || !font || !font.material) return;

            string path=Generated+"/Materials/FacadeDepthText.mat";
            Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat)
            {
                mat=new Material(shader){name="Facade Depth Text"};
                mat.SetTexture("_MainTex",font.material.mainTexture);
                if(mat.HasProperty("_Color")) mat.SetColor("_Color",Color.white);
                if(mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff",.02f);
                mat.renderQueue=2450;
                AssetDatabase.CreateAsset(mat,path);
            }

            Renderer renderer=mesh.GetComponent<Renderer>();
            if(renderer)
            {
                renderer.sharedMaterial=mat;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
        }

        static Mats BuildMaterials()
        {
            return new Mats
            {
                trim=Mat("Facade_Trim",new Color(.72f,.72f,.68f),.08f),
                door=Mat("Facade_Door",new Color(.24f,.12f,.06f),.06f),
                brass=Mat("Facade_Brass",new Color(.55f,.38f,.10f),.20f),
                awning=Mat("Facade_Awning",new Color(.25f,.08f,.07f),.04f),
                sign=Mat("Facade_Sign",new Color(.08f,.09f,.10f),.03f),
                fireDoor=Mat("Facade_Service_Door",new Color(.23f,.25f,.26f),.08f)
            };
        }

        static Material Mat(string name,Color color,float smoothness)
        {
            string path=Generated+"/Materials/"+name+".mat";
            Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!mat)
            {
                mat=new Material(shader){name=name};
                AssetDatabase.CreateAsset(mat,path);
            }
            mat.color=color;
            if(mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color);
            if(mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic",0f);
            if(mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness",smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject Find(string name)
        {
            GameObject direct=GameObject.Find(name);
            if(direct) return direct;
            Transform[] all=UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,FindObjectsSortMode.None);
            for(int i=0;i<all.Length;i++)
                if(all[i] && all[i].gameObject.scene.IsValid() && all[i].name==name)
                    return all[i].gameObject;
            return null;
        }

        static Transform DirectChild(Transform root,string name)
        {
            if(!root) return null;
            for(int i=0;i<root.childCount;i++)
            {
                Transform child=root.GetChild(i);
                if(child.name==name) return child;
            }
            return null;
        }

        static Transform FindChild(Transform root,string name)
        {
            if(!root) return null;
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
                if(all[i] && all[i].name==name) return all[i];
            return null;
        }

        static bool IsDescendantOf(Transform item,Transform ancestor)
        {
            Transform t=item;
            while(t)
            {
                if(t==ancestor) return true;
                t=t.parent;
            }
            return false;
        }

        static Transform New(string name,Transform parent)
        {
            GameObject go=new GameObject(name);
            if(parent) go.transform.SetParent(parent,false);
            return go.transform;
        }

        static void Ensure(string path)
        {
            string[] pieces=path.Split('/');
            string current=pieces[0];
            for(int i=1;i<pieces.Length;i++)
            {
                string next=current+"/"+pieces[i];
                if(!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current,pieces[i]);
                current=next;
            }
        }
    }
}
