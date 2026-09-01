using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldRegionalInfrastructureFinalizer
    {
        const string WorldName="Hanger 51 Surrounding Countryside";
        const string TerrainName="Hanger 51 Editable Terrain";
        const string RefineName="Hanger 51 Roadside Refinement";
        const string PassName="Hanger 51 Regional Infrastructure Pass";
        const string FinalName="Hanger 51 Regional Infrastructure Finalizer";
        const string BaseGen="Assets/_Project/Environment/Generated/CountrysideLivedInPass";
        const string Gen="Assets/_Project/Environment/Generated/CountrysideRegionalFinalizer";
        static int meshId;

        [MenuItem("Hanger 51/World/Current/106 - Finalize Regional Infrastructure")]
        public static void Build()
        {
            Hanger51WorldRegionalInfrastructurePass.Build();
            GameObject world=Find(WorldName),pass=Find(PassName),refine=Find(RefineName);Terrain terrain=FindTerrain();
            if(!world||!pass||!refine||!terrain){Debug.LogError("Step 106 could not find the Step 104 world.");return;}
            Transform roads=DirectChild(world.transform,"Road Network"),settlements=FindChild(world.transform,"Settlements"),plant=FindChild(pass.transform,"Power Station Complex");
            if(!roads||!settlements||!plant){Debug.LogError("Step 106 could not find roads, towns, or the power station.",world);return;}
            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Preparing final regional pass",.05f);
                GameObject old=Find(FinalName);if(old)UnityEngine.Object.DestroyImmediate(old);ResetFolder();meshId=0;Transform root=New(FinalName,pass.transform);
                Material wood=Load("Weathered_Wood"),metal=Load("Dark_Metal"),white=Load("Warm_White"),green=Load("Farm_Green"),gravel=Load("Matte_Gravel"),concrete=Load("Concrete");

                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Leveling power station site without terrain seams",.22f);
                FlattenPlantSite(terrain,plant.position,122f,102f,42f);

                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Reconforming roads after site grading",.38f);
                ReConformRoads(terrain,roads);

                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Removing old ghost roadside foliage",.50f);
                RemoveNamed(refine.transform,"Dense Roadside Asset Grass");

                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Adding town distribution utilities along streets",.63f);
                int townPoles=AddTownUtilities(terrain,roads,settlements,root,wood,metal,white);

                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Adding industrial verge and service details",.78f);
                int plantDetails=AddPlantGroundDetail(terrain,plant,root,green,gravel,concrete,metal);

                EditorUtility.DisplayProgressBar("Hanger 51 Finalizer","Final road-edge safety check",.92f);
                ClampRoadVerticesToTerrain(terrain,roads,65f);
                ReConformRoads(terrain,roads);

                terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();Selection.activeGameObject=root.gameObject;
                Debug.Log($"Step 106 complete. Power-station terrain graded smoothly, regional roads clamped to land and reconformed, town street utility poles={townPoles}, industrial ground details={plantDetails}. Run Step 107 to validate.",root.gameObject);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        [MenuItem("Hanger 51/World/Current/107 - Validate Final Regional Infrastructure")]
        public static void Validate()
        {
            GameObject world=Find(WorldName),pass=Find(PassName),final=Find(FinalName);Terrain t=FindTerrain();if(!world||!pass||!final||!t){Debug.LogError("Step 107 failed: run Step 106 first.");return;}
            Transform roads=DirectChild(world.transform,"Road Network");Bounds land=TerrainBounds(t);int offLand=0,buried=0,verts=0;
            foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))if(mf&&mf.sharedMesh&&mf.gameObject.name=="Road Surface")foreach(Vector3 v in mf.sharedMesh.vertices){Vector3 w=mf.transform.TransformPoint(v);verts++;if(!Inside(land,w,1f))offLand++;if(w.y<Ground(t,w)+.045f)buried++;}
            int regionals=Count(roads,"Regional Road Marker"),plant=Count(pass.transform,"Power Station Complex"),cooling=Count(pass.transform,"Cooling Tower"),transformers=Count(pass.transform,"Transformer Bank"),regionalPoles=Count(pass.transform,"Regional Utility Pole"),townPoles=Count(final.transform,"Town Distribution Pole"),transmission=Count(pass.transform,"Transmission Structure"),grass=Count(pass.transform,"Regional Asset Grass"),trees=Count(pass.transform,"Shelter Belt Tree"),roadDetails=Count(pass.transform,"Regional Road Detail"),plantDetails=Count(final.transform,"Plant Ground Detail");
            Transform settlements=FindChild(world.transform,"Settlements");int connected=0;if(settlements)for(int i=0;i<settlements.childCount;i++){Vector3 c=TownCenter(settlements.GetChild(i));if(DistanceToRegional(c,roads)<430f)connected++;}
            bool ok=offLand==0&&buried==0&&regionals>=5&&connected>=4&&plant>=1&&cooling>=2&&transformers>=4&&regionalPoles>=30&&townPoles>=25&&transmission>=6&&grass>=800&&trees>=70&&roadDetails>=60&&plantDetails>=30;
            if(ok)Debug.Log($"Step 107 passed. road vertices={verts}, off-land={offLand}, buried={buried}, regional roads={regionals}, towns connected={connected}/4, cooling towers={cooling}, transformer banks={transformers}, regional poles={regionalPoles}, town poles={townPoles}, transmission structures={transmission}, asset grass={grass}, shelter trees={trees}, road details={roadDetails}, plant ground details={plantDetails}.",final);
            else Debug.LogError($"Step 107 failed. road vertices={verts}, off-land={offLand}, buried={buried}, regional roads={regionals}, towns connected={connected}/4, plant={plant}, cooling={cooling}, transformers={transformers}, regional poles={regionalPoles}, town poles={townPoles}, transmission={transmission}, grass={grass}, trees={trees}, road details={roadDetails}, plant ground details={plantDetails}.",final);
        }

        static void FlattenPlantSite(Terrain t,Vector3 center,float halfX,float halfZ,float blend)
        {
            TerrainData d=t.terrainData;int res=d.heightmapResolution;Vector3 o=t.transform.position,s=d.size;float target=(center.y-o.y)/Mathf.Max(.001f,s.y);float outerX=halfX+blend,outerZ=halfZ+blend;
            int x0=Mathf.Clamp(Mathf.FloorToInt((center.x-outerX-o.x)/s.x*(res-1)),0,res-1),x1=Mathf.Clamp(Mathf.CeilToInt((center.x+outerX-o.x)/s.x*(res-1)),0,res-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((center.z-outerZ-o.z)/s.z*(res-1)),0,res-1),z1=Mathf.Clamp(Mathf.CeilToInt((center.z+outerZ-o.z)/s.z*(res-1)),0,res-1);int w=x1-x0+1,h=z1-z0+1;float[,] heights=d.GetHeights(x0,z0,w,h);
            for(int z=0;z<h;z++){float wz=o.z+(z0+z)/(float)(res-1)*s.z;for(int x=0;x<w;x++){float wx=o.x+(x0+x)/(float)(res-1)*s.x;float dx=Mathf.Abs(wx-center.x),dz=Mathf.Abs(wz-center.z);float edge=Mathf.Max(Mathf.InverseLerp(halfX,outerX,dx),Mathf.InverseLerp(halfZ,outerZ,dz));float weight=1f-Mathf.SmoothStep(0,1,edge);heights[z,x]=Mathf.Lerp(heights[z,x],target,weight);}}
            d.SetHeights(x0,z0,heights);
        }

        static void ClampRoadVerticesToTerrain(Terrain t,Transform roads,float margin)
        {
            Bounds b=TerrainBounds(t);foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))if(mf&&mf.sharedMesh&&(mf.gameObject.name=="Road Surface"||mf.gameObject.name=="Gravel Shoulder"||mf.gameObject.name=="Center Line")){Mesh mesh=mf.sharedMesh;Vector3[] v=mesh.vertices;bool changed=false;for(int i=0;i<v.Length;i++){Vector3 w=mf.transform.TransformPoint(v[i]);float nx=Mathf.Clamp(w.x,b.min.x+margin,b.max.x-margin),nz=Mathf.Clamp(w.z,b.min.z+margin,b.max.z-margin);if(Mathf.Abs(nx-w.x)>.001f||Mathf.Abs(nz-w.z)>.001f){w.x=nx;w.z=nz;v[i]=mf.transform.InverseTransformPoint(w);changed=true;}}if(changed){mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);}}
        }

        static void ReConformRoads(Terrain t,Transform roads)
        {
            foreach(MeshFilter mf in roads.GetComponentsInChildren<MeshFilter>(true))if(mf&&mf.sharedMesh&&(mf.gameObject.name=="Road Surface"||mf.gameObject.name=="Gravel Shoulder"||mf.gameObject.name=="Center Line")){float off=mf.gameObject.name=="Center Line"?.15f:mf.gameObject.name=="Road Surface"?.11f:.04f;Mesh mesh=mf.sharedMesh;Vector3[] v=mesh.vertices;for(int i=0;i<v.Length;i++){Vector3 w=mf.transform.TransformPoint(v[i]);w.y=Ground(t,w)+off;v[i]=mf.transform.InverseTransformPoint(w);}mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);MeshCollider mc=mf.GetComponent<MeshCollider>();if(mc){mc.sharedMesh=null;mc.sharedMesh=mesh;}}
        }

        static int AddTownUtilities(Terrain t,Transform roads,Transform settlements,Transform root,Material wood,Material metal,Material white)
        {
            Transform util=New("Town Street Distribution Utilities",root);int made=0;for(int ti=0;ti<settlements.childCount;ti++){Transform town=settlements.GetChild(ti);Transform road=FindTownRoad(roads,town.name);if(!road)continue;List<Vector3> path=RoadPath(road);if(path.Count<2)continue;path=Resample(path,45f);Vector3[] last=new Vector3[3];bool have=false;for(int i=0;i<path.Count;i++){Vector3 tan=Tangent(path,i),side=Vector3.Cross(Vector3.up,tan).normalized;Vector3 p=path[i]+side*8.5f;p.y=Ground(t,p);Transform pole=New("Town Distribution Pole "+(++made).ToString("000"),util);pole.position=p;pole.rotation=Quaternion.LookRotation(tan,Vector3.up);Cylinder(pole,"Pole",new Vector3(0,4.6f,0),new Vector3(.16f,4.6f,.16f),wood,false);Box(pole,"Crossarm",new Vector3(0,8.45f,0),new Vector3(2.5f,.16f,.16f),wood,false);for(int k=-1;k<=1;k++){Cylinder(pole,"Insulator",new Vector3(k*.8f,8.68f,0),new Vector3(.07f,.20f,.07f),white,false);Vector3 now=pole.TransformPoint(new Vector3(k*.8f,8.92f,0));if(have)Wire(util,last[k+1],now,metal,"Town Distribution Wire");last[k+1]=now;}have=true;}}
            return made;
        }

        static int AddPlantGroundDetail(Terrain t,Transform plant,Transform root,Material green,Material gravel,Material concrete,Material metal)
        {
            Transform d=New("Power Station Exterior Detail",root);d.position=plant.position;d.rotation=plant.rotation;int made=0;for(int i=0;i<16;i++){float x=-92+i%8*26,z=i<8?92:-92;Transform g=New("Plant Ground Detail Bollard "+(++made).ToString("000"),d);g.localPosition=new Vector3(x,0,z);g.position=new Vector3(g.position.x,Ground(t,g.position),g.position.z);Cylinder(g,"Bollard",new Vector3(0,.55f,0),new Vector3(.18f,.55f,.18f),metal,false);}for(int i=0;i<12;i++){float a=i/12f*Mathf.PI*2;Vector3 lp=new Vector3(Mathf.Cos(a)*112,0,Mathf.Sin(a)*92);Transform g=New("Plant Ground Detail Shrub Island "+(++made).ToString("000"),d);g.localPosition=lp;g.position=new Vector3(g.position.x,Ground(t,g.position),g.position.z);Box(g,"Gravel Island",new Vector3(0,.03f,0),new Vector3(5,.06f,5),gravel,false);GameObject s=GameObject.CreatePrimitive(PrimitiveType.Sphere);s.name="Industrial Shrub";s.transform.SetParent(g,false);s.transform.localPosition=new Vector3(0,.65f,0);s.transform.localScale=new Vector3(2.4f,1.3f,2.0f);s.GetComponent<Renderer>().sharedMaterial=green;UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());}for(int i=0;i<8;i++){Transform pad=New("Plant Ground Detail Equipment Pad "+(++made).ToString("000"),d);pad.localPosition=new Vector3(-86+i*22,0,72);pad.position=new Vector3(pad.position.x,Ground(t,pad.position),pad.position.z);Box(pad,"Equipment Pad",new Vector3(0,.08f,0),new Vector3(8,.16f,6),concrete,false);Box(pad,"Cabinet",new Vector3(0,1.1f,0),new Vector3(3.2f,2.2f,1.8f),metal,false);}return made;
        }

        static void RemoveNamed(Transform root,string name){List<GameObject> kill=new List<GameObject>();foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)kill.Add(t.gameObject);foreach(GameObject g in kill)if(g)UnityEngine.Object.DestroyImmediate(g);}
        static Transform FindTownRoad(Transform roads,string town){Transform fallback=null;for(int i=0;i<roads.childCount;i++){Transform r=roads.GetChild(i);if(!r.name.StartsWith(town))continue;if(!fallback)fallback=r;if(r.name.Contains("Avenue 2")||r.name.Contains("Street 2"))return r;}return fallback;}
        static List<Vector3> RoadPath(Transform road){Transform s=FindChild(road,"Road Surface");if(!s)return new List<Vector3>();MeshFilter mf=s.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return new List<Vector3>();Vector3[] v=mf.sharedMesh.vertices;List<Vector3> p=new List<Vector3>();for(int i=0;i+1<v.Length;i+=2)p.Add((s.TransformPoint(v[i])+s.TransformPoint(v[i+1]))*.5f);return p;}
        static List<Vector3> Resample(List<Vector3> p,float step){List<Vector3> o=new List<Vector3>();if(p.Count==0)return o;o.Add(p[0]);for(int i=0;i<p.Count-1;i++){float d=Planar(p[i],p[i+1]);int n=Mathf.Max(1,Mathf.CeilToInt(d/step));for(int k=1;k<=n;k++)o.Add(Vector3.Lerp(p[i],p[i+1],k/(float)n));}return o;}
        static Vector3 Tangent(List<Vector3> p,int i){Vector3 d=i==0?p[1]-p[0]:i==p.Count-1?p[p.Count-1]-p[p.Count-2]:p[i+1]-p[i-1];d.y=0;return d.sqrMagnitude<.001f?Vector3.forward:d.normalized;}

        static float DistanceToRegional(Vector3 p,Transform roads){float b=float.MaxValue;foreach(Transform r in roads.GetComponentsInChildren<Transform>(true))if(r.name.StartsWith("Regional County Route")||r.name.StartsWith("Regional Industrial Access")){List<Vector3> path=RoadPath(r);for(int i=0;i<path.Count-1;i++)b=Mathf.Min(b,SegDist(p,path[i],path[i+1]));}return b==float.MaxValue?99999:b;}
        static Vector3 TownCenter(Transform town){List<Transform> h=new List<Transform>();for(int i=0;i<town.childCount;i++)if(town.GetChild(i).name.StartsWith("Detailed House")||town.GetChild(i).name.StartsWith("Building"))h.Add(town.GetChild(i));if(h.Count==0)return town.position;Vector3 c=Vector3.zero;foreach(Transform x in h)c+=x.position;return c/h.Count;}

        static Material Load(string n)=>AssetDatabase.LoadAssetAtPath<Material>(BaseGen+"/Materials/"+n+".mat");
        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static GameObject Cylinder(Transform p,string n,Vector3 pos,Vector3 scale,Material m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;if(m)g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static void Wire(Transform p,Vector3 a,Vector3 b,Material m,string n){Vector3 d=b-a;if(d.sqrMagnitude<.01f)return;GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.position=(a+b)*.5f;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);g.transform.localScale=new Vector3(.022f,d.magnitude*.5f,.022f);if(m)g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}

        static Bounds TerrainBounds(Terrain t){Vector3 o=t.transform.position,s=t.terrainData.size;return new Bounds(o+new Vector3(s.x*.5f,s.y*.5f,s.z*.5f),s);}
        static bool Inside(Bounds b,Vector3 p,float margin){return p.x>=b.min.x+margin&&p.x<=b.max.x-margin&&p.z>=b.min.z+margin&&p.z<=b.max.z-margin;}
        static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
        static float Planar(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        static float SegDist(Vector3 p,Vector3 a,Vector3 b){Vector2 q=new Vector2(p.x,p.z),x=new Vector2(a.x,a.z),d=new Vector2(b.x-a.x,b.z-a.z);return d.sqrMagnitude<.001f?Vector2.Distance(q,x):Vector2.Distance(q,x+d*Mathf.Clamp01(Vector2.Dot(q-x,d)/d.sqrMagnitude));}
        static Terrain FindTerrain(){GameObject g=Find(TerrainName);Terrain t=g?(g.GetComponent<Terrain>()??g.GetComponentInChildren<Terrain>(true)):null;if(t)return t;Terrain[] a=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);return a.Length>0?a[0]:null;}
        static GameObject Find(string n){GameObject g=GameObject.Find(n);if(g)return g;foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t&&t.name==n&&t.gameObject.scene.IsValid())return t.gameObject;return null;}
        static Transform FindChild(Transform r,string n){foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;return null;}
        static Transform DirectChild(Transform r,string n){for(int i=0;i<r.childCount;i++)if(r.GetChild(i).name==n)return r.GetChild(i);return null;}
        static Transform New(string n,Transform p){GameObject g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
        static int Count(Transform r,string n){if(!r)return 0;int c=0;foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t!=r&&t.name.Contains(n))c++;return c;}
        static string Safe(string n){char[] bad=System.IO.Path.GetInvalidFileNameChars();foreach(char c in bad)n=n.Replace(c,'_');return n.Replace(' ','_');}
        static void ResetFolder(){if(AssetDatabase.IsValidFolder(Gen))AssetDatabase.DeleteAsset(Gen);Ensure(Gen+"/Meshes");}
        static void Ensure(string path){string[] p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}}
    }
}
