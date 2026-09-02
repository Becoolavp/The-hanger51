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
        const string World="Hanger 51 Surrounding Countryside";
        const string Airport="Hanger 51 Airport Complex";
        const string TerrainName="Hanger 51 Editable Terrain";
        const string Lived="Hanger 51 Lived-In Countryside Detail";
        const string Refine="Hanger 51 Roadside Refinement";
        const string Pass="Hanger 51 Regional Infrastructure Pass";
        const string BaseGen="Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen="Assets/_Project/Environment/Generated/CountrysideRegionalInfrastructure";
        const string Grass1="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string Grass2="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string Leaf="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string Fir="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string TextShader="Assets/_Project/Shaders/Hanger51DepthWorldText.shader";
        const int Seed=51104;
        static int meshId;

        class Town { public Transform tr; public string name; public Vector3 c; public List<Transform> roads=new List<Transform>(); }
        class Road { public string name; public float width; public List<Vector3> p=new List<Vector3>(); }
        struct Mats { public Material asphalt,gravel,line,concrete,wood,metal,white,red,green,blue,glass,rubber,yellow,industrial,cooling; }

        [MenuItem("Hanger 51/World/Current/104 - Fix Regional Roads And Build Power Station")]
        public static void Build()
        {
            Hanger51WorldRoadsideRefinement.Build();
            GameObject world=Find(World),airport=Find(Airport),lived=Find(Lived),refine=Find(Refine);
            Terrain terrain=FindTerrain();
            if(!world||!airport||!lived||!refine||!terrain){Debug.LogError("Step 104 could not find the Step 102 countryside pieces.");return;}
            Transform settlements=FindChild(world.transform,"Settlements"),roadRoot=DirectChild(world.transform,"Road Network");
            if(!settlements||!roadRoot){Debug.LogError("Step 104 could not find settlements or the road network.",world);return;}

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Preparing terrain-safe regional network",.04f);
                GameObject old=Find(Pass);if(old)UnityEngine.Object.DestroyImmediate(old);
                ResetFolder();meshId=0;Mats m=LoadMats();Transform pass=New(Pass,lived.transform);
                Bounds land=TerrainBounds(terrain),airportBounds=BoundsOf(airport);Vector3 ac=airportBounds.center;ac.y=Ground(terrain,ac);
                float safe=Mathf.Max(1700f,Mathf.Sqrt(airportBounds.extents.x*airportBounds.extents.x+airportBounds.extents.z*airportBounds.extents.z)+700f);

                RemoveOldRegionalRoads(roadRoot);
                RemoveOldRoadFollowing(refine.transform);
                List<Town> towns=CollectTowns(settlements,roadRoot);
                if(towns.Count<4){Debug.LogError("Step 104 needs four towns after Step 102.",world);return;}

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Connecting towns without leaving terrain",.24f);
                Transform regionalRoot=New("Regional Road Network",roadRoot);
                List<Road> roads=BuildRegionalRoads(terrain,land,regionalRoot,towns,ac,safe,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Building detailed power station",.43f);
                Vector3 site=ChoosePlantSite(terrain,land,towns[0].c,ac,safe);
                Transform plant=BuildPlant(terrain,pass,site,towns[0].c,m);
                Road industrial=MakeRoad(terrain,land,regionalRoot,"Regional Industrial Access - Power Station",
                    TownEndpoint(towns[0],site),plant.position+plant.forward*92f,ac,safe,8f,m);
                if(industrial!=null)roads.Add(industrial);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Adding utilities and transmission",.60f);
                Transform utilities=New("Regional Utilities",pass);
                int poles=AddUtilities(terrain,utilities,roads,m);
                int transmission=AddTransmission(terrain,utilities,plant,towns[0].c,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Adding regional foliage",.75f);
                Transform nature=New("Regional Nature Detail",pass);
                int grass=AddGrass(terrain,land,nature,roads,towns,plant.position);
                int trees=AddTrees(terrain,land,nature,roads,towns,plant.position);

                EditorUtility.DisplayProgressBar("Hanger 51 Regional Pass","Adding roadside detail",.88f);
                Transform detailsRoot=New("Regional Road Details",pass);
                int details=AddRoadDetails(terrain,detailsRoot,roads,m);
                FixText(world.transform);

                terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();Selection.activeGameObject=pass.gameObject;
                Debug.Log($"Step 104 complete. regional roads={roads.Count}, poles={poles}, transmission={transmission}, grass={grass}, trees={trees}, road details={details}.",pass.gameObject);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        [MenuItem("Hanger 51/World/Current/105 - Validate Regional Roads And Power Station")]
        public static void Validate()
        {
            GameObject world=Find(World),pass=Find(Pass);Terrain t=FindTerrain();
            if(!world||!pass||!t){Debug.LogError("Step 105 failed: run Step 104 first.");return;}
            Transform roads=DirectChild(world.transform,"Road Network");Bounds land=TerrainBounds(t);int off=0,buried=0,verts=0;
            if(roads)foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))
                if(mf&&mf.sharedMesh&&mf.gameObject.name=="Road Surface")
                    foreach(Vector3 v in mf.sharedMesh.vertices){Vector3 w=mf.transform.TransformPoint(v);verts++;if(!Inside(land,w,1))off++;if(w.y<Ground(t,w)+.025f)buried++;}
            int markers=Count(roads,"Regional Road Marker"),cooling=Count(pass.transform,"Cooling Tower"),transformers=Count(pass.transform,"Transformer Bank");
            int poles=Count(pass.transform,"Regional Utility Pole"),transmission=Count(pass.transform,"Transmission Structure");
            int grass=Count(pass.transform,"Regional Asset Grass"),trees=Count(pass.transform,"Shelter Belt Tree"),details=Count(pass.transform,"Regional Road Detail");
            bool ok=off==0&&buried==0&&markers>=5&&cooling>=2&&transformers>=4&&poles>=30&&transmission>=6&&grass>=800&&trees>=70&&details>=60;
            if(ok)Debug.Log($"Step 105 passed. vertices={verts}, off-land={off}, buried={buried}, regional roads={markers}, cooling={cooling}, transformers={transformers}, poles={poles}, transmission={transmission}, grass={grass}, trees={trees}, details={details}.",pass);
            else Debug.LogError($"Step 105 failed. vertices={verts}, off-land={off}, buried={buried}, regional roads={markers}, cooling={cooling}, transformers={transformers}, poles={poles}, transmission={transmission}, grass={grass}, trees={trees}, details={details}.",pass);
        }

        static Mats LoadMats()
        {
            Mats m=new Mats();
            m.asphalt=Load("Matte_Asphalt");m.gravel=Load("Matte_Gravel");m.line=Load("Road_Paint");m.concrete=Load("Concrete");
            m.wood=Load("Weathered_Wood");m.metal=Load("Dark_Metal");m.white=Load("Warm_White");m.red=Load("Barn_Red");
            m.green=Load("Farm_Green");m.blue=Load("Civic_Blue");m.glass=Load("Dark_Glass");m.rubber=Load("Rubber");
            m.yellow=Mat("Industrial Safety Yellow",new Color(.78f,.61f,.08f));m.industrial=Mat("Industrial Concrete",new Color(.35f,.36f,.35f));
            m.cooling=Mat("Cooling Concrete",new Color(.58f,.58f,.55f));return m;
        }
        static Material Load(string n){Material m=AssetDatabase.LoadAssetAtPath<Material>(BaseGen+"/Materials/"+n+".mat");if(!m)Debug.LogWarning("Step 104 could not load material "+n);return m;}
        static Material Mat(string n,Color c){Shader s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new Material(s){name="H51_"+n,color=c};if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",0);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);m.enableInstancing=true;AssetDatabase.CreateAsset(m,Gen+"/Materials/"+Safe(n)+".mat");return m;}

        static void RemoveOldRegionalRoads(Transform root)
        {
            List<GameObject> kill=new List<GameObject>();for(int i=0;i<root.childCount;i++){Transform r=root.GetChild(i);if(r.name.StartsWith("County Road")||r.name.StartsWith("Airport Access Road")||r.name=="Town Road Connections"||r.name=="Regional Road Network")kill.Add(r.gameObject);}
            foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);
        }
        static void RemoveOldRoadFollowing(Transform refine)
        {
            List<GameObject> kill=new List<GameObject>();foreach(Transform tr in refine.GetComponentsInChildren<Transform>(true))if(tr.name=="Road Following Utilities"||tr.name=="Roadside Details")kill.Add(tr.gameObject);
            foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);
        }
        static List<Town> CollectTowns(Transform settlements,Transform roadRoot)
        {
            List<Town> list=new List<Town>();for(int i=0;i<settlements.childCount;i++){Transform tr=settlements.GetChild(i);Town q=new Town{tr=tr,name=tr.name,c=TownCenter(tr)};
                for(int r=0;r<roadRoot.childCount;r++)if(roadRoot.GetChild(r).name.StartsWith(tr.name))q.roads.Add(roadRoot.GetChild(r));if(q.roads.Count>0)list.Add(q);}
            return list;
        }

        static List<Road> BuildRegionalRoads(Terrain t,Bounds land,Transform root,List<Town> towns,Vector3 ac,float safe,Mats m)
        {
            List<Road> roads=new List<Road>();int[,] links={{0,1},{1,3},{3,2},{2,0}};
            for(int i=0;i<4;i++){Town a=towns[links[i,0]],b=towns[links[i,1]];Road r=MakeRoad(t,land,root,$"Regional County Route {i+1} - {a.name} to {b.name}",TownEndpoint(a,b.c),TownEndpoint(b,a.c),ac,safe,7.2f,m);if(r!=null)roads.Add(r);}
            return roads;
        }
        static Road MakeRoad(Terrain t,Bounds land,Transform root,string name,Vector3 a,Vector3 b,Vector3 ac,float safe,float width,Mats m)
        {
            a=Clamp(t,land,a,90);b=Clamp(t,land,b,90);List<Vector3> controls=new List<Vector3>{a};
            if(SegDist(ac,a,b)<safe+120){Vector3 best=Vector3.zero;float score=float.MaxValue;Vector3[] cand={ac+Vector3.right*(safe+400),ac-Vector3.right*(safe+400),ac+Vector3.forward*(safe+400),ac-Vector3.forward*(safe+400)};
                foreach(Vector3 raw in cand){Vector3 q=Clamp(t,land,raw,90);float clear=Mathf.Min(SegDist(ac,a,q),SegDist(ac,q,b));float s=Planar(a,q)+Planar(q,b);if(clear>safe-80&&s<score){score=s;best=q;}}if(score<float.MaxValue)controls.Add(best);}
            else{Vector3 d=b-a;d.y=0;Vector3 side=Vector3.Cross(Vector3.up,d.normalized);controls.Add(Clamp(t,land,(a+b)*.5f+side*Mathf.Clamp(Planar(a,b)*.035f,0,65),90));}
            controls.Add(b);List<Vector3> path=Densify(t,land,controls,8,90);if(path.Count<2)return null;
            Transform rr=New(name,root);New("Regional Road Marker",rr);Ribbon(t,rr,"Gravel Shoulder",path,width+7.5f,m.gravel,.04f,false);Ribbon(t,rr,"Road Surface",path,width,m.asphalt,.11f,true);Ribbon(t,rr,"Center Line",path,.18f,m.line,.15f,false);
            return new Road{name=name,width=width,p=path};
        }
        static List<Vector3> Densify(Terrain t,Bounds land,List<Vector3> c,float step,float margin)
        {
            List<Vector3> p=new List<Vector3>();for(int s=0;s<c.Count-1;s++){float d=Planar(c[s],c[s+1]);int n=Mathf.Max(2,Mathf.CeilToInt(d/step));for(int i=0;i<n;i++){if(s>0&&i==0)continue;Vector3 q=Clamp(t,land,Vector3.Lerp(c[s],c[s+1],i/(float)n),margin);q.y=Ground(t,q)+.04f;p.Add(q);}}
            Vector3 last=Clamp(t,land,c[c.Count-1],margin);last.y=Ground(t,last)+.04f;p.Add(last);return p;
        }

        static Vector3 ChoosePlantSite(Terrain t,Bounds land,Vector3 main,Vector3 ac,float safe)
        {
            Vector3 best=Clamp(t,land,main,320);float score=float.MinValue;for(int i=0;i<20;i++){float a=i/20f*Mathf.PI*2,rad=720+(i%4)*110;Vector3 p=main+new Vector3(Mathf.Cos(a)*rad,0,Mathf.Sin(a)*rad);if(!Inside(land,p,320)||Planar(p,ac)<safe+650)continue;float s=EdgeMargin(land,p)+Planar(p,ac)*.1f;if(s>score){score=s;best=p;}}
            best=Clamp(t,land,best,320);best.y=Ground(t,best);return best;
        }
        static Transform BuildPlant(Terrain t,Transform parent,Vector3 site,Vector3 town,Mats m)
        {
            Transform root=New("Power Station Complex",parent);root.position=site;Vector3 face=town-site;face.y=0;if(face.sqrMagnitude<1)face=Vector3.forward;root.rotation=Quaternion.LookRotation(face.normalized,Vector3.up);
            Box(root,"Industrial Site Pad",new Vector3(0,.03f,0),new Vector3(210,.06f,170),m.industrial,false);BuildFence(root,m);
            Transform gate=New("Security Gate",root);gate.localPosition=new Vector3(0,0,83);Box(gate,"Gate House",new Vector3(-8,2,-5),new Vector3(8,4,7),m.white,true);Box(gate,"Barrier",new Vector3(4,1.1f,0),new Vector3(16,.16f,.16f),m.yellow,false);Label(gate,"POWER STATION",new Vector3(-8,3,-8.6f),.24f);
            Transform hall=New("Turbine Hall",root);hall.localPosition=new Vector3(-38,0,2);Box(hall,"Turbine Hall Building",new Vector3(0,11,0),new Vector3(60,21,44),m.white,true);Box(hall,"Turbine Hall Roof",new Vector3(0,22,0),new Vector3(62,1.1f,46),m.metal,false);
            for(int x=-2;x<=2;x++)Box(hall,"High Window",new Vector3(x*10,14,22.05f),new Vector3(5,3,.1f),m.glass,false);Label(hall,"TURBINE HALL",new Vector3(0,18,22.15f),.27f);
            Transform gen=New("Generation Hall",root);gen.localPosition=new Vector3(25,0,-8);Box(gen,"Generation Building",new Vector3(0,15,0),new Vector3(40,29,50),m.green,true);Box(gen,"Generation Roof",new Vector3(0,30,0),new Vector3(42,1,52),m.metal,false);
            Mesh cool=CoolingMesh();for(int i=0;i<2;i++){Transform c=New("Cooling Tower "+(i+1),root);c.localPosition=new Vector3(55+i*42,0,-48+i*4);GameObject shell=new GameObject("Cooling Tower Shell");shell.transform.SetParent(c,false);shell.AddComponent<MeshFilter>().sharedMesh=cool;shell.AddComponent<MeshRenderer>().sharedMaterial=m.cooling;shell.AddComponent<MeshCollider>().sharedMesh=cool;}
            Transform stack=New("Exhaust Stack",root);stack.localPosition=new Vector3(7,0,-55);Cylinder(stack,"Stack",new Vector3(0,31,0),new Vector3(4.2f,31,4.2f),m.red,true);Cylinder(stack,"Stack Band 1",new Vector3(0,18,0),new Vector3(4.35f,.7f,4.35f),m.white,false);Cylinder(stack,"Stack Band 2",new Vector3(0,37,0),new Vector3(4.35f,.7f,4.35f),m.white,false);
            Transform tanks=New("Tank Farm",root);tanks.localPosition=new Vector3(-72,0,-48);for(int i=0;i<3;i++){Transform tank=New("Storage Tank "+(i+1),tanks);tank.localPosition=new Vector3(i*18,0,0);Cylinder(tank,"Tank",new Vector3(0,4,0),new Vector3(7,4,7),m.metal,true);Cylinder(tank,"Tank Roof",new Vector3(0,8.2f,0),new Vector3(7.2f,.35f,7.2f),m.white,false);}
            BuildSubstation(root,m);BuildParking(root,m);for(int i=0;i<12;i++){float a=i/12f*Mathf.PI*2;Transform l=New("Plant Light Pole",root);l.localPosition=new Vector3(Mathf.Cos(a)*88,0,Mathf.Sin(a)*68);Box(l,"Pole",new Vector3(0,5,0),new Vector3(.18f,10,.18f),m.metal,false);Box(l,"Light",new Vector3(0,9.7f,.45f),new Vector3(1,.35f,.8f),m.white,false);}
            return root;
        }
        static void BuildFence(Transform root,Mats m)
        {
            float hw=105,hl=85;for(int side=0;side<4;side++){bool h=side<2;float fixedV=side%2==0?-1:1;int n=h?22:18;for(int i=0;i<=n;i++){float u=i/(float)n*2-1;Vector3 p=h?new Vector3(u*hw,1.1f,fixedV*hl):new Vector3(fixedV*hw,1.1f,u*hl);if(h&&fixedV>0&&Mathf.Abs(p.x)<13)continue;Box(root,"Security Fence Post",p,new Vector3(.14f,2.2f,.14f),m.metal,false);}Vector3 c=h?new Vector3(0,1,fixedV*hl):new Vector3(fixedV*hw,1,0);Vector3 s=h?new Vector3(hw*2,1.6f,.07f):new Vector3(.07f,1.6f,hl*2);if(!(h&&fixedV>0))Box(root,"Security Fence Rail",c,s,m.metal,false);}
        }
        static void BuildSubstation(Transform root,Mats m)
        {
            Transform s=New("Electrical Substation",root);s.localPosition=new Vector3(72,0,45);Box(s,"Substation Gravel",new Vector3(0,.03f,0),new Vector3(56,.06f,42),m.gravel,false);
            for(int i=0;i<4;i++){Transform tr=New("Transformer Bank "+(i+1),s);tr.localPosition=new Vector3(-18+i*12,0,-5);Box(tr,"Transformer Body",new Vector3(0,2.2f,0),new Vector3(7,4.4f,5),m.green,true);for(int b=-1;b<=1;b++)Cylinder(tr,"Transformer Bushing",new Vector3(b*2,5.2f,0),new Vector3(.24f,1.25f,.24f),m.white,false);}
            for(int row=0;row<2;row++)for(int i=0;i<5;i++){Transform g=New("Substation Gantry",s);g.localPosition=new Vector3(-22+i*11,0,-16+row*31);Box(g,"Gantry L",new Vector3(-2.3f,4,0),new Vector3(.25f,8,.25f),m.metal,false);Box(g,"Gantry R",new Vector3(2.3f,4,0),new Vector3(.25f,8,.25f),m.metal,false);Box(g,"Gantry Top",new Vector3(0,7.8f,0),new Vector3(5,.25f,.25f),m.metal,false);}
        }
        static void BuildParking(Transform root,Mats m)
        {
            Transform p=New("Employee Parking",root);p.localPosition=new Vector3(-52,0,58);Box(p,"Parking Surface",new Vector3(0,.04f,0),new Vector3(68,.08f,30),m.asphalt,false);
            for(int x=-2;x<=2;x++)for(int z=-1;z<=1;z+=2){Box(p,"Parking Stripe",new Vector3(x*11,.09f,z*6),new Vector3(.15f,.03f,10),m.white,false);if((x+z)%2==0)Car(p,new Vector3(x*11,.1f,z*6),z>0?180:0,x+z,m);}
        }
        static Mesh CoolingMesh()
        {
            string path=Gen+"/Meshes/CoolingTower.asset";Mesh e=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(e)return e;int seg=32,rings=10;List<Vector3> v=new List<Vector3>();List<int> tr=new List<int>();
            for(int y=0;y<rings;y++){float u=y/(float)(rings-1),h=u*42,r=u<.55f?Mathf.Lerp(14,8.8f,u/.55f):Mathf.Lerp(8.8f,11.3f,(u-.55f)/.45f);for(int i=0;i<seg;i++){float a=i/(float)seg*Mathf.PI*2;v.Add(new Vector3(Mathf.Cos(a)*r,h,Mathf.Sin(a)*r));}}
            for(int y=0;y<rings-1;y++)for(int i=0;i<seg;i++){int n=(i+1)%seg,a=y*seg+i,b=y*seg+n,c=(y+1)*seg+i,d=(y+1)*seg+n;tr.Add(a);tr.Add(c);tr.Add(b);tr.Add(b);tr.Add(c);tr.Add(d);}
            Mesh m=new Mesh{name="H51 Cooling Tower"};m.SetVertices(v);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,path);return m;
        }

        static int AddUtilities(Terrain t,Transform root,List<Road> roads,Mats m)
        {
            int made=0;for(int ri=0;ri<roads.Count;ri++){List<Vector3> p=Resample(roads[ri].p,60);Vector3[] last=new Vector3[3];bool have=false;int sign=ri%2==0?1:-1;
                for(int i=0;i<p.Count;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;Vector3 q=p[i]+side*sign*(roads[ri].width*.5f+8.5f);q.y=Ground(t,q);Transform pole=New("Regional Utility Pole "+(++made).ToString("000"),root);pole.position=q;pole.rotation=Quaternion.LookRotation(tan,Vector3.up);Cylinder(pole,"Pole",new Vector3(0,5.2f,0),new Vector3(.18f,5.2f,.18f),m.wood,false);Box(pole,"Crossarm",new Vector3(0,9.55f,0),new Vector3(2.8f,.18f,.18f),m.wood,false);
                    for(int k=-1;k<=1;k++){Vector3 now=pole.TransformPoint(new Vector3(k*.9f,10.1f,0));if(have)Wire(root,last[k+1],now,m.metal,"Regional Distribution Wire");last[k+1]=now;}have=true;}}
            return made;
        }
        static int AddTransmission(Terrain t,Transform root,Transform plant,Vector3 town,Mats m)
        {
            Vector3 a=plant.position+plant.right*72,b=town;int n=Mathf.Max(8,Mathf.CeilToInt(Planar(a,b)/95));Vector3[] last=new Vector3[3];bool have=false;int made=0;Vector3 tan=b-a;tan.y=0;if(tan.sqrMagnitude<1)tan=Vector3.forward;tan.Normalize();
            for(int i=0;i<=n;i++){Vector3 p=Vector3.Lerp(a,b,i/(float)n);p.y=Ground(t,p);Transform tower=New("Transmission Structure "+(++made).ToString("00"),root);tower.position=p;tower.rotation=Quaternion.LookRotation(tan,Vector3.up);Box(tower,"Tower Mast",new Vector3(0,9,0),new Vector3(.7f,18,.7f),m.metal,false);Box(tower,"Upper Crossarm",new Vector3(0,15,0),new Vector3(8,.35f,.35f),m.metal,false);Box(tower,"Lower Crossarm",new Vector3(0,11.5f,0),new Vector3(11,.35f,.35f),m.metal,false);
                for(int k=-1;k<=1;k++){Vector3 local=new Vector3(k*3.4f,k==0?15.3f:11.8f,0),now=tower.TransformPoint(local);if(have)Wire(root,last[k+1],now,m.metal,"Transmission Conductor");last[k+1]=now;}have=true;}return made;
        }

        static int AddGrass(Terrain t,Bounds land,Transform root,List<Road> roads,List<Town> towns,Vector3 plant)
        {
            GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(Grass1),b=AssetDatabase.LoadAssetAtPath<GameObject>(Grass2);if(!a&&!b)return 0;System.Random rng=new System.Random(Seed+1);int made=0;
            foreach(Road r in roads){List<Vector3> p=Resample(r.p,9);for(int i=0;i<p.Count&&made<1350;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){if(rng.NextDouble()<.24)continue;Vector3 q=p[i]+side*s*(r.width*.5f+Next(rng,4,12))+tan*Next(rng,-3,3);if(!Inside(land,q,8)||Planar(q,plant)<125)continue;q.y=Ground(t,q);Spawn((rng.NextDouble()<.5?a:b)??a??b,root,q,rng,"Regional Asset Grass ",ref made);}}}
            for(int ti=0;ti<towns.Count&&made<1600;ti++)for(int k=0;k<120&&made<1600;k++){float ang=Next(rng,0,6.283f),rad=Next(rng,250,470);Vector3 q=towns[ti].c+new Vector3(Mathf.Cos(ang)*rad,0,Mathf.Sin(ang)*rad);if(!Inside(land,q,15)||Distance(q,roads)<12||Planar(q,plant)<130)continue;q.y=Ground(t,q);Spawn((rng.NextDouble()<.5?a:b)??a??b,root,q,rng,"Regional Asset Grass ",ref made);}return made;
        }
        static int AddTrees(Terrain t,Bounds land,Transform root,List<Road> roads,List<Town> towns,Vector3 plant)
        {
            GameObject leaf=AssetDatabase.LoadAssetAtPath<GameObject>(Leaf),fir=AssetDatabase.LoadAssetAtPath<GameObject>(Fir);if(!leaf&&!fir)return 0;System.Random rng=new System.Random(Seed+2);int made=0;
            foreach(Town town in towns)for(int side=-1;side<=1;side+=2){Vector3 outv=town.c-TerrainCenter(t);outv.y=0;if(outv.sqrMagnitude<1)outv=Vector3.right;outv.Normalize();Vector3 tan=Vector3.Cross(Vector3.up,outv).normalized;
                for(int i=-12;i<=12;i++){Vector3 q=town.c+outv*side*330+tan*i*22+outv*Next(rng,-18,18);if(!Inside(land,q,25)||Distance(q,roads)<18||Planar(q,plant)<150)continue;q.y=Ground(t,q);GameObject src=rng.NextDouble()<.78?leaf:fir;if(!src)src=leaf??fir;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)continue;g.name="Shelter Belt Tree "+(++made).ToString("000");g.transform.SetParent(root,false);g.transform.position=q;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.72f,1.15f);}}
            return made;
        }
        static int AddRoadDetails(Terrain t,Transform root,List<Road> roads,Mats m)
        {
            int made=0;foreach(Road r in roads){List<Vector3> p=Resample(r.p,70);for(int i=0;i<p.Count;i++){Vector3 tan=Tangent(p,i),side=Vector3.Cross(Vector3.up,tan).normalized;for(int s=-1;s<=1;s+=2){Vector3 q=p[i]+side*s*(r.width*.5f+3.3f);q.y=Ground(t,q);Transform d=New("Regional Road Detail "+(++made).ToString("000"),root);d.position=q;d.rotation=Quaternion.LookRotation(tan,Vector3.up);Box(d,"Delineator",new Vector3(0,.65f,0),new Vector3(.14f,1.3f,.14f),m.white,false);Box(d,"Reflector",new Vector3(0,1.12f,-.08f),new Vector3(.22f,.22f,.04f),s<0?m.red:m.yellow,false);}}}return made;
        }

        static void Ribbon(Terrain t,Transform root,string name,List<Vector3> path,float width,Material mat,float off,bool col)
        {
            GameObject g=new GameObject(name);g.transform.SetParent(root,false);int c=path.Count;Vector3[] v=new Vector3[c*2];Vector2[] uv=new Vector2[c*2];int[] tr=new int[(c-1)*6];float dist=0;
            for(int i=0;i<c;i++){Vector3 tan=Tangent(path,i),side=Vector3.Cross(Vector3.up,tan).normalized*width*.5f,l=path[i]-side,r=path[i]+side;l.y=Ground(t,l)+off;r.y=Ground(t,r)+off;if(i>0)dist+=Planar(path[i-1],path[i]);v[i*2]=g.transform.InverseTransformPoint(l);v[i*2+1]=g.transform.InverseTransformPoint(r);uv[i*2]=new Vector2(0,dist/7);uv[i*2+1]=new Vector2(1,dist/7);if(i<c-1){int q=i*6,j=i*2;tr[q]=j;tr[q+1]=j+2;tr[q+2]=j+1;tr[q+3]=j+1;tr[q+4]=j+2;tr[q+5]=j+3;}}
            Mesh mesh=new Mesh{name="H51_104_"+Safe(name)+"_"+(meshId++).ToString("0000")};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tr;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Gen+"/Meshes/"+mesh.name+".asset");g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(col)g.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;
        }
        static Vector3 TownEndpoint(Town town,Vector3 target){Vector3 best=town.c;float bd=float.MaxValue;foreach(Transform r in town.roads){List<Vector3> p=RoadPath(r);if(p.Count<2)continue;Vector3[] ends={p[0],p[p.Count-1]};foreach(Vector3 e in ends){float d=Planar(e,target);if(d<bd){bd=d;best=e;}}}return best;}
        static List<Vector3> RoadPath(Transform road){Transform s=FindChild(road,"Road Surface");if(!s)return new List<Vector3>();MeshFilter mf=s.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return new List<Vector3>();Vector3[] v=mf.sharedMesh.vertices;List<Vector3> p=new List<Vector3>();for(int i=0;i+1<v.Length;i+=2)p.Add((s.TransformPoint(v[i])+s.TransformPoint(v[i+1]))*.5f);return p;}
        static List<Vector3> Resample(List<Vector3> p,float step){List<Vector3> o=new List<Vector3>();if(p.Count==0)return o;o.Add(p[0]);for(int i=0;i<p.Count-1;i++){int n=Mathf.Max(1,Mathf.CeilToInt(Planar(p[i],p[i+1])/step));for(int k=1;k<=n;k++)o.Add(Vector3.Lerp(p[i],p[i+1],k/(float)n));}return o;}
        static Vector3 Tangent(List<Vector3> p,int i){if(p.Count<2)return Vector3.forward;Vector3 d=i==0?p[1]-p[0]:i==p.Count-1?p[p.Count-1]-p[p.Count-2]:p[i+1]-p[i-1];d.y=0;return d.sqrMagnitude<.001f?Vector3.forward:d.normalized;}
        static float Distance(Vector3 q,List<Road> roads){float d=float.MaxValue;foreach(Road r in roads)for(int i=0;i<r.p.Count-1;i++)d=Mathf.Min(d,SegDist(q,r.p[i],r.p[i+1]));return d;}

        static void FixText(Transform world)
        {
            Shader s=AssetDatabase.LoadAssetAtPath<Shader>(TextShader);if(!s)return;Dictionary<string,Material> mats=new Dictionary<string,Material>();
            foreach(TextMesh tm in world.GetComponentsInChildren<TextMesh>(true)){Renderer r=tm.GetComponent<Renderer>();if(!r)continue;string key=tm.font?tm.font.name:"Default";Material mat;if(!mats.TryGetValue(key,out mat)){string path=Gen+"/Materials/DepthText_"+Safe(key)+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(s){name="H51 Regional Depth Text "+key};Texture tex=tm.font&&tm.font.material?tm.font.material.mainTexture:null;if(tex)mat.SetTexture("_MainTex",tex);mat.SetColor("_Color",Color.white);mat.SetFloat("_Cutoff",.1f);AssetDatabase.CreateAsset(mat,path);}mats[key]=mat;}r.sharedMaterial=mat;}
        }
        static void Label(Transform p,string text,Vector3 pos,float scale){GameObject g=new GameObject("Sign - "+text);g.transform.SetParent(p,false);g.transform.localPosition=pos;TextMesh tm=g.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.characterSize=.5f;tm.fontSize=44;tm.color=Color.white;g.transform.localScale=Vector3.one*scale;}

        static void Spawn(GameObject src,Transform root,Vector3 p,System.Random rng,string prefix,ref int made){if(!src)return;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)return;g.name=prefix+(++made).ToString("0000");g.transform.SetParent(root,false);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.72f,1.48f);foreach(Collider c in g.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);}
        static void Car(Transform parent,Vector3 pos,float yaw,int variant,Mats m){Transform c=New("Power Station Parked Car",parent);c.localPosition=pos;c.localRotation=Quaternion.Euler(0,yaw,0);Material body=variant%3==0?m.red:variant%3==1?m.blue:m.green;Box(c,"Body",new Vector3(0,.65f,0),new Vector3(1.9f,.65f,4.2f),body,false);Box(c,"Cabin",new Vector3(0,1.15f,-.15f),new Vector3(1.65f,.65f,2),m.glass,false);}
        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static GameObject Cylinder(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static void Wire(Transform p,Vector3 a,Vector3 b,Material m,string n){Vector3 d=b-a;if(d.sqrMagnitude<.01f)return;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.position=(a+b)*.5f;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);g.transform.localScale=new Vector3(.022f,d.magnitude*.5f,.022f);if(m)g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}

        static Bounds TerrainBounds(Terrain t){Vector3 o=t.transform.position,s=t.terrainData.size;return new Bounds(o+s*.5f,s);}
        static Vector3 TerrainCenter(Terrain t){Bounds b=TerrainBounds(t);Vector3 p=b.center;p.y=Ground(t,p);return p;}
        static bool Inside(Bounds b,Vector3 p,float margin){return p.x>=b.min.x+margin&&p.x<=b.max.x-margin&&p.z>=b.min.z+margin&&p.z<=b.max.z-margin;}
        static Vector3 Clamp(Terrain t,Bounds b,Vector3 p,float margin){p.x=Mathf.Clamp(p.x,b.min.x+margin,b.max.x-margin);p.z=Mathf.Clamp(p.z,b.min.z+margin,b.max.z-margin);p.y=Ground(t,p);return p;}
        static float EdgeMargin(Bounds b,Vector3 p){return Mathf.Min(p.x-b.min.x,b.max.x-p.x,p.z-b.min.z,b.max.z-p.z);}
        static Bounds BoundsOf(GameObject g){bool set=false;Bounds b=new Bounds(g.transform.position,Vector3.zero);foreach(Renderer r in g.GetComponentsInChildren<Renderer>(true)){if(!set){b=r.bounds;set=true;}else b.Encapsulate(r.bounds);}foreach(Collider c in g.GetComponentsInChildren<Collider>(true)){if(!set){b=c.bounds;set=true;}else b.Encapsulate(c.bounds);}return b;}
        static Vector3 TownCenter(Transform town){List<Transform> h=new List<Transform>();for(int i=0;i<town.childCount;i++){Transform c=town.GetChild(i);if(c.name.StartsWith("Detailed House")||c.name.StartsWith("Building"))h.Add(c);}if(h.Count==0)return town.position;Vector3 p=Vector3.zero;foreach(Transform x in h)p+=x.position;return p/h.Count;}
        static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
        static float Planar(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        static float SegDist(Vector3 p,Vector3 a,Vector3 b){Vector2 q=new Vector2(p.x,p.z),x=new Vector2(a.x,a.z),d=new Vector2(b.x-a.x,b.z-a.z);return d.sqrMagnitude<.001f?Vector2.Distance(q,x):Vector2.Distance(q,x+d*Mathf.Clamp01(Vector2.Dot(q-x,d)/d.sqrMagnitude));}
        static float Next(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
        static Terrain FindTerrain(){GameObject g=Find(TerrainName);Terrain t=g?(g.GetComponent<Terrain>()??g.GetComponentInChildren<Terrain>(true)):null;if(t)return t;Terrain[] a=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);return a.Length>0?a[0]:null;}
        static GameObject Find(string n){GameObject g=GameObject.Find(n);if(g)return g;foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t&&t.name==n&&t.gameObject.scene.IsValid())return t.gameObject;return null;}
        static Transform FindChild(Transform r,string n){foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;return null;}
        static Transform DirectChild(Transform r,string n){for(int i=0;i<r.childCount;i++)if(r.GetChild(i).name==n)return r.GetChild(i);return null;}
        static Transform New(string n,Transform p){GameObject g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
        static int Count(Transform r,string n){if(!r)return 0;int c=0;foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t!=r&&t.name.Contains(n))c++;return c;}
        static string Safe(string n){foreach(char c in System.IO.Path.GetInvalidFileNameChars())n=n.Replace(c,'_');return n.Replace(' ','_');}
        static void ResetFolder(){if(AssetDatabase.IsValidFolder(Gen))AssetDatabase.DeleteAsset(Gen);Ensure(Gen+"/Materials");Ensure(Gen+"/Meshes");}
        static void Ensure(string path){string[] p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}}
    }
}
