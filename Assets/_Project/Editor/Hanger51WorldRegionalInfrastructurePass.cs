using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldRegionalInfrastructurePass
    {
        const string WorldName="Hanger 51 Surrounding Countryside";
        const string AirportName="Hanger 51 Airport Complex";
        const string TerrainName="Hanger 51 Editable Terrain";
        const string LivedName="Hanger 51 Lived-In Countryside Detail";
        const string RefineName="Hanger 51 Roadside Refinement";
        const string PassName="Hanger 51 Regional Infrastructure Pass";
        const string BaseGen="Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen="Assets/_Project/Environment/Generated/CountrysideRegionalInfrastructure";
        const string Grass1="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string Grass2="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string Leaf="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string Fir="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string TextShader="Assets/_Project/Shaders/Hanger51DepthWorldText.shader";
        const int Seed=51104;
        static int meshId;

        class Town
        {
            public Transform root;
            public string name;
            public Vector3 center;
            public List<Transform> roads=new List<Transform>();
        }

        class RegionalRoad
        {
            public string name;
            public List<Vector3> path=new List<Vector3>();
            public float width;
        }

        struct Mats
        {
            public Material asphalt,gravel,line,concrete,wood,metal,white,red,green,blue,glass,rubber,yellow,industrial,pipe,cooling;
        }

        [MenuItem("Hanger 51/World/Current/104 - Fix Regional Roads And Build Power Station")]
        public static void Build()
        {
            Hanger51WorldRoadsideRefinement.Build();
            GameObject world=Find(WorldName),airport=Find(AirportName);Terrain terrain=FindTerrain();
            if(!world||!airport||!terrain){Debug.LogError("Step 104 could not find the countryside, airport, or editable terrain.");return;}
            Transform settlements=FindChild(world.transform,"Settlements"),roads=DirectChild(world.transform,"Road Network");
            GameObject lived=Find(LivedName),refine=Find(RefineName);
            if(!settlements||!roads||!lived||!refine){Debug.LogError("Step 104 could not find the Step 102 world pieces.",world);return;}

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Preparing terrain-safe regional network",.03f);
                GameObject old=Find(PassName);if(old)UnityEngine.Object.DestroyImmediate(old);
                ResetFolder();meshId=0;Transform pass=New(PassName,lived.transform);Mats m=LoadMats();
                Bounds land=TerrainBounds(terrain);Bounds airportBounds=AirportBounds(airport);Vector3 ac=airportBounds.center;ac.y=Ground(terrain,ac);
                float airportSafe=Mathf.Max(1700f,Mathf.Sqrt(airportBounds.extents.x*airportBounds.extents.x+airportBounds.extents.z*airportBounds.extents.z)+700f);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Removing roads that leave the landmass",.12f);
                RemoveOldRegionalRoads(roads);RemoveOldRoadFollowingInfrastructure(refine.transform);
                List<Town> towns=CollectTowns(settlements,roads);
                if(towns.Count<4){Debug.LogError("Step 104 needs the four generated towns after Step 102.",world);return;}

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Building regional roads entirely on terrain",.25f);
                Transform regionalRoot=New("Regional Road Network",roads);List<RegionalRoad> regional=BuildRegionalNetwork(terrain,land,regionalRoot,towns,ac,airportSafe,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Building industrial power station district",.42f);
                Vector3 plantSite=ChoosePowerPlantSite(terrain,land,towns,ac,airportSafe);Transform plant=BuildPowerPlant(terrain,pass,plantSite,towns[0].center,m);
                RegionalRoad plantRoad=BuildPowerPlantRoad(terrain,land,regionalRoot,towns[0],plant,ac,airportSafe,m);if(plantRoad!=null)regional.Add(plantRoad);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Rebuilding roadside utilities on corrected roads",.58f);
                Transform infrastructure=New("Regional Utilities",pass);int poles=BuildRegionalUtilities(terrain,infrastructure,regional,m);int transmission=BuildPlantTransmission(terrain,infrastructure,plant,regional,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Adding roadside foliage and field boundaries",.72f);
                Transform nature=New("Regional Nature Detail",pass);int grass=AddRegionalGrass(terrain,land,nature,regional,towns,plant.position);int trees=AddShelterBelts(terrain,land,nature,regional,towns,plant.position);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Adding culverts, markers and intersection detail",.84f);
                Transform roadDetail=New("Regional Road Details",pass);int details=AddRoadDetails(terrain,roadDetail,regional,m);AddTownGatewayDetail(terrain,roadDetail,towns,regional,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Final terrain and text cleanup",.94f);
                ConformAllRoadSurfaces(terrain,roads);int fixedText=FixText(world.transform);

                terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();Selection.activeGameObject=pass.gameObject;
                Debug.Log($"Step 104 complete. terrain-safe regional roads={regional.Count}, power station built, regional utility poles={poles}, transmission structures={transmission}, added asset grass={grass}, shelter-belt trees={trees}, roadside details={details}, depth-fixed text={fixedText}. No regional road is intentionally allowed outside the editable terrain.",pass.gameObject);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        [MenuItem("Hanger 51/World/Current/105 - Validate Regional Roads And Power Station")]
        public static void Validate()
        {
            GameObject world=Find(WorldName),pass=Find(PassName);Terrain t=FindTerrain();if(!world||!pass||!t){Debug.LogError("Step 105 failed: run Step 104 first.");return;}
            Transform roads=DirectChild(world.transform,"Road Network");Bounds land=TerrainBounds(t);int offLand=0,buried=0,verts=0;
            if(roads)foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))if(mf&&mf.sharedMesh&&(mf.gameObject.name=="Road Surface"||mf.gameObject.name=="Industrial Access Surface"))foreach(Vector3 v in mf.sharedMesh.vertices){Vector3 w=mf.transform.TransformPoint(v);verts++;if(!InsideXZ(land,w,1f))offLand++;if(w.y<Ground(t,w)+.025f)buried++;}
            int regionals=Count(pass.transform,"Regional Road Marker"),plant=Count(pass.transform,"Power Station Complex"),cooling=Count(pass.transform,"Cooling Tower"),transformers=Count(pass.transform,"Transformer Bank"),poles=Count(pass.transform,"Regional Utility Pole"),transmission=Count(pass.transform,"Transmission Structure"),grass=Count(pass.transform,"Regional Asset Grass"),trees=Count(pass.transform,"Shelter Belt Tree"),details=Count(pass.transform,"Regional Road Detail");
            Transform settlements=FindChild(world.transform,"Settlements");int connected=0;if(settlements&&roads)for(int i=0;i<settlements.childCount;i++){Vector3 c=TownCenter(settlements.GetChild(i));if(DistanceToNamedRoads(c,roads,"Regional")<420f)connected++;}
            bool ok=offLand==0&&buried==0&&regionals>=4&&plant>=1&&cooling>=2&&transformers>=4&&poles>=30&&transmission>=6&&grass>=800&&trees>=70&&details>=60&&connected>=4;
            if(ok)Debug.Log($"Step 105 passed. road vertices={verts}, off-land={offLand}, buried={buried}, regional links={regionals}, towns connected={connected}/4, cooling towers={cooling}, transformer banks={transformers}, utility poles={poles}, transmission structures={transmission}, asset grass={grass}, shelter-belt trees={trees}, road details={details}.",pass);
            else Debug.LogError($"Step 105 failed. road vertices={verts}, off-land={offLand}, buried={buried}, regional links={regionals}, towns connected={connected}/4, plant={plant}, cooling towers={cooling}, transformers={transformers}, utility poles={poles}, transmission={transmission}, asset grass={grass}, shelter trees={trees}, road details={details}.",pass);
        }

        static Mats LoadMats()
        {
            Mats m=new Mats();m.asphalt=LoadBase("Matte_Asphalt");m.gravel=LoadBase("Matte_Gravel");m.line=LoadBase("Road_Paint");m.concrete=LoadBase("Concrete");m.wood=LoadBase("Weathered_Wood");m.metal=LoadBase("Dark_Metal");m.white=LoadBase("Warm_White");m.red=LoadBase("Barn_Red");m.green=LoadBase("Farm_Green");m.blue=LoadBase("Civic_Blue");m.glass=LoadBase("Dark_Glass");m.rubber=LoadBase("Rubber");
            m.yellow=Mat("Industrial Safety Yellow",new Color(.78f,.61f,.08f),0);m.industrial=Mat("Industrial Concrete",new Color(.35f,.36f,.35f),0);m.pipe=Mat("Pipe Steel",new Color(.26f,.29f,.30f),.05f);m.cooling=Mat("Cooling Concrete",new Color(.58f,.58f,.55f),0);return m;
        }
        static Material LoadBase(string n){Material m=AssetDatabase.LoadAssetAtPath<Material>(BaseGen+"/Materials/"+n+".mat");if(!m)Debug.LogWarning("Step 104 could not load Step 100 material "+n);return m;}
        static Material Mat(string n,Color c,float smooth){Shader s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new Material(s){name="H51_"+n,color=c};if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);if(m.HasProperty("_SpecColor"))m.SetColor("_SpecColor",Color.black);m.enableInstancing=true;AssetDatabase.CreateAsset(m,Gen+"/Materials/"+Safe(n)+".mat");return m;}

        static void RemoveOldRegionalRoads(Transform roads)
        {
            List<GameObject> kill=new List<GameObject>();for(int i=0;i<roads.childCount;i++){Transform r=roads.GetChild(i);if(r.name.StartsWith("County Road")||r.name.StartsWith("Airport Access Road")||r.name=="Town Road Connections"||r.name=="Regional Road Network")kill.Add(r.gameObject);}foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);
        }
        static void RemoveOldRoadFollowingInfrastructure(Transform refine)
        {
            List<GameObject> kill=new List<GameObject>();foreach(Transform tr in refine.GetComponentsInChildren<Transform>(true))if(tr.name=="Road Following Utilities"||tr.name=="Roadside Details")kill.Add(tr.gameObject);foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);
        }

        static List<Town> CollectTowns(Transform settlements,Transform roads)
        {
            List<Town> a=new List<Town>();for(int i=0;i<settlements.childCount;i++){Transform tr=settlements.GetChild(i);Town q=new Town{root=tr,name=tr.name,center=TownCenter(tr)};for(int r=0;r<roads.childCount;r++)if(roads.GetChild(r).name.StartsWith(tr.name))q.roads.Add(roads.GetChild(r));if(q.roads.Count>0)a.Add(q);}return a;
        }

        static List<RegionalRoad> BuildRegionalNetwork(Terrain t,Bounds land,Transform root,List<Town> towns,Vector3 ac,float airportSafe,Mats m)
        {
            List<RegionalRoad> outRoads=new List<RegionalRoad>();int[,] links={{0,1},{1,3},{3,2},{2,0}};
            for(int i=0;i<4;i++){Town a=towns[links[i,0]],b=towns[links[i,1]];Vector3 start=TownRoadEndpointToward(a,b.center),end=TownRoadEndpointToward(b,a.center);List<Vector3> path=SafeRoute(t,land,start,end,ac,airportSafe,95f);RegionalRoad r=CreateRegionalRoad(t,root,$"Regional County Route {i+1} - {a.name} to {b.name}",path,7.2f,m);if(r!=null)outRoads.Add(r);}
            return outRoads;
        }

        static RegionalRoad CreateRegionalRoad(Terrain t,Transform root,string name,List<Vector3> path,float width,Mats m)
        {
            if(path==null||path.Count<2)return null;Transform rr=New(name,root);New("Regional Road Marker",rr);MakeRibbon(t,rr,"Gravel Shoulder",path,width+7.5f,m.gravel,.035f,false);MakeRibbon(t,rr,"Road Surface",path,width,m.asphalt,.11f,true);MakeRibbon(t,rr,"Center Line",path,.18f,m.line,.145f,false);RegionalRoad r=new RegionalRoad{name=name,width=width,path=path};return r;
        }

        static List<Vector3> SafeRoute(Terrain t,Bounds land,Vector3 a,Vector3 b,Vector3 ac,float safe,float margin)
        {
            a=ClampLand(t,land,a,margin);b=ClampLand(t,land,b,margin);List<Vector3> control=new List<Vector3>{a};float clear=SegDist(ac,a,b);
            if(clear<safe+140f)
            {
                Vector3 da=a-ac,db=b-ac;da.y=db.y=0;if(da.sqrMagnitude<1)da=Vector3.right;if(db.sqrMagnitude<1)db=Vector3.forward;float aa=Mathf.Atan2(da.z,da.x)*Mathf.Rad2Deg,bb=Mathf.Atan2(db.z,db.x)*Mathf.Rad2Deg;float radius=safe+330f;
                List<Vector3> cw=ArcRoute(t,land,ac,aa,bb,-1,radius,margin);List<Vector3> ccw=ArcRoute(t,land,ac,aa,bb,1,radius,margin);List<Vector3> arc=ChooseArc(cw,ccw,land,margin);
                if(arc.Count>0)control.AddRange(arc);else{Vector3 mid=(a+b)*.5f,dir=mid-ac;dir.y=0;if(dir.sqrMagnitude<1)dir=Vector3.Cross(Vector3.up,(b-a).normalized);Vector3 via=ClampLand(t,land,ac+dir.normalized*(safe+450f),margin);control.Add(via);}
            }
            else
            {
                Vector3 d=b-a;d.y=0;Vector3 side=Vector3.Cross(Vector3.up,d.normalized);Vector3 mid=(a+b)*.5f+side*Mathf.Clamp(Planar(a,b)*.04f,0,85f)*Mathf.Sign(Mathf.Sin((a.x+b.z)*.0019f));control.Add(ClampLand(t,land,mid,margin));
            }
            control.Add(b);return SmoothPolyline(t,land,control,8f,margin);
        }
        static List<Vector3> ArcRoute(Terrain t,Bounds land,Vector3 c,float aa,float bb,int direction,float radius,float margin)
        {
            float delta=direction>0?Mathf.Repeat(bb-aa,360f):-Mathf.Repeat(aa-bb,360f);int n=Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta)/12f),3,32);List<Vector3>a=new List<Vector3>();for(int i=0;i<=n;i++){float ang=(aa+delta*i/(float)n)*Mathf.Deg2Rad;Vector3 p=c+new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang))*radius;if(!InsideXZ(land,p,margin))return new List<Vector3>();p.y=Ground(t,p)+.04f;a.Add(p);}return a;
        }
        static List<Vector3> ChooseArc(List<Vector3>a,List<Vector3>b,Bounds land,float margin){if(a.Count==0)return b;if(b.Count==0)return a;return PathLength(a)<=PathLength(b)?a:b;}
        static List<Vector3> SmoothPolyline(Terrain t,Bounds land,List<Vector3> c,float step,float margin)
        {
            List<Vector3> p=new List<Vector3>();for(int s=0;s<c.Count-1;s++){Vector3 a=c[s],b=c[s+1];float d=Planar(a,b);int n=Mathf.Max(2,Mathf.CeilToInt(d/step));for(int i=0;i<n;i++){if(s>0&&i==0)continue;float u=i/(float)n;Vector3 q=Vector3.Lerp(a,b,u);q=ClampLand(t,land,q,margin);q.y=Ground(t,q)+.04f;p.Add(q);}}Vector3 last=ClampLand(t,land,c[c.Count-1],margin);last.y=Ground(t,last)+.04f;p.Add(last);return p;
        }

        static Vector3 ChoosePowerPlantSite(Terrain t,Bounds land,List<Town> towns,Vector3 ac,float safe)
        {
            Town main=towns[0];Vector3 best=main.center;float bestScore=float.MinValue;for(int i=0;i<24;i++){float ang=i/24f*Mathf.PI*2f;float rad=720f+(i%3)*150f;Vector3 p=main.center+new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang))*rad;if(!InsideXZ(land,p,320f))continue;float airport=Planar(p,ac);if(airport<safe+650f)continue;float townSep=float.MaxValue;for(int k=1;k<towns.Count;k++)townSep=Mathf.Min(townSep,Planar(p,towns[k].center));if(townSep<500f)continue;float edge=EdgeMargin(land,p);float score=edge+airport*.15f-townSep*.03f;if(score>bestScore){bestScore=score;best=p;}}
            best=ClampLand(t,land,best,320f);best.y=Ground(t,best);return best;
        }

        static Transform BuildPowerPlant(Terrain t,Transform parent,Vector3 site,Vector3 town,Mats m)
        {
            Transform root=New("Power Station Complex",parent);root.position=site;Vector3 toward=town-site;toward.y=0;if(toward.sqrMagnitude<1)toward=Vector3.forward;root.rotation=Quaternion.LookRotation(toward.normalized,Vector3.up);
            Box(root,"Industrial Site Pad",new Vector3(0,.03f,0),new Vector3(210,.06f,170),m.industrial,false);BuildFence(root,m);
            Transform gate=New("Security Gate",root);gate.localPosition=new Vector3(0,0,83);Box(gate,"Gate House",new Vector3(-8,2.1f,-5),new Vector3(8,4.2f,7),m.white,true);Box(gate,"Gate Roof",new Vector3(-8,4.4f,-5),new Vector3(8.6f,.35f,7.6f),m.metal,false);Box(gate,"Barrier Left",new Vector3(-1,1.15f,0),new Vector3(8,.16f,.16f),m.yellow,false);Box(gate,"Barrier Right",new Vector3(7,1.15f,0),new Vector3(8,.16f,.16f),m.yellow,false);WorldLabel(gate,"POWER STATION",new Vector3(-8,3.0f,-8.55f),.24f);

            Transform turbine=New("Turbine Hall",root);turbine.localPosition=new Vector3(-38,0,2);Box(turbine,"Turbine Hall Foundation",new Vector3(0,.3f,0),new Vector3(62,.6f,46),m.concrete,true);Box(turbine,"Turbine Hall Building",new Vector3(0,11,0),new Vector3(60,21,44),m.white,true);Box(turbine,"Turbine Hall Roof",new Vector3(0,22,0),new Vector3(62,1.1f,46),m.metal,false);for(int x=-2;x<=2;x++)Box(turbine,"High Window",new Vector3(x*10,14,22.05f),new Vector3(5,3,.10f),m.glass,false);for(int x=-1;x<=1;x++)Box(turbine,"Service Door",new Vector3(x*16,4,22.1f),new Vector3(7,7,.12f),m.metal,false);WorldLabel(turbine,"TURBINE HALL",new Vector3(0,18.2f,22.15f),.27f);

            Transform boiler=New("Generation Hall",root);boiler.localPosition=new Vector3(25,0,-8);Box(boiler,"Generation Foundation",new Vector3(0,.3f,0),new Vector3(42,.6f,52),m.concrete,true);Box(boiler,"Generation Building",new Vector3(0,15,0),new Vector3(40,29,50),m.green,true);Box(boiler,"Generation Roof",new Vector3(0,30,0),new Vector3(42,1,52),m.metal,false);for(int y=0;y<4;y++)for(int x=-1;x<=1;x++)Box(boiler,"Vent",new Vector3(x*14,8+y*5,25.1f),new Vector3(6,2,.12f),m.metal,false);

            Mesh cooling=CoolingTowerMesh();for(int i=0;i<2;i++){Transform c=New("Cooling Tower "+(i+1),root);c.localPosition=new Vector3(55+i*42,0,-48+i*4);c.localScale=Vector3.one*(i==0?1f:.92f);GameObject g=new GameObject("Cooling Tower Shell");g.transform.SetParent(c,false);g.AddComponent<MeshFilter>().sharedMesh=cooling;g.AddComponent<MeshRenderer>().sharedMaterial=m.cooling;g.AddComponent<MeshCollider>().sharedMesh=cooling;for(int k=0;k<12;k++){float a=k/12f*Mathf.PI*2;Box(c,"Cooling Tower Base Pier",new Vector3(Mathf.Cos(a)*11,1.2f,Mathf.Sin(a)*11),new Vector3(1.2f,2.4f,1.2f),m.concrete,false);}}

            Transform stack=New("Exhaust Stack",root);stack.localPosition=new Vector3(7,0,-55);Cylinder(stack,"Stack Shaft",new Vector3(0,31,0),new Vector3(4.2f,31,4.2f),m.red,true);Cylinder(stack,"Stack Band 1",new Vector3(0,18,0),new Vector3(4.35f,.7f,4.35f),m.white,false);Cylinder(stack,"Stack Band 2",new Vector3(0,37,0),new Vector3(4.35f,.7f,4.35f),m.white,false);Cylinder(stack,"Stack Rim",new Vector3(0,62.2f,0),new Vector3(4.5f,.6f,4.5f),m.metal,false);

            Transform tanks=New("Tank Farm",root);tanks.localPosition=new Vector3(-72,0,-48);for(int i=0;i<3;i++){Transform tank=New("Storage Tank "+(i+1),tanks);tank.localPosition=new Vector3(i*18,0,0);Cylinder(tank,"Tank",new Vector3(0,4,0),new Vector3(7,4,7),m.metal,true);Cylinder(tank,"Tank Roof",new Vector3(0,8.2f,0),new Vector3(7.2f,.35f,7.2f),m.white,false);Box(tank,"Tank Ladder",new Vector3(7.15f,4,0),new Vector3(.2f,8,.8f),m.yellow,false);}

            BuildPipeRack(root,m);BuildSubstation(root,m);BuildPlantParking(root,m);BuildPlantLights(root,m);return root;
        }

        static void BuildFence(Transform root,Mats m)
        {
            float hw=105,hl=85;for(int side=0;side<4;side++){bool horizontal=side<2;float fixedV=side%2==0?-1:1;int n=horizontal?22:18;for(int i=0;i<=n;i++){float u=i/(float)n*2-1;Vector3 p=horizontal?new Vector3(u*hw,1.1f,fixedV*hl):new Vector3(fixedV*hw,1.1f,u*hl);if(horizontal&&fixedV>0&&Mathf.Abs(p.x)<13)continue;Box(root,"Security Fence Post",p,new Vector3(.14f,2.2f,.14f),m.metal,false);}Vector3 c=horizontal?new Vector3(0,1.0f,fixedV*hl):new Vector3(fixedV*hw,1.0f,0);Vector3 s=horizontal?new Vector3(hw*2,1.6f,.07f):new Vector3(.07f,1.6f,hl*2);if(!(horizontal&&fixedV>0))Box(root,"Security Fence Rail",c,s,m.metal,false);}
        }
        static void BuildPipeRack(Transform root,Mats m)
        {
            Transform r=New("Pipe Rack",root);r.localPosition=new Vector3(-12,0,-29);for(int i=0;i<8;i++){float z=-20+i*6;Box(r,"Pipe Rack Upright L",new Vector3(-8,3,z),new Vector3(.35f,6,.35f),m.metal,false);Box(r,"Pipe Rack Upright R",new Vector3(8,3,z),new Vector3(.35f,6,.35f),m.metal,false);Box(r,"Pipe Rack Beam",new Vector3(0,5.8f,z),new Vector3(16,.3f,.3f),m.metal,false);}for(int p=0;p<3;p++)Pipe(r,new Vector3(-7+p*7,6.2f,-22),new Vector3(-7+p*7,6.2f,24),m.pipe,"Process Pipe");
        }
        static void BuildSubstation(Transform root,Mats m)
        {
            Transform s=New("Electrical Substation",root);s.localPosition=new Vector3(72,0,45);Box(s,"Substation Gravel",new Vector3(0,.03f,0),new Vector3(56,.06f,42),m.gravel,false);for(int i=0;i<4;i++){Transform tr=New("Transformer Bank "+(i+1),s);tr.localPosition=new Vector3(-18+i*12,0,-5);Box(tr,"Transformer Body",new Vector3(0,2.2f,0),new Vector3(7,4.4f,5),m.green,true);for(int b=-1;b<=1;b++)Cylinder(tr,"Transformer Bushing",new Vector3(b*2,5.2f,0),new Vector3(.24f,1.25f,.24f),m.white,false);Box(tr,"Cooling Fins",new Vector3(0,2.2f,2.7f),new Vector3(6.4f,3.4f,.35f),m.metal,false);}for(int row=0;row<2;row++)for(int i=0;i<5;i++){Transform g=New("Substation Gantry",s);g.localPosition=new Vector3(-22+i*11,0,-16+row*31);Box(g,"Gantry L",new Vector3(-2.3f,4,0),new Vector3(.25f,8,.25f),m.metal,false);Box(g,"Gantry R",new Vector3(2.3f,4,0),new Vector3(.25f,8,.25f),m.metal,false);Box(g,"Gantry Top",new Vector3(0,7.8f,0),new Vector3(5,.25f,.25f),m.metal,false);for(int q=-1;q<=1;q++)Cylinder(g,"Insulator",new Vector3(q*1.6f,7.2f,0),new Vector3(.11f,.65f,.11f),m.white,false);}}
        }
        static void BuildPlantParking(Transform root,Mats m)
        {
            Transform p=New("Employee Parking",root);p.localPosition=new Vector3(-52,0,58);Box(p,"Parking Surface",new Vector3(0,.04f,0),new Vector3(68,.08f,30),m.asphalt,false);for(int x=-2;x<=2;x++)for(int z=-1;z<=1;z+=2){Box(p,"Parking Stripe",new Vector3(x*11,.09f,z*6),new Vector3(.15f,.03f,10),m.white,false);if((x+z)%2==0)Car(p,"Power Station Parked Car",new Vector3(x*11,.1f,z*6),Quaternion.Euler(0,z>0?180:0,0),x+z+10,m);}}
        static void BuildPlantLights(Transform root,Mats m){for(int i=0;i<12;i++){float a=i/12f*Mathf.PI*2;Vector3 p=new Vector3(Mathf.Cos(a)*88,0,Mathf.Sin(a)*68);Transform l=New("Plant Light Pole",root);l.localPosition=p;Box(l,"Light Pole",new Vector3(0,5,0),new Vector3(.18f,10,.18f),m.metal,false);Box(l,"Light Head",new Vector3(0,9.7f,.45f),new Vector3(1.0f,.35f,.8f),m.white,false);}}

        static RegionalRoad BuildPowerPlantRoad(Terrain t,Bounds land,Transform roadRoot,Town main,Transform plant,Vector3 ac,float safe,Mats m)
        {
            Vector3 start=TownRoadEndpointToward(main,plant.position),end=plant.position+plant.forward*92f;end=ClampLand(t,land,end,100f);List<Vector3> path=SafeRoute(t,land,start,end,ac,safe,95f);RegionalRoad r=CreateRegionalRoad(t,roadRoot,"Regional Industrial Access - Power Station",path,8f,m);if(r!=null&&r.path.Count>0){Transform marker=New("Industrial Access Gate Connection",plant);marker.position=r.path[r.path.Count-1];}return r;
        }

        static int BuildRegionalUtilities(Terrain t,Transform root,List<RegionalRoad> roads,Mats m)
        {
            int made=0;for(int ri=0;ri<roads.Count;ri++){RegionalRoad r=roads[ri];List<Vector3> p=Resample(r.path,64f);Vector3[] last=new Vector3[3];bool have=false;int sign=ri%2==0?1:-1;for(int i=0;i<p.Count;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;Vector3 q=p[i]+side*sign*(r.width*.5f+8.5f);q.y=Ground(t,q);Transform pole=New("Regional Utility Pole "+(++made).ToString("000"),root);pole.position=q;pole.rotation=Quaternion.LookRotation(tan,Vector3.up);Cylinder(pole,"Pole",new Vector3(0,5.2f,0),new Vector3(.18f,5.2f,.18f),m.wood,false);Box(pole,"Crossarm",new Vector3(0,9.55f,0),new Vector3(2.8f,.18f,.18f),m.wood,false);for(int k=-1;k<=1;k++){Cylinder(pole,"Insulator",new Vector3(k*.9f,9.82f,0),new Vector3(.08f,.24f,.08f),m.white,false);Vector3 now=pole.TransformPoint(new Vector3(k*.9f,10.1f,0));if(have)Wire(root,last[k+1],now,m.metal,"Regional Distribution Wire");last[k+1]=now;}have=true;}}return made;
        }
        static int BuildPlantTransmission(Terrain t,Transform root,Transform plant,List<RegionalRoad> roads,Mats m)
        {
            if(roads.Count==0)return 0;RegionalRoad nearest=roads[0];float bd=float.MaxValue;Vector3 target=plant.position;foreach(RegionalRoad r in roads)foreach(Vector3 p in r.path){float d=Planar(p,plant.position);if(d<bd){bd=d;nearest=r;target=p;}}List<Vector3> path=new List<Vector3>{plant.position+plant.right*72f,target};path=Resample(path,95f);int made=0;Vector3[] last=new Vector3[3];bool have=false;for(int i=0;i<path.Count;i++){Vector3 p=path[i];p.y=Ground(t,p);Transform tower=New("Transmission Structure "+(++made).ToString("00"),root);tower.position=p;Vector3 tan=Tangent(path,i);tower.rotation=Quaternion.LookRotation(tan,Vector3.up);Box(tower,"Tower Mast",new Vector3(0,9,0),new Vector3(.7f,18,.7f),m.metal,false);Box(tower,"Upper Crossarm",new Vector3(0,15,0),new Vector3(8,.35f,.35f),m.metal,false);Box(tower,"Lower Crossarm",new Vector3(0,11.5f,0),new Vector3(11,.35f,.35f),m.metal,false);for(int k=-1;k<=1;k++){Vector3 local=new Vector3(k*3.4f,k==0?15.3f:11.8f,0);Cylinder(tower,"Transmission Insulator",local,new Vector3(.13f,.55f,.13f),m.white,false);Vector3 now=tower.TransformPoint(local+Vector3.up*.8f);if(have)Wire(root,last[k+1],now,m.metal,"Transmission Conductor");last[k+1]=now;}have=true;}return made;
        }

        static int AddRegionalGrass(Terrain t,Bounds land,Transform root,List<RegionalRoad> roads,List<Town> towns,Vector3 plant)
        {
            GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(Grass1),b=AssetDatabase.LoadAssetAtPath<GameObject>(Grass2);if(!a&&!b)return 0;System.Random rng=new System.Random(Seed+1);int made=0;foreach(RegionalRoad r in roads){List<Vector3> p=Resample(r.path,10f);for(int i=0;i<p.Count&&made<1300;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){if(rng.NextDouble()<.28)continue;Vector3 q=p[i]+side*s*(r.width*.5f+Next(rng,4,12))+tan*Next(rng,-3,3);if(!InsideXZ(land,q,8f)||Planar(q,plant)<125)continue;q.y=Ground(t,q);SpawnPrefabGrass((rng.NextDouble()<.5?a:b)??a??b,root,q,rng,ref made);}}}
            for(int ti=0;ti<towns.Count&&made<1600;ti++)for(int k=0;k<120&&made<1600;k++){float ang=Next(rng,0,6.283f),rad=Next(rng,260,480);Vector3 q=towns[ti].center+new Vector3(Mathf.Cos(ang)*rad,0,Mathf.Sin(ang)*rad);if(!InsideXZ(land,q,15f)||DistanceToRegional(q,roads)<12)continue;q.y=Ground(t,q);SpawnPrefabGrass((rng.NextDouble()<.5?a:b)??a??b,root,q,rng,ref made);}return made;
        }
        static void SpawnPrefabGrass(GameObject src,Transform root,Vector3 p,System.Random rng,ref int made){if(!src)return;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)return;g.name="Regional Asset Grass "+(++made).ToString("0000");g.transform.SetParent(root,false);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.72f,1.48f);foreach(Collider c in g.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);}

        static int AddShelterBelts(Terrain t,Bounds land,Transform root,List<RegionalRoad> roads,List<Town> towns,Vector3 plant)
        {
            GameObject leaf=AssetDatabase.LoadAssetAtPath<GameObject>(Leaf),fir=AssetDatabase.LoadAssetAtPath<GameObject>(Fir);if(!leaf&&!fir)return 0;System.Random rng=new System.Random(Seed+2);int made=0;foreach(Town town in towns){for(int side=-1;side<=1;side+=2){Vector3 dir=(town.center-TerrainCenter(t));dir.y=0;if(dir.sqrMagnitude<1)dir=Vector3.right;Vector3 tangent=Vector3.Cross(Vector3.up,dir.normalized);for(int i=-10;i<=10;i++){Vector3 q=town.center+dir.normalized*side*340f+tangent*i*23f+dir.normalized*Next(rng,-18,18);if(!InsideXZ(land,q,25f)||DistanceToRegional(q,roads)<18||Planar(q,plant)<150)continue;q.y=Ground(t,q);GameObject src=rng.NextDouble()<.78?leaf:fir;if(!src)src=leaf??fir;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)continue;g.name="Shelter Belt Tree "+(++made).ToString("000");g.transform.SetParent(root,false);g.transform.position=q;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.72f,1.15f);}}}return made;
        }

        static int AddRoadDetails(Terrain t,Transform root,List<RegionalRoad> roads,Mats m)
        {
            int made=0;for(int ri=0;ri<roads.Count;ri++){RegionalRoad r=roads[ri];List<Vector3> p=Resample(r.path,85f);for(int i=0;i<p.Count;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){Vector3 q=p[i]+side*s*(r.width*.5f+3.3f);q.y=Ground(t,q);Transform d=New("Regional Road Detail "+(++made).ToString("000"),root);d.position=q;d.rotation=Quaternion.LookRotation(tan,Vector3.up);Box(d,"Delineator",new Vector3(0,.65f,0),new Vector3(.14f,1.3f,.14f),m.white,false);Box(d,"Reflector",new Vector3(0,1.12f,-.08f),new Vector3(.22f,.22f,.04f),s<0?m.red:m.yellow,false);}}
                for(int i=2;i<p.Count-2;i+=6){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;Transform cul=New("Regional Road Detail Culvert "+(++made).ToString("000"),root);cul.position=p[i]+side*(r.width*.5f+6.5f);cul.rotation=Quaternion.LookRotation(side,Vector3.up);Cylinder(cul,"Culvert Pipe",new Vector3(0,.45f,0),new Vector3(.65f,3.2f,.65f),m.metal,false);Box(cul,"Gravel Headwall",new Vector3(0,.25f,-3.1f),new Vector3(3,.5f,.4f),m.gravel,false);}
            }return made;
        }
        static void AddTownGatewayDetail(Terrain t,Transform root,List<Town> towns,List<RegionalRoad> roads,Mats m)
        {
            foreach(Town q in towns){RegionalRoad best=null;Vector3 gate=q.center;float bd=float.MaxValue;foreach(RegionalRoad r in roads)foreach(Vector3 p in r.path){float d=Planar(p,q.center);if(d<bd){bd=d;best=r;gate=p;}}if(best==null)continue;Vector3 tan=Tangent(best.path,NearestIndex(best.path,gate)),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){Transform g=New("Town Gateway - "+q.name+" "+s,root);g.position=gate+side*s*(best.width*.5f+8);g.position=new Vector3(g.position.x,Ground(t,g.position),g.position.z);Box(g,"Stone Gateway Base",new Vector3(0,.65f,0),new Vector3(1.2f,1.3f,1.2f),m.concrete,false);Box(g,"Welcome Post",new Vector3(0,2.4f,0),new Vector3(.35f,3.5f,.35f),m.wood,false);}}
        }

        static void ConformAllRoadSurfaces(Terrain t,Transform roads)
        {
            foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))if(mf&&mf.sharedMesh&&(mf.gameObject.name=="Road Surface"||mf.gameObject.name=="Gravel Shoulder"||mf.gameObject.name=="Center Line")){Mesh mesh=mf.sharedMesh;Vector3[] v=mesh.vertices;float off=mf.gameObject.name=="Center Line"?.15f:mf.gameObject.name=="Road Surface"?.11f:.035f;for(int i=0;i<v.Length;i++){Vector3 w=mf.transform.TransformPoint(v[i]);w.y=Ground(t,w)+off;v[i]=mf.transform.InverseTransformPoint(w);}mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);MeshCollider mc=mf.GetComponent<MeshCollider>();if(mc){mc.sharedMesh=null;mc.sharedMesh=mesh;}}
        }

        static int FixText(Transform world)
        {
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(TextShader);if(!shader)return 0;Dictionary<string,Material> mats=new Dictionary<string,Material>();int count=0;foreach(TextMesh tm in world.GetComponentsInChildren<TextMesh>(true)){Renderer r=tm.GetComponent<Renderer>();if(!r)continue;string key=tm.font?tm.font.name:"Default";Material mat;if(!mats.TryGetValue(key,out mat)){string path=Gen+"/Materials/DepthText_"+Safe(key)+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){Texture tex=tm.font&&tm.font.material?tm.font.material.mainTexture:null;mat=new Material(shader){name="H51 Regional Depth Text "+key};if(tex)mat.SetTexture("_MainTex",tex);mat.SetColor("_Color",Color.white);mat.SetFloat("_Cutoff",.1f);AssetDatabase.CreateAsset(mat,path);}mats[key]=mat;}r.sharedMaterial=mat;count++;}return count;
        }
        static void WorldLabel(Transform p,string text,Vector3 pos,float scale){GameObject g=new GameObject("Sign - "+text);g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.identity;TextMesh tm=g.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.characterSize=.5f;tm.fontSize=44;tm.color=Color.white;g.transform.localScale=Vector3.one*scale;}

        static Mesh CoolingTowerMesh()
        {
            string path=Gen+"/Meshes/CoolingTower.asset";Mesh e=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(e)return e;int seg=32,rings=10;List<Vector3> v=new List<Vector3>();List<int> tr=new List<int>();for(int y=0;y<rings;y++){float u=y/(float)(rings-1),h=u*42f;float radius=u<.55f?Mathf.Lerp(14f,8.8f,u/.55f):Mathf.Lerp(8.8f,11.3f,(u-.55f)/.45f);for(int i=0;i<seg;i++){float a=i/(float)seg*Mathf.PI*2;v.Add(new Vector3(Mathf.Cos(a)*radius,h,Mathf.Sin(a)*radius));}}for(int y=0;y<rings-1;y++)for(int i=0;i<seg;i++){int n=(i+1)%seg,a=y*seg+i,b=y*seg+n,c=(y+1)*seg+i,d=(y+1)*seg+n;tr.Add(a);tr.Add(c);tr.Add(b);tr.Add(b);tr.Add(c);tr.Add(d);}Mesh m=new Mesh{name="H51 Cooling Tower"};m.SetVertices(v);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,path);return m;
        }

        static void MakeRibbon(Terrain t,Transform root,string name,List<Vector3> path,float width,Material mat,float off,bool col){GameObject g=new GameObject(name);g.transform.SetParent(root,false);Mesh mesh=RibbonMesh(t,g.transform,path,width,off,name);g.AddComponent<MeshFilter>().sharedMesh=mesh;if(mat)g.AddComponent<MeshRenderer>().sharedMaterial=mat;else g.AddComponent<MeshRenderer>();if(col)g.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;}
        static Mesh RibbonMesh(Terrain t,Transform holder,List<Vector3> path,float width,float off,string name){int c=path.Count;Vector3[] v=new Vector3[c*2];Vector2[] uv=new Vector2[c*2];int[] tri=new int[Mathf.Max(0,(c-1)*6)];float dist=0;for(int i=0;i<c;i++){Vector3 tan=Tangent(path,i),side=Vector3.Cross(Vector3.up,tan).normalized*width*.5f;Vector3 l=path[i]-side,r=path[i]+side;l.y=Ground(t,l)+off;r.y=Ground(t,r)+off;if(i>0)dist+=Planar(path[i-1],path[i]);v[i*2]=holder.InverseTransformPoint(l);v[i*2+1]=holder.InverseTransformPoint(r);uv[i*2]=new Vector2(0,dist/7);uv[i*2+1]=new Vector2(1,dist/7);if(i<c-1){int q=i*6,j=i*2;tri[q]=j;tri[q+1]=j+2;tri[q+2]=j+1;tri[q+3]=j+1;tri[q+4]=j+2;tri[q+5]=j+3;}}Mesh m=new Mesh{name="H51_104_"+Safe(name)+"_"+(meshId++).ToString("0000")};m.vertices=v;m.uv=uv;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Gen+"/Meshes/"+m.name+".asset");return m;}

        static Vector3 TownRoadEndpointToward(Town town,Vector3 target){Vector3 best=town.center;float bd=float.MaxValue;foreach(Transform r in town.roads){List<Vector3> p=RoadPath(r);if(p.Count<2)continue;Vector3[] ends={p[0],p[p.Count-1]};foreach(Vector3 e in ends){float d=Planar(e,target);if(d<bd){bd=d;best=e;}}}return best;}
        static List<Vector3> RoadPath(Transform road){Transform s=FindChild(road,"Road Surface");if(!s)return new List<Vector3>();MeshFilter mf=s.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return new List<Vector3>();Vector3[] v=mf.sharedMesh.vertices;List<Vector3> p=new List<Vector3>();for(int i=0;i+1<v.Length;i+=2)p.Add((s.TransformPoint(v[i])+s.TransformPoint(v[i+1]))*.5f);return p;}
        static List<Vector3> Resample(List<Vector3> p,float step){List<Vector3> o=new List<Vector3>();if(p==null||p.Count==0)return o;o.Add(p[0]);for(int i=0;i<p.Count-1;i++){float d=Planar(p[i],p[i+1]);int n=Mathf.Max(1,Mathf.CeilToInt(d/step));for(int k=1;k<=n;k++)o.Add(Vector3.Lerp(p[i],p[i+1],k/(float)n));}return o;}
        static Vector3 Tangent(List<Vector3> p,int i){if(p.Count<2)return Vector3.forward;Vector3 d=i==0?p[1]-p[0]:i==p.Count-1?p[p.Count-1]-p[p.Count-2]:p[i+1]-p[i-1];d.y=0;return d.sqrMagnitude<.001f?Vector3.forward:d.normalized;}
        static int NearestIndex(List<Vector3> p,Vector3 q){int bi=0;float bd=float.MaxValue;for(int i=0;i<p.Count;i++){float d=Planar(p[i],q);if(d<bd){bd=d;bi=i;}}return bi;}
        static float DistanceToRegional(Vector3 p,List<RegionalRoad> roads){float b=float.MaxValue;foreach(RegionalRoad r in roads)for(int i=0;i<r.path.Count-1;i++)b=Mathf.Min(b,SegDist(p,r.path[i],r.path[i+1]));return b==float.MaxValue?99999:b;}
        static float DistanceToNamedRoads(Vector3 p,Transform roads,string key){float b=float.MaxValue;foreach(Transform r in roads.GetComponentsInChildren<Transform>(true))if(r.name.Contains(key)){List<Vector3> path=RoadPath(r);for(int i=0;i<path.Count-1;i++)b=Mathf.Min(b,SegDist(p,path[i],path[i+1]));}return b==float.MaxValue?99999:b;}

        static Bounds TerrainBounds(Terrain t){Vector3 o=t.transform.position,s=t.terrainData.size;return new Bounds(o+new Vector3(s.x*.5f,s.y*.5f,s.z*.5f),s);}
        static Vector3 TerrainCenter(Terrain t){Vector3 o=t.transform.position,s=t.terrainData.size;return new Vector3(o.x+s.x*.5f,Ground(t,o+new Vector3(s.x*.5f,0,s.z*.5f)),o.z+s.z*.5f);}
        static bool InsideXZ(Bounds b,Vector3 p,float margin){return p.x>=b.min.x+margin&&p.x<=b.max.x-margin&&p.z>=b.min.z+margin&&p.z<=b.max.z-margin;}
        static Vector3 ClampLand(Terrain t,Bounds b,Vector3 p,float margin){p.x=Mathf.Clamp(p.x,b.min.x+margin,b.max.x-margin);p.z=Mathf.Clamp(p.z,b.min.z+margin,b.max.z-margin);p.y=Ground(t,p);return p;}
        static float EdgeMargin(Bounds b,Vector3 p){return Mathf.Min(p.x-b.min.x,b.max.x-p.x,p.z-b.min.z,b.max.z-p.z);}
        static Bounds AirportBounds(GameObject airport){Bounds b=BoundsOf(airport);foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)){if(!t||!t.gameObject.scene.IsValid())continue;string n=t.name.ToLowerInvariant();if(!n.Contains("runway")&&!n.Contains("taxiway")&&!n.Contains("apron"))continue;foreach(Renderer r in t.GetComponentsInChildren<Renderer>(true))b.Encapsulate(r.bounds);foreach(Collider c in t.GetComponentsInChildren<Collider>(true))b.Encapsulate(c.bounds);}return b;}
        static Bounds BoundsOf(GameObject g){bool set=false;Bounds b=new Bounds(g.transform.position,Vector3.zero);foreach(Renderer r in g.GetComponentsInChildren<Renderer>(true)){if(!set){b=r.bounds;set=true;}else b.Encapsulate(r.bounds);}foreach(Collider c in g.GetComponentsInChildren<Collider>(true)){if(!set){b=c.bounds;set=true;}else b.Encapsulate(c.bounds);}return b;}
        static Vector3 TownCenter(Transform town){List<Transform> h=new List<Transform>();for(int i=0;i<town.childCount;i++)if(town.GetChild(i).name.StartsWith("Detailed House")||town.GetChild(i).name.StartsWith("Building"))h.Add(town.GetChild(i));if(h.Count==0)return town.position;Vector3 c=Vector3.zero;foreach(Transform x in h)c+=x.position;return c/h.Count;}

        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static GameObject Cylinder(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static void Pipe(Transform p,Vector3 a,Vector3 b,Material m,string n){Vector3 d=b-a;if(d.sqrMagnitude<.01f)return;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=(a+b)*.5f;g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized);g.transform.localScale=new Vector3(.24f,d.magnitude*.5f,.24f);if(m)g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
        static void Wire(Transform p,Vector3 a,Vector3 b,Material m,string n){Vector3 d=b-a;if(d.sqrMagnitude<.01f)return;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.position=(a+b)*.5f;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);g.transform.localScale=new Vector3(.022f,d.magnitude*.5f,.022f);if(m)g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
        static void Car(Transform parent,string name,Vector3 pos,Quaternion rot,int variant,Mats m){Transform c=New(name,parent);c.localPosition=pos;c.localRotation=rot;Material body=variant%3==0?m.red:variant%3==1?m.blue:m.green;Box(c,"Body",new Vector3(0,.65f,0),new Vector3(1.9f,.65f,4.2f),body,false);Box(c,"Cabin",new Vector3(0,1.15f,-.15f),new Vector3(1.65f,.65f,2),m.glass,false);for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){GameObject w=Cylinder(c,"Wheel",new Vector3(x, .42f,z*1.35f),new Vector3(.32f,.18f,.32f),m.rubber,false);w.transform.localRotation=Quaternion.Euler(0,0,90);}}

        static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
        static float Planar(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        static float SegDist(Vector3 p,Vector3 a,Vector3 b){Vector2 q=new Vector2(p.x,p.z),x=new Vector2(a.x,a.z),d=new Vector2(b.x-a.x,b.z-a.z);return d.sqrMagnitude<.001f?Vector2.Distance(q,x):Vector2.Distance(q,x+d*Mathf.Clamp01(Vector2.Dot(q-x,d)/d.sqrMagnitude));}
        static float PathLength(List<Vector3> p){float d=0;for(int i=0;i<p.Count-1;i++)d+=Planar(p[i],p[i+1]);return d;}
        static float Next(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
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
