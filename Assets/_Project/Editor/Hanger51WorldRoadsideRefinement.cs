using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldRoadsideRefinement
    {
        const string WorldName="Hanger 51 Surrounding Countryside";
        const string TerrainName="Hanger 51 Editable Terrain";
        const string PassName="Hanger 51 Lived-In Countryside Detail";
        const string RefineName="Hanger 51 Roadside Refinement";
        const string BaseGen="Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen="Assets/_Project/Environment/Generated/CountrysideRoadsideRefinement";
        const string Grass1="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string Grass2="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string Leaf="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string Fir="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string TextShader="Assets/_Project/Shaders/Hanger51DepthWorldText.shader";
        const int Seed=51102;
        static int meshId;

        struct Mats
        {
            public Material asphalt,gravel,line,concrete,wood,metal,white,red,green,rubber;
        }

        [MenuItem("Hanger 51/World/Current/102 - Refine Roads Utilities Foliage")]
        public static void Build()
        {
            Hanger51WorldCountrysideLivedInPass.Build();
            GameObject world=Find(WorldName),pass=Find(PassName);Terrain terrain=FindTerrain();
            if(!world||!pass||!terrain){Debug.LogError("Step 102 could not find the Step 100 countryside.");return;}
            Transform roads=DirectChild(world.transform,"Road Network"),settlements=FindChild(world.transform,"Settlements");
            if(!roads||!settlements){Debug.LogError("Step 102 could not find the road network or settlements.",world);return;}
            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Preparing refinement pass",.03f);
                GameObject old=Find(RefineName);if(old)UnityEngine.Object.DestroyImmediate(old);
                ResetFolder();meshId=0;Transform root=New(RefineName,pass.transform);Mats m=LoadMats();

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Making every road hug the terrain",.12f);
                ConformExistingRoads(terrain,roads);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Connecting county roads directly into towns",.25f);
                int connectors=ConnectTowns(terrain,roads,settlements,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Rebuilding utilities along actual road paths",.40f);
                RemoveOldUtilityCorridors(pass.transform);int poles=AddRoadFollowingUtilities(terrain,roads,root,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Moving and rebuilding bus stops beside roads",.53f);
                RemoveOldBusStops(world.transform);int busStops=AddBusStops(terrain,roads,settlements,root,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Adding home and business road access",.64f);
                int driveways=AddDriveways(terrain,roads,settlements,pass.transform,root,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Adding dense asset grass and town foliage",.76f);
                int grass=AddRoadsideGrass(terrain,roads,settlements,root);int trees=AddTownFoliage(terrain,roads,settlements,root);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Adding roadside furniture and town detail",.87f);
                int details=AddRoadsideDetails(terrain,roads,settlements,root,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Refinement","Fixing world-space text depth and direction",.95f);
                int text=FixWorldText(world.transform);

                terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();Selection.activeGameObject=root.gameObject;
                Debug.Log($"Step 102 complete. terrain-conforming roads, town connectors={connectors}, road-following utility poles={poles}, roadside bus stops={busStops}, driveways={driveways}, new asset grass={grass}, new town trees={trees}, roadside details={details}, depth-fixed text={text}.",root.gameObject);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        [MenuItem("Hanger 51/World/Current/103 - Validate Roadside Refinement")]
        public static void Validate()
        {
            GameObject world=Find(WorldName),root=Find(RefineName);Terrain t=FindTerrain();if(!world||!root||!t){Debug.LogError("Step 103 failed: run Step 102 first.");return;}
            Transform roads=DirectChild(world.transform,"Road Network");int buried=0,roadVerts=0;
            if(roads)foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))if(mf&&mf.sharedMesh&&mf.gameObject.name=="Road Surface")foreach(Vector3 v in mf.sharedMesh.vertices){Vector3 w=mf.transform.TransformPoint(v);roadVerts++;if(w.y<Ground(t,w)+.025f)buried++;}
            int connectors=Count(roads,"Town Connector -"),poles=Count(root.transform,"Road Utility Pole"),wires=Count(root.transform,"Road Power Wire"),bus=Count(root.transform,"Roadside Bus Stop"),grass=Count(root.transform,"Roadside Asset Grass"),trees=Count(root.transform,"Refinement Town Tree"),details=Count(root.transform,"Roadside Detail"),driveways=Count(root.transform,"Driveway");
            int badText=0,totalText=0;Shader expected=AssetDatabase.LoadAssetAtPath<Shader>(TextShader);foreach(TextMesh tm in world.GetComponentsInChildren<TextMesh>(true)){totalText++;Renderer r=tm.GetComponent<Renderer>();if(!r||!r.sharedMaterial||r.sharedMaterial.shader!=expected)badText++;}
            float worstBus=0;foreach(Transform tr in root.GetComponentsInChildren<Transform>(true))if(tr.name.StartsWith("Roadside Bus Stop"))worstBus=Mathf.Max(worstBus,DistanceToRoadNetwork(tr.position,roads));
            float worstPole=0;foreach(Transform tr in root.GetComponentsInChildren<Transform>(true))if(tr.name.StartsWith("Road Utility Pole"))worstPole=Mathf.Max(worstPole,DistanceToRoadNetwork(tr.position,roads));
            bool ok=buried==0&&connectors>=4&&poles>=35&&wires>=60&&bus>=5&&grass>=700&&trees>=45&&driveways>=60&&details>=80&&badText==0&&worstBus<18&&worstPole<20;
            if(ok)Debug.Log($"Step 103 passed. road vertices={roadVerts} buried={buried}, connectors={connectors}, poles={poles}, wires={wires}, bus stops={bus}, driveways={driveways}, asset grass={grass}, town trees={trees}, roadside details={details}, text={totalText}, farthest bus from road={worstBus:0.0}m, farthest pole from road={worstPole:0.0}m.",root);
            else Debug.LogError($"Step 103 failed. road vertices={roadVerts} buried={buried}, connectors={connectors}, poles={poles}, wires={wires}, bus stops={bus}, driveways={driveways}, asset grass={grass}, town trees={trees}, roadside details={details}, bad text={badText}/{totalText}, farthest bus={worstBus:0.0}m, farthest pole={worstPole:0.0}m.",root);
        }

        static Mats LoadMats()
        {
            Mats m=new Mats();m.asphalt=LoadMat("Matte_Asphalt");m.gravel=LoadMat("Matte_Gravel");m.line=LoadMat("Road_Paint");m.concrete=LoadMat("Concrete");m.wood=LoadMat("Weathered_Wood");m.metal=LoadMat("Dark_Metal");m.white=LoadMat("Warm_White");m.red=LoadMat("Barn_Red");m.green=LoadMat("Farm_Green");m.rubber=LoadMat("Rubber");return m;
        }
        static Material LoadMat(string n){Material m=AssetDatabase.LoadAssetAtPath<Material>(BaseGen+"/Materials/"+n+".mat");if(!m)Debug.LogWarning("Step 102 could not load material "+n+" from Step 100.");return m;}

        static void ConformExistingRoads(Terrain t,Transform roadRoot)
        {
            List<Transform> roots=DirectChildren(roadRoot);foreach(Transform r in roots){List<Vector3> path=RoadPath(r);if(path.Count<2)continue;path=Resample(path,5f);ConformNamed(t,r,path,"Gravel Shoulder",.035f);ConformNamed(t,r,path,"Road Surface",.095f);ConformNamed(t,r,path,"Center Line",.125f);}
        }
        static void ConformNamed(Terrain t,Transform road,List<Vector3> path,string name,float offset){Transform h=DirectChild(road,name);if(!h)return;float w=MeshWidth(h);if(w<=.01f)w=name=="Center Line"?.18f:name=="Road Surface"?6f:12f;Mesh mesh=RibbonMesh(t,h,path,w,offset,name);MeshFilter mf=h.GetComponent<MeshFilter>();if(mf)mf.sharedMesh=mesh;MeshCollider mc=h.GetComponent<MeshCollider>();if(mc){mc.sharedMesh=null;mc.sharedMesh=mesh;}}

        static int ConnectTowns(Terrain t,Transform roads,Transform settlements,Mats m)
        {
            int made=0;List<Transform> external=new List<Transform>();foreach(Transform r in DirectChildren(roads))if(r.name.StartsWith("County Road")||r.name.StartsWith("Airport Access Road"))external.Add(r);
            Transform connectorRoot=New("Town Road Connections",roads);
            for(int ti=0;ti<settlements.childCount;ti++)
            {
                Transform town=settlements.GetChild(ti);List<Transform> local=new List<Transform>();foreach(Transform r in DirectChildren(roads))if(r.name.StartsWith(town.name))local.Add(r);if(local.Count==0||external.Count==0)continue;
                Vector3 bestA=Vector3.zero,bestB=Vector3.zero;float best=float.MaxValue;
                foreach(Transform lr in local){List<Vector3> lp=RoadPath(lr);if(lp.Count<2)continue;Vector3[] ends={lp[0],lp[lp.Count-1]};foreach(Vector3 a in ends)foreach(Transform er in external){List<Vector3> ep=RoadPath(er);foreach(Vector3 b in ep){float d=Planar(a,b);if(d<best){best=d;bestA=a;bestB=b;}}}}
                if(best==float.MaxValue)continue;Vector3 delta=bestB-bestA;delta.y=0;if(delta.magnitude<1)continue;Vector3 side=Vector3.Cross(Vector3.up,delta.normalized);Vector3 mid=(bestA+bestB)*.5f+side*Mathf.Clamp(best*.08f,0,35)*(ti%2==0?1:-1);int n=Mathf.Clamp(Mathf.CeilToInt(best/5f),3,100);List<Vector3> p=new List<Vector3>();for(int i=0;i<=n;i++){float u=i/(float)n;Vector3 x=Bezier(bestA,mid,bestB,u);x.y=Ground(t,x)+.04f;p.Add(x);}Transform rr=New("Town Connector - "+town.name,connectorRoot);AddRoadPieces(t,rr,p,6.5f,true,m);made++;
            }
            return made;
        }

        static void AddRoadPieces(Terrain t,Transform root,List<Vector3> p,float width,bool line,Mats m)
        {
            MakeRoadMeshObject(t,root,"Gravel Shoulder",p,width+7,m.gravel,.035f,false);MakeRoadMeshObject(t,root,"Road Surface",p,width,m.asphalt,.095f,true);if(line)MakeRoadMeshObject(t,root,"Center Line",p,.18f,m.line,.125f,false);
        }
        static void MakeRoadMeshObject(Terrain t,Transform root,string n,List<Vector3> p,float w,Material m,float y,bool collider){GameObject g=new GameObject(n);g.transform.SetParent(root,false);Mesh mesh=RibbonMesh(t,g.transform,p,w,y,n);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=m;if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;}

        static int AddRoadFollowingUtilities(Terrain t,Transform roads,Transform root,Mats m)
        {
            Transform util=New("Road Following Utilities",root);int poleCount=0,roadIndex=0;
            foreach(Transform rr in DirectChildrenRecursiveRoads(roads))
            {
                bool county=rr.name.StartsWith("County Road")||rr.name.StartsWith("Airport Access Road");bool townMain=rr.name.Contains("Avenue 2")||rr.name.Contains("Street 2")||rr.name.StartsWith("Town Connector -");if(!county&&!townMain)continue;List<Vector3> path=RoadPath(rr);if(path.Count<2)continue;path=Resample(path,county?58f:48f);Vector3[] last=new Vector3[3];bool have=false;int sideSign=(roadIndex++%2==0)?1:-1;
                for(int i=0;i<path.Count;i++)
                {
                    Vector3 tan=Tangent(path,i),side=Vector3.Cross(Vector3.up,tan).normalized;Vector3 p=path[i]+side*(county?11f:8.5f)*sideSign;p.y=Ground(t,p);Transform pole=New("Road Utility Pole "+(++poleCount).ToString("000"),util);pole.position=p;pole.rotation=Quaternion.LookRotation(tan,Vector3.up);Cylinder(pole,"Pole",new Vector3(0,4.6f,0),new Vector3(.16f,4.6f,.16f),m.wood,false);Box(pole,"Crossarm",new Vector3(0,8.45f,0),new Vector3(2.5f,.16f,.16f),m.wood,false);for(int q=-1;q<=1;q++){Cylinder(pole,"Insulator",new Vector3(q*.82f,8.66f,0),new Vector3(.07f,.18f,.07f),m.white,false);Vector3 now=pole.TransformPoint(new Vector3(q*.82f,8.82f,0));if(have)Wire(util,last[q+1],now,m.metal,"Road Power Wire");last[q+1]=now;}have=true;
                }
            }
            return poleCount;
        }

        static void RemoveOldUtilityCorridors(Transform pass)
        {
            List<GameObject> kill=new List<GameObject>();foreach(Transform tr in pass.GetComponentsInChildren<Transform>(true))if(tr.name.StartsWith("Utility Corridor -"))kill.Add(tr.gameObject);foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);
        }
        static void RemoveOldBusStops(Transform world)
        {
            List<GameObject> kill=new List<GameObject>();foreach(Transform tr in world.GetComponentsInChildren<Transform>(true)){string n=tr.name.ToLowerInvariant();if((n.Contains("bus stop")||n.Contains("busstop"))&&(tr.parent==null||(!tr.parent.name.ToLowerInvariant().Contains("bus stop")&&!tr.parent.name.ToLowerInvariant().Contains("busstop"))))kill.Add(tr.gameObject);}foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);
        }

        static int AddBusStops(Terrain t,Transform roads,Transform settlements,Transform root,Mats m)
        {
            Transform busRoot=New("Roadside Bus Stops",root);int made=0;
            for(int ti=0;ti<settlements.childCount;ti++)
            {
                Transform town=settlements.GetChild(ti);Transform road=FindTownMainRoad(roads,town.name);if(!road)continue;List<Vector3> path=RoadPath(road);if(path.Count<2)continue;float[] f=ti==0?new[]{.28f,.72f}:new[]{.50f};
                for(int bi=0;bi<f.Length;bi++)
                {
                    Vector3 center,tan;PointTangentAtFraction(path,f[bi],out center,out tan);Vector3 side=Vector3.Cross(Vector3.up,tan).normalized*((bi+ti)%2==0?1:-1);float roadWidth=RoadSurfaceWidth(road);Vector3 p=center+side*(roadWidth*.5f+6f);p.y=Ground(t,p);Transform b=New("Roadside Bus Stop - "+town.name+" "+(bi+1),busRoot);b.position=p;b.rotation=Quaternion.LookRotation(-side,Vector3.up);
                    Box(b,"Bus Stop Pad",new Vector3(0,.06f,0),new Vector3(7,.12f,3.8f),m.concrete,false);Box(b,"Shelter Back",new Vector3(0,1.5f,-1.55f),new Vector3(6.2f,3,.12f),m.metal,false);Box(b,"Shelter Roof",new Vector3(0,3.1f,-.25f),new Vector3(6.5f,.18f,3.0f),m.metal,false);Box(b,"Bench Seat",new Vector3(0,.72f,-.75f),new Vector3(3.8f,.18f,.72f),m.wood,false);Box(b,"Bench Back",new Vector3(0,1.28f,-1.02f),new Vector3(3.8f,1.0f,.14f),m.wood,false);Box(b,"Route Sign Pole",new Vector3(3.25f,1.7f,.55f),new Vector3(.12f,3.4f,.12f),m.metal,false);Box(b,"Route Sign",new Vector3(3.25f,3.15f,.55f),new Vector3(1.15f,1.15f,.10f),m.white,false);WorldLabel(b,"BUS",new Vector3(3.25f,3.15f,.61f),.20f);Box(b,"Trash Can",new Vector3(-2.8f,.55f,-.5f),new Vector3(.7f,1.1f,.7f),m.green,false);made++;
                }
            }
            return made;
        }

        static int AddDriveways(Terrain t,Transform roads,Transform settlements,Transform pass,Transform root,Mats m)
        {
            Transform droot=New("Road Access Driveways",root);int made=0;
            for(int ti=0;ti<settlements.childCount;ti++)
            {
                Transform town=settlements.GetChild(ti);List<List<Vector3>> local=TownRoadPaths(roads,town.name);if(local.Count==0)continue;for(int hi=0;hi<town.childCount;hi++)
                {
                    Transform h=town.GetChild(hi);if(!h.name.StartsWith("Detailed House")&&!h.name.StartsWith("Building"))continue;Vector3 start=h.position+h.forward*9f;Vector3 end=NearestRoadPoint(start,local);float d=Planar(start,end);if(d<3||d>75)continue;List<Vector3> p=LinePath(t,start,end,3.5f);Transform dr=New("Driveway - "+town.name+" "+hi.ToString("000"),droot);MakeRoadMeshObject(t,dr,"Driveway Surface",p,3.1f,m.gravel,.075f,false);made++;
                }
            }
            foreach(Transform tr in pass.GetComponentsInChildren<Transform>(true))if(tr.name.StartsWith("Purpose -"))
            {
                Vector3 start=tr.position+tr.forward*12f;Vector3 end=NearestRoadPoint(start,AllRoadPaths(roads));float d=Planar(start,end);if(d<4||d>90)continue;List<Vector3> p=LinePath(t,start,end,4f);Transform dr=New("Business Driveway - "+tr.name,droot);MakeRoadMeshObject(t,dr,"Driveway Surface",p,4.5f,m.concrete,.08f,false);made++;
            }
            return made;
        }

        static int AddRoadsideGrass(Terrain t,Transform roads,Transform settlements,Transform root)
        {
            GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(Grass1),b=AssetDatabase.LoadAssetAtPath<GameObject>(Grass2);if(!a&&!b)return 0;Transform gr=New("Dense Roadside Asset Grass",root);System.Random rng=new System.Random(Seed+1);int made=0;
            foreach(Transform rr in DirectChildrenRecursiveRoads(roads))
            {
                List<Vector3> p=RoadPath(rr);if(p.Count<2)continue;p=Resample(p,11f);float w=RoadSurfaceWidth(rr);for(int i=0;i<p.Count&&made<1250;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){if(rng.NextDouble()<.30)continue;Vector3 q=p[i]+side*s*(w*.5f+Next(rng,3.0f,9.5f))+tan*Next(rng,-3,3);q.y=Ground(t,q);SpawnGrass((rng.NextDouble()<.5?a:b)??a??b,gr,q,rng,ref made);if(rng.NextDouble()<.35&&made<1250){Vector3 q2=q+side*s*Next(rng,1.2f,3.5f)+tan*Next(rng,-2.5f,2.5f);q2.y=Ground(t,q2);SpawnGrass((rng.NextDouble()<.5?a:b)??a??b,gr,q2,rng,ref made);}}}
            }
            for(int ti=0;ti<settlements.childCount&&made<1500;ti++)for(int hi=0;hi<settlements.GetChild(ti).childCount&&made<1500;hi++)
            {
                Transform h=settlements.GetChild(ti).GetChild(hi);if(!h.name.StartsWith("Detailed House")&&!h.name.StartsWith("Building"))continue;for(int k=0;k<3&&made<1500;k++){Vector3 q=h.position+h.right*Next(rng,-10,10)-h.forward*Next(rng,7,15);q.y=Ground(t,q);SpawnGrass((rng.NextDouble()<.5?a:b)??a??b,gr,q,rng,ref made);}
            }
            return made;
        }
        static void SpawnGrass(GameObject src,Transform parent,Vector3 p,System.Random rng,ref int made){if(!src)return;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)return;g.name="Roadside Asset Grass "+(++made).ToString("0000");g.transform.SetParent(parent,false);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.75f,1.45f);foreach(Collider c in g.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);}

        static int AddTownFoliage(Terrain t,Transform roads,Transform settlements,Transform root)
        {
            GameObject leaf=AssetDatabase.LoadAssetAtPath<GameObject>(Leaf),fir=AssetDatabase.LoadAssetAtPath<GameObject>(Fir);if(!leaf&&!fir)return 0;Transform tr=New("Refinement Town Foliage",root);System.Random rng=new System.Random(Seed+2);int made=0;List<List<Vector3>> all=AllRoadPaths(roads);
            for(int ti=0;ti<settlements.childCount;ti++)for(int hi=0;hi<settlements.GetChild(ti).childCount;hi++)
            {
                Transform h=settlements.GetChild(ti).GetChild(hi);if(!h.name.StartsWith("Detailed House")&&!h.name.StartsWith("Building")||rng.NextDouble()>.62)continue;Vector3 q=h.position-h.forward*Next(rng,10,18)+h.right*(rng.NextDouble()<.5?-1:1)*Next(rng,8,15);q.y=Ground(t,q);if(DistanceToPaths(q,all)<8)continue;GameObject src=rng.NextDouble()<.82?leaf:fir;if(!src)src=leaf??fir;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)continue;g.name="Refinement Town Tree "+(++made).ToString("000");g.transform.SetParent(tr,false);g.transform.position=q;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.68f,1.05f);
            }
            return made;
        }

        static int AddRoadsideDetails(Terrain t,Transform roads,Transform settlements,Transform root,Mats m)
        {
            Transform d=New("Roadside Details",root);int made=0;System.Random rng=new System.Random(Seed+3);
            foreach(Transform rr in DirectChildrenRecursiveRoads(roads))if(rr.name.StartsWith("County Road")||rr.name.StartsWith("Airport Access Road"))
            {
                List<Vector3> p=RoadPath(rr);if(p.Count<2)continue;p=Resample(p,72f);float w=RoadSurfaceWidth(rr);for(int i=0;i<p.Count;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){Vector3 q=p[i]+side*s*(w*.5f+4.2f);q.y=Ground(t,q);Transform post=New("Roadside Detail Delineator "+(++made).ToString("000"),d);post.position=q;post.rotation=Quaternion.LookRotation(tan,Vector3.up);Box(post,"White Post",new Vector3(0,.65f,0),new Vector3(.13f,1.3f,.13f),m.white,false);Box(post,"Reflector",new Vector3(0,1.12f,-.08f),new Vector3(.18f,.20f,.04f),m.red,false);}}
            }
            if(settlements.childCount>0)
            {
                Transform town=settlements.GetChild(0);Transform main=FindTownMainRoad(roads,town.name);if(main){List<Vector3> p=Resample(RoadPath(main),58f);float w=RoadSurfaceWidth(main);for(int i=0;i<p.Count;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;Vector3 q=p[i]+side*(w*.5f+5.5f);q.y=Ground(t,q);Transform lamp=New("Roadside Detail Street Lamp "+(++made).ToString("000"),d);lamp.position=q;lamp.rotation=Quaternion.LookRotation(tan,Vector3.up);Cylinder(lamp,"Lamp Pole",new Vector3(0,3.7f,0),new Vector3(.09f,3.7f,.09f),m.metal,false);Box(lamp,"Lamp Arm",new Vector3(-1.0f,7.0f,0),new Vector3(2,.10f,.10f),m.metal,false);Box(lamp,"Lamp Head",new Vector3(-2.0f,6.9f,0),new Vector3(.7f,.18f,.38f),m.white,false);if(i%2==0){Transform hydrant=New("Roadside Detail Fire Hydrant "+(++made).ToString("000"),d);Vector3 hp=p[i]-side*(w*.5f+4.5f);hp.y=Ground(t,hp);hydrant.position=hp;Cylinder(hydrant,"Hydrant Body",new Vector3(0,.55f,0),new Vector3(.30f,.55f,.30f),m.red,false);Cylinder(hydrant,"Hydrant Cap",new Vector3(0,1.15f,0),new Vector3(.38f,.14f,.38f),m.red,false);Box(hydrant,"Hydrant Side",new Vector3(.38f,.70f,0),new Vector3(.35f,.22f,.22f),m.red,false);}}}
            }
            return made;
        }

        static int FixWorldText(Transform world)
        {
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(TextShader);if(!shader){Debug.LogWarning("Step 102 could not load the depth world text shader.");return 0;}int fixedCount=0;Dictionary<string,Material> mats=new Dictionary<string,Material>();
            foreach(TextMesh tm in world.GetComponentsInChildren<TextMesh>(true))
            {
                Renderer r=tm.GetComponent<Renderer>();if(!r)continue;Texture tex=null;if(r.sharedMaterial)tex=r.sharedMaterial.mainTexture;if(!tex&&tm.font&&tm.font.material)tex=tm.font.material.mainTexture;string key=tm.font?tm.font.name:"Default";Material mat;if(!mats.TryGetValue(key,out mat)){string path=Gen+"/Materials/DepthText_"+Safe(key)+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(shader){name="H51 Depth Text "+key};if(tex)mat.SetTexture("_MainTex",tex);mat.SetColor("_Color",Color.white);mat.SetFloat("_Cutoff",.10f);AssetDatabase.CreateAsset(mat,path);}mats[key]=mat;}r.sharedMaterial=mat;tm.transform.localRotation=tm.transform.localRotation*Quaternion.Euler(0,180,0);fixedCount++;
            }
            return fixedCount;
        }
        static void WorldLabel(Transform p,string text,Vector3 pos,float scale){GameObject g=new GameObject("Sign - "+text);g.transform.SetParent(p,false);g.transform.localPosition=pos;TextMesh tm=g.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.characterSize=.5f;tm.fontSize=42;tm.color=Color.white;g.transform.localScale=Vector3.one*scale;}

        static List<Vector3> RoadPath(Transform road)
        {
            Transform s=FindChild(road,"Road Surface");if(!s)return new List<Vector3>();MeshFilter mf=s.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return new List<Vector3>();Vector3[] v=mf.sharedMesh.vertices;List<Vector3> p=new List<Vector3>();for(int i=0;i+1<v.Length;i+=2){Vector3 a=s.TransformPoint(v[i]),b=s.TransformPoint(v[i+1]);p.Add((a+b)*.5f);}return p;
        }
        static float RoadSurfaceWidth(Transform road){Transform s=FindChild(road,"Road Surface");return s?MeshWidth(s):6f;}
        static float MeshWidth(Transform holder){MeshFilter mf=holder.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh||mf.sharedMesh.vertexCount<2)return 0;Vector3[] v=mf.sharedMesh.vertices;return Vector3.Distance(holder.TransformPoint(v[0]),holder.TransformPoint(v[1]));}
        static Mesh RibbonMesh(Terrain t,Transform holder,List<Vector3> path,float width,float yOffset,string name)
        {
            int c=path.Count;Vector3[] v=new Vector3[c*2];Vector2[] uv=new Vector2[c*2];int[] tri=new int[Mathf.Max(0,(c-1)*6)];float dist=0;for(int i=0;i<c;i++){Vector3 tan=Tangent(path,i),side=Vector3.Cross(Vector3.up,tan).normalized*width*.5f;Vector3 l=path[i]-side,r=path[i]+side;l.y=Ground(t,l)+yOffset;r.y=Ground(t,r)+yOffset;if(i>0)dist+=Planar(path[i-1],path[i]);v[i*2]=holder.InverseTransformPoint(l);v[i*2+1]=holder.InverseTransformPoint(r);uv[i*2]=new Vector2(0,dist/7f);uv[i*2+1]=new Vector2(1,dist/7f);if(i<c-1){int q=i*6,j=i*2;tri[q]=j;tri[q+1]=j+2;tri[q+2]=j+1;tri[q+3]=j+1;tri[q+4]=j+2;tri[q+5]=j+3;}}
            Mesh mesh=new Mesh{name="H51_102_"+Safe(name)+"_"+(meshId++).ToString("0000")};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Gen+"/Meshes/"+mesh.name+".asset");return mesh;
        }
        static List<Vector3> Resample(List<Vector3> p,float step){List<Vector3> o=new List<Vector3>();if(p.Count==0)return o;o.Add(p[0]);for(int i=0;i<p.Count-1;i++){Vector3 a=p[i],b=p[i+1];float d=Planar(a,b);int n=Mathf.Max(1,Mathf.CeilToInt(d/step));for(int k=1;k<=n;k++)o.Add(Vector3.Lerp(a,b,k/(float)n));}return o;}
        static List<Vector3> LinePath(Terrain t,Vector3 a,Vector3 b,float step){List<Vector3> p=new List<Vector3>();int n=Mathf.Max(2,Mathf.CeilToInt(Planar(a,b)/step));for(int i=0;i<=n;i++){Vector3 q=Vector3.Lerp(a,b,i/(float)n);q.y=Ground(t,q)+.03f;p.Add(q);}return p;}
        static Vector3 Tangent(List<Vector3> p,int i){Vector3 d=i==0?p[1]-p[0]:i==p.Count-1?p[p.Count-1]-p[p.Count-2]:p[i+1]-p[i-1];d.y=0;return d.sqrMagnitude<.001f?Vector3.forward:d.normalized;}
        static void PointTangentAtFraction(List<Vector3> p,float f,out Vector3 point,out Vector3 tangent){if(p.Count<2){point=p.Count==1?p[0]:Vector3.zero;tangent=Vector3.forward;return;}float total=0;for(int i=0;i<p.Count-1;i++)total+=Planar(p[i],p[i+1]);float want=total*Mathf.Clamp01(f),run=0;for(int i=0;i<p.Count-1;i++){float d=Planar(p[i],p[i+1]);if(run+d>=want){float u=d<.001f?0:(want-run)/d;point=Vector3.Lerp(p[i],p[i+1],u);tangent=p[i+1]-p[i];tangent.y=0;tangent=tangent.sqrMagnitude<.001f?Vector3.forward:tangent.normalized;return;}run+=d;}point=p[p.Count-1];tangent=Tangent(p,p.Count-1);}

        static Transform FindTownMainRoad(Transform roads,string town){Transform fallback=null;foreach(Transform r in DirectChildrenRecursiveRoads(roads))if(r.name.StartsWith(town)){if(!fallback)fallback=r;if(r.name.Contains("Avenue 2")||r.name.Contains("Street 2"))return r;}return fallback;}
        static List<List<Vector3>> TownRoadPaths(Transform roads,string town){List<List<Vector3>> a=new List<List<Vector3>>();foreach(Transform r in DirectChildrenRecursiveRoads(roads))if(r.name.StartsWith(town)){List<Vector3> p=RoadPath(r);if(p.Count>1)a.Add(p);}return a;}
        static List<List<Vector3>> AllRoadPaths(Transform roads){List<List<Vector3>> a=new List<List<Vector3>>();foreach(Transform r in DirectChildrenRecursiveRoads(roads)){List<Vector3> p=RoadPath(r);if(p.Count>1)a.Add(p);}return a;}
        static Vector3 NearestRoadPoint(Vector3 p,List<List<Vector3>> paths){Vector3 best=p;float bd=float.MaxValue;foreach(List<Vector3> path in paths)for(int i=0;i<path.Count-1;i++){Vector3 q=ClosestXZ(p,path[i],path[i+1]);float d=Planar(p,q);if(d<bd){bd=d;best=q;}}return best;}
        static Vector3 ClosestXZ(Vector3 p,Vector3 a,Vector3 b){Vector2 q=new Vector2(p.x,p.z),x=new Vector2(a.x,a.z),d=new Vector2(b.x-a.x,b.z-a.z);float u=d.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(q-x,d)/d.sqrMagnitude);Vector3 r=Vector3.Lerp(a,b,u);return r;}
        static float DistanceToPaths(Vector3 p,List<List<Vector3>> paths){float b=float.MaxValue;foreach(List<Vector3> x in paths)for(int i=0;i<x.Count-1;i++)b=Mathf.Min(b,Planar(p,ClosestXZ(p,x[i],x[i+1])));return b==float.MaxValue?99999:b;}
        static float DistanceToRoadNetwork(Vector3 p,Transform roads){if(!roads)return 99999;return DistanceToPaths(p,AllRoadPaths(roads));}

        static List<Transform> DirectChildren(Transform p){List<Transform>a=new List<Transform>();for(int i=0;i<p.childCount;i++)a.Add(p.GetChild(i));return a;}
        static List<Transform> DirectChildrenRecursiveRoads(Transform roads){List<Transform>a=new List<Transform>();foreach(Transform r in roads.GetComponentsInChildren<Transform>(true))if(r!=roads&&FindChild(r,"Road Surface")&&r.GetComponent<MeshFilter>()==null)a.Add(r);return UniqueTopRoads(a);}
        static List<Transform> UniqueTopRoads(List<Transform>a){List<Transform>o=new List<Transform>();foreach(Transform r in a){bool nested=false;Transform p=r.parent;while(p){if(a.Contains(p)){nested=true;break;}p=p.parent;}if(!nested)o.Add(r);}return o;}

        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static GameObject Cylinder(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static void Wire(Transform p,Vector3 a,Vector3 b,Material m,string name){Vector3 d=b-a;if(d.sqrMagnitude<.01f)return;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(p,false);g.transform.position=(a+b)*.5f;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);g.transform.localScale=new Vector3(.022f,d.magnitude*.5f,.022f);if(m)g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;}

        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t){float u=1-t;return u*u*a+2*u*t*b+t*t*c;}
        static float Planar(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        static float Next(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
        static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
        static Terrain FindTerrain(){GameObject g=Find(TerrainName);Terrain t=g?(g.GetComponent<Terrain>()??g.GetComponentInChildren<Terrain>(true)):null;if(t)return t;Terrain[] a=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);return a.Length>0?a[0]:null;}
        static GameObject Find(string n){GameObject g=GameObject.Find(n);if(g)return g;foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t&&t.name==n&&t.gameObject.scene.IsValid())return t.gameObject;return null;}
        static Transform FindChild(Transform r,string n){foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;return null;}
        static Transform DirectChild(Transform r,string n){for(int i=0;i<r.childCount;i++)if(r.GetChild(i).name==n)return r.GetChild(i);return null;}
        static Transform New(string n,Transform p){GameObject g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
        static int Count(Transform r,string n){if(!r)return 0;int c=0;foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t!=r&&t.name.Contains(n))c++;return c;}
        static string Safe(string n){char[] bad=System.IO.Path.GetInvalidFileNameChars();foreach(char c in bad)n=n.Replace(c,'_');return n.Replace(' ','_');}
        static void ResetFolder(){if(AssetDatabase.IsValidFolder(Gen))AssetDatabase.DeleteAsset(Gen);Ensure(Gen+"/Materials");Ensure(Gen+"/Meshes");}
        static void Ensure(string path){string[] p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}}
    }
}
