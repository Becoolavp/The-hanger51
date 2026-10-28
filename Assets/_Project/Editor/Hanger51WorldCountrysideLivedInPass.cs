using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldCountrysideLivedInPass
    {
        const string WorldName="Hanger 51 Surrounding Countryside";
        const string AirportName="Hanger 51 Airport Complex";
        const string TerrainName="Hanger 51 Editable Terrain";
        const string PassName="Hanger 51 Lived-In Countryside Detail";
        const string BaseGen="Assets/_Project/Environment/Generated/CountrysideVisualUpgrade";
        const string Gen="Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Fir="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Fir/forestpack_tree_fir_tall.prefab";
        const string Leaf="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Tree/Leaf/Normal/forestpack_tree_1_leaf_1.prefab";
        const string Grass1="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_1.prefab";
        const string Grass2="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Foliage/Grass/forestpack_foliage_grassPatch_small_2.prefab";
        const string RockL="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Stone/forestpack_stone_large_1.prefab";
        const string RockM="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Stone/forestpack_stone_medium_1.prefab";
        const string Sign="Assets/Supercyan Free Forest Sample/Prefabs/High Quality/Sign/forestpack_roadSign_westEast_1.prefab";
        const int Seed=51100;

        class Road { public string n; public float w; public bool major; public List<Vector3> p=new List<Vector3>(); }
        class Town
        {
            public Transform root; public string n; public Vector3 c,f,r; public float hw,hl; public bool major;
            public List<Transform> houses=new List<Transform>();
        }
        struct Mats
        {
            public Material asphalt,gravel,line,concrete,dirt,wood,trim,foundation,roof,metal,red,blue,green,white,glass,rubber;
        }

        [MenuItem("Hanger 51/World/Current/100 - Rebuild Lived-In Countryside")]
        public static void Build()
        {
            Hanger51WorldCountrysideVisualUpgrade.Build();
            GameObject world=Find(WorldName),airport=Find(AirportName); Terrain terrain=FindTerrain();
            if(!world||!airport||!terrain){Debug.LogError("Step 100 could not find the countryside, airport, or editable terrain.");return;}
            Transform settlements=FindChild(world.transform,"Settlements");
            if(!settlements||settlements.childCount<4){Debug.LogError("Step 100 could not find the four generated settlements.",world);return;}
            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Preparing lived-in countryside",.03f);
                GameObject old=Find(PassName); if(old)UnityEngine.Object.DestroyImmediate(old);
                ResetFolder(); Transform pass=New(PassName,world.transform); Mats m=BuildMats();
                Bounds safety=AirportSafetyBounds(airport); Vector3 ac=safety.center; ac.y=Ground(terrain,ac);
                float safe=Mathf.Max(3000f,Mathf.Sqrt(safety.extents.x*safety.extents.x+safety.extents.z*safety.extents.z)+900f);
                FixTerrainFinish(terrain); FixBaseMaterials();

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Moving and organizing towns",.14f);
                List<Town> towns=MoveAndLayoutTowns(terrain,settlements,ac,safe);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Replacing spider roads",.28f);
                Transform oldRoads=DirectChild(world.transform,"Road Network"); if(oldRoads)UnityEngine.Object.DestroyImmediate(oldRoads.gameObject);
                Transform roadRoot=New("Road Network",world.transform); List<Road> roads=BuildRoads(terrain,roadRoot,towns,ac,safe,m);
                PaintTerrain(terrain,roads);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Adding lived-in residential detail",.45f);
                Transform residential=New("Residential Details",pass); AddHomes(terrain,residential,towns,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Adding purposeful buildings and farms",.58f);
                Transform purpose=New("Purposeful Buildings",pass); AddPurpose(terrain,purpose,towns,m); AddFarms(terrain,purpose,towns,m);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Adding utilities and roadside detail",.68f);
                Transform infrastructure=New("Infrastructure",pass); AddUtilities(terrain,infrastructure,towns,m); AddSigns(terrain,infrastructure,towns,ac);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Rebuilding forests and grass with project assets",.78f);
                RebuildTrees(terrain,towns,roads,ac,safe); RebuildDetailGrass(terrain,towns,roads,ac,safe);
                Transform vegetation=New("Asset Vegetation",pass); AddGrassAssets(terrain,vegetation,towns,roads,ac,safe); AddTownTrees(terrain,vegetation,towns,roads);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Replacing rocks with project assets",.90f);
                ReplaceRocks(terrain,world.transform,towns,roads,ac,safe);
                AddParks(terrain,purpose,towns,m);

                terrain.Flush(); EditorUtility.SetDirty(terrain.terrainData); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes(); Selection.activeGameObject=pass.gameObject;
                Debug.Log($"Step 100 complete. Airport/runway exclusion={safe:0}m; roads={Count(world.transform,"Road Surface")}, lived-in homes={Count(world.transform,"Home Detail Set")}, purposeful sites={Count(pass,"Purpose -")}, trees={terrain.terrainData.treeInstanceCount}.",pass.gameObject);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        [MenuItem("Hanger 51/World/Current/101 - Validate Lived-In Countryside")]
        public static void Validate()
        {
            GameObject world=Find(WorldName),airport=Find(AirportName),pass=Find(PassName); Terrain terrain=FindTerrain();
            if(!world||!airport||!pass||!terrain){Debug.LogError("Step 101 failed: run Step 100 first.");return;}
            Bounds safety=AirportSafetyBounds(airport); Vector3 ac=safety.center; float safe=Mathf.Max(3000f,Mathf.Sqrt(safety.extents.x*safety.extents.x+safety.extents.z*safety.extents.z)+900f);
            Transform settlements=FindChild(world.transform,"Settlements"); float nearestTown=float.MaxValue;
            if(settlements)for(int i=0;i<settlements.childCount;i++){List<Transform> h=Houses(settlements.GetChild(i));if(h.Count>0)nearestTown=Mathf.Min(nearestTown,Dist(Average(h),ac));}
            float nearestRoad=float.MaxValue; Transform rr=DirectChild(world.transform,"Road Network");
            if(rr)foreach(MeshFilter mf in rr.GetComponentsInChildren<MeshFilter>(true))if(mf.gameObject.name=="Road Surface"&&mf.sharedMesh){Vector3[] v=mf.sharedMesh.vertices;for(int i=0;i<v.Length;i++)nearestRoad=Mathf.Min(nearestRoad,Dist(mf.transform.TransformPoint(v[i]),ac));}
            int roads=Count(world.transform,"Road Surface"),homes=Count(world.transform,"Home Detail Set"),purpose=Count(pass.transform,"Purpose -"),cars=Count(world.transform,"Parked Car"),farms=Count(pass.transform,"Farmstead"),grass=Count(pass.transform,"Asset Grass"),townTrees=Count(pass.transform,"Town Tree"),rocks=Count(world.transform,"Asset Rock");
            bool matte=true; if(terrain.terrainData.terrainLayers!=null)foreach(TerrainLayer l in terrain.terrainData.terrainLayers)if(l&&(l.smoothness>.06f||l.metallic>.01f))matte=false;
            bool ok=nearestTown>safe+700f&&nearestRoad>=safe-60f&&roads>=16&&homes>=70&&purpose>=8&&cars>=16&&farms>=3&&grass>=160&&townTrees>=30&&rocks>=140&&terrain.terrainData.treeInstanceCount>=5000&&matte;
            if(ok)Debug.Log($"Step 101 passed. nearest town={nearestTown:0}m, nearest road={nearestRoad:0}m, roads={roads}, homes={homes}, purposeful sites={purpose}, cars={cars}, farms={farms}, asset grass={grass}, town trees={townTrees}, rocks={rocks}, terrain trees={terrain.terrainData.treeInstanceCount}. Airport/runway exclusion is clear and ground layers are matte.",pass);
            else Debug.LogError($"Step 101 failed. nearest town={nearestTown:0}m (need > {safe+700f:0}), nearest road={nearestRoad:0}m (need >= {safe-60f:0}), roads={roads}, homes={homes}, purposeful={purpose}, cars={cars}, farms={farms}, asset grass={grass}, town trees={townTrees}, rocks={rocks}, terrain trees={terrain.terrainData.treeInstanceCount}, matte={matte}.",pass);
        }

        static Mats BuildMats()
        {
            Mats m=new Mats();
            m.asphalt=Mat("Matte Asphalt",new Color(.075f,.08f,.085f),0); m.gravel=Mat("Matte Gravel",new Color(.34f,.33f,.29f),0); m.line=Mat("Road Paint",new Color(.94f,.78f,.18f),0);
            m.concrete=Mat("Concrete",new Color(.53f,.52f,.49f),0); m.dirt=Mat("Packed Dirt",new Color(.30f,.22f,.14f),0); m.wood=Mat("Weathered Wood",new Color(.29f,.17f,.09f),0);
            m.trim=Mat("Painted Trim",new Color(.82f,.80f,.73f),.02f); m.foundation=Mat("Foundation Stone",new Color(.42f,.41f,.38f),0); m.roof=Mat("Utility Roof",new Color(.16f,.14f,.13f),0);
            m.metal=Mat("Dark Metal",new Color(.14f,.15f,.16f),.05f); m.red=Mat("Barn Red",new Color(.46f,.15f,.10f),0); m.blue=Mat("Civic Blue",new Color(.23f,.35f,.42f),0); m.green=Mat("Farm Green",new Color(.22f,.30f,.17f),0);
            m.white=Mat("Warm White",new Color(.83f,.81f,.74f),0); m.glass=Mat("Dark Glass",new Color(.08f,.15f,.18f),.20f); m.rubber=Mat("Rubber",new Color(.035f,.035f,.035f),0); return m;
        }

        static Material Mat(string name,Color c,float smooth)
        {
            Shader s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"); Material m=new Material(s){name="H51_"+name,color=c};
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",c); if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth); if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0); if(m.HasProperty("_SpecColor"))m.SetColor("_SpecColor",Color.black);
            m.enableInstancing=true; AssetDatabase.CreateAsset(m,Gen+"/Materials/"+Safe(name)+".mat"); return m;
        }

        static void FixTerrainFinish(Terrain t)
        {
            TerrainLayer[] l=t.terrainData.terrainLayers; if(l==null)return;
            for(int i=0;i<l.Length;i++)if(l[i]){l[i].smoothness=0;l[i].metallic=0;l[i].normalScale=Mathf.Min(l[i].normalScale,.85f);float tile=i==0?42:i==1?58:i==2?14:30;l[i].tileSize=new Vector2(tile,tile);EditorUtility.SetDirty(l[i]);}
        }

        static void FixBaseMaterials()
        {
            string[] n={"Asphalt","Gravel","Boulder","Shingles","Wood","Trim","Foundation","Wall_Cream","Wall_Gray","Wall_Blue","Wall_Red"};
            for(int i=0;i<n.Length;i++){Material m=AssetDatabase.LoadAssetAtPath<Material>(BaseGen+"/Materials/"+n[i]+".mat");if(!m)continue;if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",0);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);if(m.HasProperty("_SpecColor"))m.SetColor("_SpecColor",Color.black);EditorUtility.SetDirty(m);}
        }

        static List<Town> MoveAndLayoutTowns(Terrain t,Transform settlements,Vector3 ac,float safe)
        {
            List<Town> towns=new List<Town>(); float[] ang={38,142,318,222}; float[] extra={2850,3350,3150,3500};
            for(int k=0;k<settlements.childCount;k++)
            {
                Transform tr=settlements.GetChild(k); List<Transform> hs=Houses(tr); if(hs.Count==0)continue; bool major=k==0;
                float a=ang[Mathf.Min(k,3)]*Mathf.Deg2Rad; Vector3 dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)); float max=MaxTravel(t,ac,dir,650); float wanted=Mathf.Min(safe+extra[Mathf.Min(k,3)],max-180); if(wanted<safe+950)wanted=Mathf.Min(max-100,safe+950);
                Vector3 center=Clamp(t,ac+dir*wanted,600); Vector3 old=Average(hs),delta=center-old;delta.y=0;tr.position+=delta;
                Vector3 f=Quaternion.Euler(0,major?24:k==1?-22:k==2?42:-38,0)*Vector3.forward,r=Vector3.Cross(Vector3.up,f).normalized;
                int streets=major?4:2; int per=Mathf.CeilToInt(hs.Count/(float)streets); int slots=Mathf.CeilToInt(per/2f); float spread=major?110:96,step=major?52:48;
                for(int i=0;i<hs.Count;i++)
                {
                    int street=i%streets,seq=i/streets; float side=(seq%2==0)?-1:1; int slot=seq/2; float x=(street-(streets-1)*.5f)*spread+side*(major?27:25); float z=(slot-(slots-1)*.5f)*step;
                    Vector3 p=center+r*x+f*z;p.y=Ground(t,p);hs[i].position=p;hs[i].rotation=Quaternion.LookRotation(-r*side,Vector3.up);
                }
                Town q=new Town{root=tr,n=tr.name,c=center,f=f,r=r,major=major,hw=major?235:145,hl=major?225:155};q.houses.AddRange(hs);towns.Add(q);
            }
            return towns;
        }

        static List<Road> BuildRoads(Terrain t,Transform root,List<Town> towns,Vector3 ac,float safe,Mats m)
        {
            List<Road> roads=new List<Road>();
            for(int k=0;k<towns.Count;k++)
            {
                Town q=towns[k]; int av=q.major?4:2,cross=q.major?4:3; float sp=q.major?110:96;
                for(int i=0;i<av;i++){float x=(i-(av-1)*.5f)*sp;Vector3 a=q.c+q.r*x-q.f*q.hl,b=q.c+q.r*x+q.f*q.hl;AddStraight(t,root,roads,q.n+" Avenue "+(i+1),a,b,q.major?5.8f:5.1f,i==av/2,m);}
                for(int i=0;i<cross;i++){float z=Mathf.Lerp(-q.hl,q.hl,cross==1?.5f:i/(float)(cross-1));Vector3 a=q.c+q.f*z-q.r*q.hw,b=q.c+q.f*z+q.r*q.hw;AddStraight(t,root,roads,q.n+" Street "+(i+1),a,b,q.major?5.4f:4.8f,i==cross/2,m);}
            }
            if(towns.Count>0)
            {
                Town main=towns[0];Vector3 d=(main.c-ac).normalized;Vector3 airportEdge=ac+d*(safe+35),townEdge=TownEdge(main,-d);AddConnector(t,root,roads,"Airport Access Road",airportEdge,townEdge,ac,safe,8,true,m);
            }
            if(towns.Count>=4)
            {
                int[,] e={{0,1},{1,3},{3,2},{2,0}};for(int i=0;i<4;i++){Town a=towns[e[i,0]],b=towns[e[i,1]];Vector3 d=(b.c-a.c).normalized;AddConnector(t,root,roads,"County Road "+(i+1),TownEdge(a,d),TownEdge(b,-d),ac,safe,7,true,m);}
            }
            return roads;
        }

        static void AddStraight(Terrain t,Transform root,List<Road> roads,string name,Vector3 a,Vector3 b,float w,bool major,Mats m)
        {
            Road r=new Road{n=name,w=w,major=major};int n=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a,b)/24),5,100);for(int i=0;i<=n;i++){Vector3 p=Vector3.Lerp(a,b,i/(float)n);p.y=Ground(t,p)+.06f;r.p.Add(p);}FinishRoad(root,roads,r,m);
        }

        static void AddConnector(Terrain t,Transform root,List<Road> roads,string name,Vector3 a,Vector3 b,Vector3 ac,float safe,float w,bool major,Mats m)
        {
            Road r=new Road{n=name,w=w,major=major};float clearance=SegDist(ac,a,b);
            if(clearance>=safe+160)
            {
                Vector3 d=b-a;d.y=0;Vector3 side=Vector3.Cross(Vector3.up,d.normalized);Vector3 mid=(a+b)*.5f+side*Mathf.Sin((a.x+b.z)*.0017f)*Mathf.Min(180,Vector3.Distance(a,b)*.08f);int n=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a,b)/28),8,160);for(int i=0;i<=n;i++){Vector3 p=Bezier(a,mid,b,i/(float)n);p.y=Ground(t,p)+.06f;r.p.Add(p);}
            }
            else
            {
                Vector3 da=a-ac,db=b-ac;da.y=db.y=0;float aa=Mathf.Atan2(da.z,da.x)*Mathf.Rad2Deg,bb=Mathf.Atan2(db.z,db.x)*Mathf.Rad2Deg,delta=Mathf.DeltaAngle(aa,bb);float rad=safe+520;
                Vector3 pa=ac+da.normalized*rad,pb=ac+db.normalized*rad;AddSegmentPoints(t,r.p,a,pa,28,false);
                int arc=Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta)/10),6,28);for(int i=1;i<arc;i++){float an=(aa+delta*i/(float)arc)*Mathf.Deg2Rad;Vector3 p=ac+new Vector3(Mathf.Cos(an),0,Mathf.Sin(an))*rad;p.y=Ground(t,p)+.06f;r.p.Add(p);}AddSegmentPoints(t,r.p,pb,b,28,true);
            }
            for(int i=0;i<r.p.Count;i++){Vector3 d=r.p[i]-ac;d.y=0;if(d.magnitude<safe){d=d.sqrMagnitude<1?Vector3.right:d.normalized;r.p[i]=ac+d*safe;r.p[i]=new Vector3(r.p[i].x,Ground(t,r.p[i])+.06f,r.p[i].z);}}
            FinishRoad(root,roads,r,m);
        }

        static void AddSegmentPoints(Terrain t,List<Vector3> list,Vector3 a,Vector3 b,float step,bool includeEnd)
        {
            int n=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(a,b)/step));for(int i=0;i<=n;i++){if(i==n&&!includeEnd)continue;if(i==0&&list.Count>0)continue;Vector3 p=Vector3.Lerp(a,b,i/(float)n);p.y=Ground(t,p)+.06f;list.Add(p);}
        }

        static void FinishRoad(Transform root,List<Road> roads,Road r,Mats m)
        {
            if(r.p.Count<2)return;roads.Add(r);Transform p=New(r.n,root);Ribbon(p,"Gravel Shoulder",r.p,r.w+(r.major?7:4),m.gravel,.01f,false);Ribbon(p,"Road Surface",r.p,r.w,m.asphalt,.055f,true);if(r.major)Ribbon(p,"Center Line",r.p,.18f,m.line,.075f,false);
        }

        static void Ribbon(Transform parent,string name,List<Vector3> p,float w,Material mat,float y,bool col)
        {
            int c=p.Count;Vector3[] v=new Vector3[c*2];Vector2[] uv=new Vector2[c*2];int[] tr=new int[(c-1)*6];float dist=0;
            for(int i=0;i<c;i++){Vector3 f=i==0?p[1]-p[0]:i==c-1?p[c-1]-p[c-2]:p[i+1]-p[i-1];f.y=0;f.Normalize();Vector3 side=Vector3.Cross(Vector3.up,f)*w*.5f;if(i>0)dist+=Dist(p[i-1],p[i]);v[i*2]=parent.InverseTransformPoint(p[i]-side+Vector3.up*y);v[i*2+1]=parent.InverseTransformPoint(p[i]+side+Vector3.up*y);uv[i*2]=new Vector2(0,dist/8);uv[i*2+1]=new Vector2(1,dist/8);if(i<c-1){int q=i*6,j=i*2;tr[q]=j;tr[q+1]=j+2;tr[q+2]=j+1;tr[q+3]=j+1;tr[q+4]=j+2;tr[q+5]=j+3;}}
            Mesh mesh=new Mesh{name="H51_"+Safe(name)+"_"+Guid.NewGuid().ToString("N").Substring(0,8)};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tr;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Gen+"/Meshes/"+mesh.name+".asset");GameObject g=new GameObject(name);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(col)g.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;
        }

        static void PaintTerrain(Terrain t,List<Road> roads)
        {
            TerrainData d=t.terrainData;int layers=d.terrainLayers==null?0:d.terrainLayers.Length;if(layers<4)return;int W=d.alphamapWidth,H=d.alphamapHeight;float[,,] a=new float[H,W,layers];Vector3 o=t.transform.position,s=d.size;
            for(int z=0;z<H;z++)for(int x=0;x<W;x++){float nx=x/(float)(W-1),nz=z/(float)(H-1),wx=o.x+nx*s.x,wz=o.z+nz*s.z;float field=Fbm(wx*.00055f,wz*.00055f,3)*.30f,stone=Mathf.InverseLerp(20,38,d.GetSteepness(nx,nz))*.55f;a[z,x,3]=stone;a[z,x,1]=field*(1-stone);a[z,x,0]=1-a[z,x,1]-stone;}
            foreach(Road r in roads)foreach(Vector3 p in r.p){int cx=Mathf.RoundToInt((p.x-o.x)/s.x*(W-1)),cz=Mathf.RoundToInt((p.z-o.z)/s.z*(H-1));int rx=Mathf.Max(1,Mathf.CeilToInt((r.w*.5f+7)/s.x*W)),rz=Mathf.Max(1,Mathf.CeilToInt((r.w*.5f+7)/s.z*H));for(int z=Mathf.Max(0,cz-rz);z<=Mathf.Min(H-1,cz+rz);z++)for(int x=Mathf.Max(0,cx-rx);x<=Mathf.Min(W-1,cx+rx);x++){float dd=Mathf.Sqrt(Mathf.Pow((x-cx)/(float)rx,2)+Mathf.Pow((z-cz)/(float)rz,2));if(dd>1)continue;float g=(1-dd)*.80f;a[z,x,2]=Mathf.Max(a[z,x,2],g);float rem=1-a[z,x,2],sum=a[z,x,0]+a[z,x,1]+a[z,x,3];if(sum>0){a[z,x,0]*=rem/sum;a[z,x,1]*=rem/sum;a[z,x,3]*=rem/sum;}}}d.SetAlphamaps(0,0,a);
        }

        static void AddHomes(Terrain t,Transform root,List<Town> towns,Mats m)
        {
            System.Random rng=new System.Random(Seed+11);foreach(Town town in towns)for(int i=0;i<town.houses.Count;i++)
            {
                Transform h=town.houses[i];Transform old=DirectChild(h,"Home Detail Set");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);Transform d=New("Home Detail Set",h);float w=12,depth=11;Transform siding=DirectChild(h,"Textured Siding");if(siding){w=Mathf.Max(8,siding.localScale.x);depth=Mathf.Max(8,siding.localScale.z);}
                Box(d,"Front Porch",new Vector3(0,.18f,depth*.5f+1.25f),new Vector3(4.2f,.36f,2.2f),m.wood,false);Box(d,"Porch Step",new Vector3(0,.08f,depth*.5f+2.45f),new Vector3(2.5f,.16f,.65f),m.concrete,false);Box(d,"Walkway",new Vector3(0,.025f,depth*.5f+5.2f),new Vector3(1.35f,.05f,5.2f),m.concrete,false);
                Box(d,"Mailbox Post",new Vector3(-3.0f,.55f,depth*.5f+7.4f),new Vector3(.10f,1.1f,.10f),m.wood,false);Box(d,"Mailbox",new Vector3(-3.0f,1.0f,depth*.5f+7.4f),new Vector3(.52f,.32f,.42f),m.trim,false);Box(d,"Trash Bin",new Vector3(w*.5f+1.4f,.55f,-depth*.15f),new Vector3(.75f,1.1f,.75f),m.green,false);
                float back=-depth*.5f-7.5f,left=-w*.5f-6,right=w*.5f+6;Box(d,"Back Fence",new Vector3(0,.48f,back),new Vector3(w+12,.96f,.10f),m.wood,false);Box(d,"Left Fence",new Vector3(left,.48f,-1),new Vector3(.10f,.96f,depth+13),m.wood,false);Box(d,"Right Fence",new Vector3(right,.48f,-1),new Vector3(.10f,.96f,depth+13),m.wood,false);
                for(int s=0;s<3;s++)Shrub(d,new Vector3(-w*.32f+s*w*.32f,.35f,depth*.5f+.45f),m.green);
                if(i%2==0){float side=i%4==0?1:-1;Transform g=New("Detached Garage",d);g.localPosition=new Vector3(side*(w*.5f+5.2f),0,-depth*.3f);Box(g,"Garage Body",new Vector3(0,2.1f,0),new Vector3(6.2f,4.2f,7.4f),m.white,true);Box(g,"Garage Roof",new Vector3(0,4.4f,0),new Vector3(6.8f,.55f,8),m.roof,false);Box(g,"Garage Door",new Vector3(0,1.55f,3.76f),new Vector3(4.5f,2.8f,.10f),m.trim,false);}else{Transform shed=New("Backyard Shed",d);shed.localPosition=new Vector3(w*.24f,0,-depth*.5f-4.6f);Box(shed,"Shed",new Vector3(0,1.4f,0),new Vector3(3.2f,2.8f,3.6f),m.wood,false);Box(shed,"Shed Roof",new Vector3(0,2.95f,0),new Vector3(3.6f,.30f,4),m.roof,false);}
                if(i%3!=1){Vector3 carPos=new Vector3((i%2==0?1:-1)*(w*.5f+3.2f),.05f,depth*.5f+4.8f);Car(d,"Parked Car",carPos,Quaternion.identity,i,m);}
                Box(d,"Utility Box",new Vector3(-w*.5f-.5f,.45f,-depth*.2f),new Vector3(.65f,.9f,.38f),m.metal,false);
            }
        }

        static void AddPurpose(Terrain t,Transform root,List<Town> towns,Mats m)
        {
            string[] main={"General Store","Fire Station","Post Office","Town Hall","Repair Garage","Clinic","Cafe","Farm Supply"};
            for(int k=0;k<towns.Count;k++)
            {
                Town q=towns[k];int count=q.major?main.Length:2;for(int i=0;i<count;i++){string type=q.major?main[i]:(i==0?"Community Store":"Community Hall");float x=(i-(count-1)*.5f)*(q.major?45:38);Vector3 p=q.c+q.f*(q.hl+44)+q.r*x;p.y=Ground(t,p);PurposeBuilding(root,"Purpose - "+q.n+" "+type,p,Quaternion.LookRotation(-q.f,Vector3.up),type,i,m);}
            }
        }

        static void PurposeBuilding(Transform root,string name,Vector3 p,Quaternion rot,string type,int index,Mats m)
        {
            Transform r=New(name,root);r.position=p;r.rotation=rot;Material wall=type.Contains("Fire")?m.red:type.Contains("Clinic")?m.blue:type.Contains("Farm")?m.green:m.white;float w=type.Contains("Garage")||type.Contains("Fire")?22:16,d=type.Contains("Town Hall")?18:14,h=type.Contains("Fire")?8:6;
            Box(r,"Foundation",new Vector3(0,.25f,0),new Vector3(w+.5f,.5f,d+.5f),m.foundation,true);Box(r,"Purpose Building",new Vector3(0,h*.5f+.5f,0),new Vector3(w,h,d),wall,true);Box(r,"Roof",new Vector3(0,h+.75f,0),new Vector3(w+1,.5f,d+1),m.roof,false);
            if(type.Contains("Fire")||type.Contains("Garage")){for(int x=-1;x<=1;x+=2)Box(r,"Service Bay",new Vector3(x*w*.24f,2.5f,d*.5f+.06f),new Vector3(w*.38f,4.5f,.10f),m.trim,false);}else{Box(r,"Front Door",new Vector3(0,2.0f,d*.5f+.06f),new Vector3(1.8f,3.3f,.10f),m.wood,false);for(int x=-1;x<=1;x+=2)Box(r,"Display Window",new Vector3(x*w*.27f,2.8f,d*.5f+.08f),new Vector3(3.3f,2.6f,.08f),m.glass,false);}
            if(type.Contains("Store")||type.Contains("Cafe")||type.Contains("Supply"))Box(r,"Awning",new Vector3(0,4.2f,d*.5f+1.0f),new Vector3(w*.72f,.20f,1.8f),type.Contains("Farm")?m.green:m.red,false);
            Label(r,type,new Vector3(0,h-.3f,d*.5f+.15f),m.white);Box(r,"Parking Pad",new Vector3(0,.02f,d*.5f+9),new Vector3(w+10,.04f,10),m.concrete,false);if(index%2==0)Car(r,"Parked Car",new Vector3(-w*.22f,.05f,d*.5f+8),Quaternion.identity,index+20,m);
            if(type.Contains("Town Hall")){Box(r,"Flag Pole",new Vector3(w*.38f,5,d*.5f+5),new Vector3(.12f,10,.12f),m.metal,false);Box(r,"Flag",new Vector3(w*.38f+.65f,8.7f,d*.5f+5),new Vector3(1.3f,.7f,.05f),m.blue,false);}
        }

        static void AddFarms(Terrain t,Transform root,List<Town> towns,Mats m)
        {
            for(int k=1;k<towns.Count;k++)
            {
                Town q=towns[k];Transform f=New("Farmstead - "+q.n,root);Vector3 p=q.c-q.f*(q.hl+190)+q.r*(k%2==0?115:-115);p.y=Ground(t,p);f.position=p;f.rotation=Quaternion.LookRotation(q.f,Vector3.up);
                Box(f,"Dirt Yard",new Vector3(0,.02f,0),new Vector3(70,.04f,58),m.dirt,false);Box(f,"Barn",new Vector3(-12,5,0),new Vector3(20,10,28),m.red,true);Box(f,"Barn Roof",new Vector3(-12,10.5f,0),new Vector3(22,1,30),m.roof,false);Box(f,"Equipment Shed",new Vector3(18,3,-10),new Vector3(18,6,15),m.green,true);Box(f,"Equipment Roof",new Vector3(18,6.4f,-10),new Vector3(19,.7f,16),m.roof,false);
                Cylinder(f,"Silo",new Vector3(20,6.5f,13),new Vector3(4.5f,6.5f,4.5f),m.metal,true);for(int i=0;i<8;i++){float x=-31+i*9;Box(f,"Paddock Fence",new Vector3(x,.8f,30),new Vector3(7.5f,1.6f,.10f),m.wood,false);}for(int i=0;i<10;i++)Cylinder(f,"Hay Bale",new Vector3(-28+i%5*5,.75f,-23+i/5*5),new Vector3(.8f,.75f,.8f),m.dirt,false);
            }
        }

        static void AddUtilities(Terrain t,Transform root,List<Town> towns,Mats m)
        {
            foreach(Town q in towns){Transform line=New("Utility Corridor - "+q.n,root);int n=q.major?12:7;Vector3 last=Vector3.zero;for(int i=0;i<n;i++){float z=Mathf.Lerp(-q.hl-35,q.hl+35,n==1?.5f:i/(float)(n-1));Vector3 p=q.c-q.r*(q.hw+17)+q.f*z;p.y=Ground(t,p);Transform pole=New("Utility Pole",line);pole.position=p;Box(pole,"Pole",new Vector3(0,4.5f,0),new Vector3(.24f,9,.24f),m.wood,false);Box(pole,"Crossarm",new Vector3(0,8.25f,0),new Vector3(2.2f,.18f,.18f),m.wood,false);if(i>0){Wire(line,last+Vector3.up*8.25f,p+Vector3.up*8.25f,m.metal);}last=p;}}
        }

        static void AddSigns(Terrain t,Transform root,List<Town> towns,Vector3 ac)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Sign);if(!prefab)return;Transform signs=New("Road Signs",root);foreach(Town q in towns){Vector3 d=(q.c-ac).normalized,p=TownEdge(q,-d)-d*18;p.y=Ground(t,p);GameObject g=PrefabUtility.InstantiatePrefab(prefab) as GameObject;if(!g)continue;g.name="Town Road Sign - "+q.n;g.transform.SetParent(signs,false);g.transform.position=p;g.transform.rotation=Quaternion.LookRotation(q.r,Vector3.up);g.transform.localScale=Vector3.one*1.15f;}
        }

        static void RebuildTrees(Terrain t,List<Town> towns,List<Road> roads,Vector3 ac,float safe)
        {
            GameObject fir=AssetDatabase.LoadAssetAtPath<GameObject>(Fir),leaf=AssetDatabase.LoadAssetAtPath<GameObject>(Leaf);TerrainData d=t.terrainData;if(fir&&leaf)d.treePrototypes=new[]{new TreePrototype{prefab=fir,bendFactor=.25f},new TreePrototype{prefab=leaf,bendFactor=.32f}};if(d.treePrototypes==null||d.treePrototypes.Length==0){Debug.LogWarning("Step 100 found no usable tree prefabs.");return;}
            List<TreeInstance> a=new List<TreeInstance>();System.Random rng=new System.Random(Seed+51);Vector3 o=t.transform.position,s=d.size;int target=7000,tries=0;
            while(a.Count<target&&tries++<120000){float nx=Next(rng,.015f,.985f),nz=Next(rng,.015f,.985f);Vector3 p=new Vector3(o.x+nx*s.x,0,o.z+nz*s.z);if(Dist(p,ac)<safe+100||NearTown(p,towns,90)||NearRoad(p,roads,16))continue;float forest=Fbm(p.x*.00068f+31,p.z*.00068f+63,4);if(forest<.43f||Next(rng,0,1)>Mathf.Clamp01((forest-.40f)*2.2f))continue;float sc=Next(rng,.78f,1.28f);a.Add(new TreeInstance{position=new Vector3(nx,d.GetInterpolatedHeight(nx,nz)/Mathf.Max(1,s.y),nz),prototypeIndex=rng.Next(d.treePrototypes.Length),widthScale=sc*Next(rng,.90f,1.12f),heightScale=sc,rotation=Next(rng,0,6.283f),color=Color.white,lightmapColor=Color.white});}d.treeInstances=a.ToArray();
        }

        static void RebuildDetailGrass(Terrain t,List<Town> towns,List<Road> roads,Vector3 ac,float safe)
        {
            TerrainData d=t.terrainData;if(d.detailPrototypes==null||d.detailPrototypes.Length==0)return;int W=d.detailWidth,H=d.detailHeight;int[,] map=new int[H,W];Vector3 o=t.transform.position,s=d.size;
            for(int z=0;z<H;z++)for(int x=0;x<W;x++){Vector3 p=new Vector3(o.x+x/(float)(W-1)*s.x,0,o.z+z/(float)(H-1)*s.z);if(Dist(p,ac)<safe+80||NearTown(p,towns,38)||NearRoad(p,roads,7))continue;float n=Fbm(p.x*.002f,p.z*.002f,3);if(n>.31f)map[z,x]=Mathf.RoundToInt(Mathf.Lerp(1,7,n));}d.SetDetailLayer(0,0,0,map);
        }

        static void AddGrassAssets(Terrain t,Transform root,List<Town> towns,List<Road> roads,Vector3 ac,float safe)
        {
            GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(Grass1),b=AssetDatabase.LoadAssetAtPath<GameObject>(Grass2);if(!a&&!b)return;System.Random rng=new System.Random(Seed+101);int made=0,tries=0;
            while(made<260&&tries++<8000){Vector3 p=Rand(rng,t);if(Dist(p,ac)<safe+80||NearTown(p,towns,25)||NearRoad(p,roads,5))continue;float n=Fbm(p.x*.0017f,p.z*.0017f,3);if(n<.48f)continue;GameObject src=(rng.NextDouble()<.5?a:b)??a??b;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)continue;g.name="Asset Grass "+(++made).ToString("000");g.transform.SetParent(root,false);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.75f,1.35f);}
        }

        static void AddTownTrees(Terrain t,Transform root,List<Town> towns,List<Road> roads)
        {
            GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(Leaf),b=AssetDatabase.LoadAssetAtPath<GameObject>(Fir);if(!a&&!b)return;System.Random rng=new System.Random(Seed+202);int made=0;
            foreach(Town q in towns)for(int i=0;i<(q.major?22:12);i++){float an=Next(rng,0,6.283f),rad=Next(rng,Mathf.Max(q.hw,q.hl)+35,Mathf.Max(q.hw,q.hl)+105);Vector3 p=q.c+new Vector3(Mathf.Cos(an)*rad,0,Mathf.Sin(an)*rad);p.y=Ground(t,p);if(NearRoad(p,roads,8))continue;GameObject src=rng.NextDouble()<.72?a:b;if(!src)src=a??b;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)continue;g.name="Town Tree "+(++made).ToString("000");g.transform.SetParent(root,false);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,Next(rng,0,360),0);g.transform.localScale=Vector3.one*Next(rng,.75f,1.12f);}
        }

        static void ReplaceRocks(Terrain t,Transform world,List<Town> towns,List<Road> roads,Vector3 ac,float safe)
        {
            Transform natural=DirectChild(world,"Natural Features");if(!natural)natural=New("Natural Features",world);Transform old=DirectChild(natural,"Rocks");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);Transform root=New("Rocks",natural);GameObject a=AssetDatabase.LoadAssetAtPath<GameObject>(RockL),b=AssetDatabase.LoadAssetAtPath<GameObject>(RockM);if(!a&&!b){Debug.LogWarning("Step 100 could not load Supercyan rock prefabs.");return;}System.Random rng=new System.Random(Seed+303);int made=0,tries=0;
            while(made<180&&tries++<12000){Vector3 p=Rand(rng,t);if(Dist(p,ac)<safe+150||NearTown(p,towns,70)||NearRoad(p,roads,13))continue;GameObject src=rng.NextDouble()<.42?a:b;if(!src)src=a??b;GameObject g=PrefabUtility.InstantiatePrefab(src) as GameObject;if(!g)continue;g.name="Asset Rock "+(++made).ToString("000");g.transform.SetParent(root,false);g.transform.position=p-Vector3.up*Next(rng,.02f,.16f);g.transform.rotation=Quaternion.Euler(Next(rng,-10,10),Next(rng,0,360),Next(rng,-10,10));g.transform.localScale=Vector3.one*Next(rng,.7f,1.8f);if(!g.GetComponentInChildren<Collider>()){MeshFilter mf=g.GetComponentInChildren<MeshFilter>();if(mf&&mf.sharedMesh){MeshCollider mc=g.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;}}}
        }

        static void AddParks(Terrain t,Transform root,List<Town> towns,Mats m)
        {
            foreach(Town q in towns){Transform p=New("Community Park - "+q.n,root);Vector3 pos=q.c-q.f*(q.hl+55);pos.y=Ground(t,pos);p.position=pos;p.rotation=Quaternion.LookRotation(q.r,Vector3.up);Box(p,"Park Path",new Vector3(0,.02f,0),new Vector3(q.major?30:20,.04f,4),m.concrete,false);for(int s=-1;s<=1;s+=2){Box(p,"Bench",new Vector3(s*6,.45f,3),new Vector3(3,.20f,.65f),m.wood,false);Box(p,"Bench Back",new Vector3(s*6,1.0f,3.25f),new Vector3(3,1,.15f),m.wood,false);}Box(p,"Picnic Table",new Vector3(0,.75f,-4),new Vector3(3.5f,.20f,1.6f),m.wood,false);Box(p,"Trash Can",new Vector3(4,.6f,-2),new Vector3(.7f,1.2f,.7f),m.green,false);}
        }

        static void Car(Transform parent,string name,Vector3 pos,Quaternion rot,int variant,Mats m)
        {
            Transform c=New(name,parent);c.localPosition=pos;c.localRotation=rot;Material body=variant%3==0?m.red:variant%3==1?m.blue:m.green;Box(c,"Body",new Vector3(0,.65f,0),new Vector3(1.9f,.65f,4.2f),body,false);Box(c,"Cabin",new Vector3(0,1.15f,-.15f),new Vector3(1.65f,.65f,2.0f),m.glass,false);for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Cylinder(c,"Wheel",new Vector3(x*1.0f,.42f,z*1.35f),new Vector3(.32f,.18f,.32f),m.rubber,false,Quaternion.Euler(0,0,90));
        }

        static void Shrub(Transform p,Vector3 pos,Material m){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="Yard Shrub";g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new Vector3(1.1f,.75f,.85f);g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;}
        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static GameObject Cylinder(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col,Quaternion? rot=null){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(rot.HasValue)g.transform.localRotation=rot.Value;g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static void Wire(Transform p,Vector3 a,Vector3 b,Material m){Vector3 d=b-a;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Power Wire";g.transform.SetParent(p,false);g.transform.position=(a+b)*.5f;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);g.transform.localScale=new Vector3(.025f,d.magnitude*.5f,.025f);g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
        static void Label(Transform p,string text,Vector3 pos,Material mat){GameObject g=new GameObject("Sign - "+text);g.transform.SetParent(p,false);g.transform.localPosition=pos;TextMesh tm=g.AddComponent<TextMesh>();tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.characterSize=.45f;tm.fontSize=44;tm.color=Color.white;g.transform.localScale=Vector3.one*.35f;}

        static bool NearTown(Vector3 p,List<Town> towns,float extra){foreach(Town t in towns){Vector3 d=p-t.c;d.y=0;if(Mathf.Abs(Vector3.Dot(d,t.r))<t.hw+extra&&Mathf.Abs(Vector3.Dot(d,t.f))<t.hl+extra)return true;}return false;}
        static bool NearRoad(Vector3 p,List<Road> roads,float extra){foreach(Road r in roads)for(int i=0;i<r.p.Count-1;i++)if(SegDist(p,r.p[i],r.p[i+1])<r.w*.5f+extra)return true;return false;}
        static Vector3 TownEdge(Town t,Vector3 dir){dir.y=0;if(dir.sqrMagnitude<.001f)dir=t.f;dir.Normalize();float x=Vector3.Dot(dir,t.r),z=Vector3.Dot(dir,t.f),tx=Mathf.Abs(x)<.001f?float.MaxValue:t.hw/Mathf.Abs(x),tz=Mathf.Abs(z)<.001f?float.MaxValue:t.hl/Mathf.Abs(z),d=Mathf.Min(tx,tz);if(d==float.MaxValue)d=Mathf.Max(t.hw,t.hl);return t.c+dir*d;}
        static List<Transform> Houses(Transform town){List<Transform>a=new List<Transform>();for(int i=0;i<town.childCount;i++){Transform c=town.GetChild(i);if(c.name.StartsWith("Detailed House")||c.name.StartsWith("Building"))a.Add(c);}return a;}
        static Vector3 Average(List<Transform>a){Vector3 c=Vector3.zero;if(a.Count==0)return c;foreach(Transform t in a)c+=t.position;return c/a.Count;}
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t){float u=1-t;return u*u*a+2*u*t*b+t*t*c;}
        static float SegDist(Vector3 p,Vector3 a,Vector3 b){Vector2 q=new Vector2(p.x,p.z),x=new Vector2(a.x,a.z),d=new Vector2(b.x-a.x,b.z-a.z);return d.sqrMagnitude<.001f?Vector2.Distance(q,x):Vector2.Distance(q,x+d*Mathf.Clamp01(Vector2.Dot(q-x,d)/d.sqrMagnitude));}
        static float Dist(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        static float Fbm(float x,float y,int o){float v=0,a=1,sum=0,f=1;for(int i=0;i<o;i++){v+=Mathf.PerlinNoise(x*f,y*f)*a;sum+=a;a*=.5f;f*=2.03f;}return sum>0?v/sum:0;}
        static float Next(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
        static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
        static Vector3 Rand(System.Random r,Terrain t){Vector3 o=t.transform.position,s=t.terrainData.size;Vector3 p=new Vector3(o.x+Next(r,.02f,.98f)*s.x,0,o.z+Next(r,.02f,.98f)*s.z);p.y=Ground(t,p);return p;}
        static Vector3 Clamp(Terrain t,Vector3 p,float margin){Vector3 o=t.transform.position,s=t.terrainData.size;p.x=Mathf.Clamp(p.x,o.x+margin,o.x+s.x-margin);p.z=Mathf.Clamp(p.z,o.z+margin,o.z+s.z-margin);p.y=Ground(t,p);return p;}
        static float MaxTravel(Terrain t,Vector3 origin,Vector3 d,float margin){Vector3 o=t.transform.position,s=t.terrainData.size;float best=float.MaxValue;if(d.x>.0001f)best=Mathf.Min(best,(o.x+s.x-margin-origin.x)/d.x);else if(d.x<-.0001f)best=Mathf.Min(best,(o.x+margin-origin.x)/d.x);if(d.z>.0001f)best=Mathf.Min(best,(o.z+s.z-margin-origin.z)/d.z);else if(d.z<-.0001f)best=Mathf.Min(best,(o.z+margin-origin.z)/d.z);return best==float.MaxValue?Mathf.Min(s.x,s.z)*.35f:best;}

        static Bounds AirportSafetyBounds(GameObject airport)
        {
            Bounds b=BoundsOf(airport);Transform[] all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);for(int i=0;i<all.Length;i++){Transform t=all[i];if(!t||!t.gameObject.scene.IsValid())continue;string n=t.name.ToLowerInvariant();if(!n.Contains("runway")&&!n.Contains("taxiway")&&!n.Contains("apron"))continue;Renderer[] rr=t.GetComponentsInChildren<Renderer>(true);foreach(Renderer r in rr)b.Encapsulate(r.bounds);Collider[] cc=t.GetComponentsInChildren<Collider>(true);foreach(Collider c in cc)b.Encapsulate(c.bounds);}return b;
        }
        static Bounds BoundsOf(GameObject g){bool set=false;Bounds b=new Bounds(g.transform.position,Vector3.zero);foreach(Renderer r in g.GetComponentsInChildren<Renderer>(true)){if(!set){b=r.bounds;set=true;}else b.Encapsulate(r.bounds);}foreach(Collider c in g.GetComponentsInChildren<Collider>(true)){if(!set){b=c.bounds;set=true;}else b.Encapsulate(c.bounds);}return b;}
        static Terrain FindTerrain(){GameObject g=Find(TerrainName);Terrain t=g?(g.GetComponent<Terrain>()??g.GetComponentInChildren<Terrain>(true)):null;if(t)return t;Terrain[] a=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);return a.Length>0?a[0]:null;}
        static GameObject Find(string n){GameObject g=GameObject.Find(n);if(g)return g;foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t&&t.name==n&&t.gameObject.scene.IsValid())return t.gameObject;return null;}
        static Transform FindChild(Transform r,string n){foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;return null;}
        static Transform DirectChild(Transform r,string n){for(int i=0;i<r.childCount;i++)if(r.GetChild(i).name==n)return r.GetChild(i);return null;}
        static Transform New(string n,Transform p){GameObject g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
        static int Count(Transform r,string n){int c=0;foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t!=r&&t.name.Contains(n))c++;return c;}
        static string Safe(string n){char[] bad=System.IO.Path.GetInvalidFileNameChars();foreach(char c in bad)n=n.Replace(c,'_');return n.Replace(' ','_');}
        static void ResetFolder(){if(AssetDatabase.IsValidFolder(Gen))AssetDatabase.DeleteAsset(Gen);Ensure(Gen+"/Materials");Ensure(Gen+"/Meshes");}
        static void Ensure(string path){string[] p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}}
    }
}
