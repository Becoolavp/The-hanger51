using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51WorldCountrysideVisualUpgrade
    {
        const string Root="Hanger 51 Surrounding Countryside";
        const string Airport="Hanger 51 Airport Complex";
        const string TerrainName="Hanger 51 Editable Terrain";
        const string Source="Assets/_Project/Environment/Terrain/Hanger51WorldTerrain.asset";
        const string Gen="Assets/_Project/Environment/Generated/CountrysideVisualUpgrade";
        const int Seed=51098;
        class Road { public List<Vector3> p; public float w; public bool major; public string n; }
        struct Mat { public Material m; public static implicit operator Material(Mat v)=>v.m; }

        [MenuItem("Hanger 51/World/Current/98 - Upgrade Countryside Visuals")]
        public static void Build()
        {
            Hanger51WorldCountrysideSetup.Build();
            Terrain t=FindTerrain(); GameObject root=Find(Root), airport=Find(Airport);
            if(!t||!root||!airport){Debug.LogError("Step 98 could not find the Step 96 world, terrain, or airport.");return;}
            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Creating new textured environment",.05f);
                ResetFolder(); TerrainData source=AssetDatabase.LoadAssetAtPath<TerrainData>(Source);
                if(source==null){Debug.LogError("Step 98 could not load the clean source terrain.");return;}
                Bounds ab=BoundsOf(airport); Vector3 ac=ab.center; ac.y=Ground(t,ac);
                float clear=Mathf.Max(800,Mathf.Max(ab.extents.x,ab.extents.z)+450);

                Texture2D meadow=Tex("Meadow",new Color(.13f,.25f,.07f),new Color(.31f,.43f,.16f),0);
                Texture2D field=Tex("Field",new Color(.20f,.23f,.08f),new Color(.43f,.39f,.18f),1);
                Texture2D gravel=Tex("Gravel",new Color(.28f,.27f,.23f),new Color(.60f,.56f,.46f),2);
                Texture2D stone=Tex("Stone",new Color(.24f,.25f,.23f),new Color(.51f,.48f,.41f),3);
                Texture2D asphalt=Tex("Asphalt",new Color(.07f,.075f,.08f),new Color(.18f,.18f,.18f),4);
                Texture2D siding=Tex("Siding",new Color(.62f,.61f,.55f),new Color(.92f,.89f,.78f),5);
                Texture2D shingles=Tex("Shingles",new Color(.10f,.08f,.07f),new Color(.28f,.22f,.18f),6);
                Texture2D wood=Tex("Wood",new Color(.19f,.08f,.03f),new Color(.48f,.25f,.09f),7);
                t.terrainData.terrainLayers=new[]{Layer("New Meadow",meadow,15),Layer("New Field",field,22),Layer("Road Gravel",gravel,7),Layer("Natural Stone",stone,12)};
                Mat asphaltM=MakeMat("Asphalt",asphalt,Color.white,.05f);
                Mat lineM=MakeMat("Road Line",Tex("RoadLine",new Color(.68f,.55f,.08f),new Color(.98f,.83f,.22f),9),Color.white,.08f);
                Mat gravelM=MakeMat("Gravel",gravel,Color.white,.02f);
                Mat rockM=MakeMat("Boulder",stone,Color.white,.02f);
                Mat roofM=MakeMat("Shingles",shingles,Color.white,.03f);
                Mat woodM=MakeMat("Wood",wood,Color.white,.04f);
                Mat trimM=MakeMat("Trim",siding,new Color(.92f,.90f,.83f),.12f);
                Mat glassM=MakeMat("Glass",Tex("Glass",new Color(.08f,.14f,.17f),new Color(.25f,.38f,.42f),8),Color.white,.65f);
                Mat foundationM=MakeMat("Foundation",stone,new Color(.75f,.72f,.67f),.02f);
                Mat[] walls={MakeMat("Wall Cream",siding,new Color(.86f,.79f,.66f),.04f),MakeMat("Wall Gray",siding,new Color(.69f,.70f,.67f),.04f),MakeMat("Wall Blue",siding,new Color(.52f,.62f,.64f),.04f),MakeMat("Wall Red",siding,new Color(.60f,.38f,.29f),.04f)};

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Replacing blob terrain with rolling country",.18f);
                Shape(t,source,ac,clear);
                Transform settlements=FindChild(root.transform,"Settlements");
                if(!settlements){Debug.LogError("Step 98 could not find generated settlements.");return;}
                List<Transform> towns=new List<Transform>(); for(int i=0;i<settlements.childCount;i++)towns.Add(settlements.GetChild(i));
                MoveAndArrange(t,towns,ac,clear,walls,roofM,woodM,trimM,glassM,foundationM);

                Transform oldRoads=DirectChild(root.transform,"Road Network"); if(oldRoads)Undo.DestroyObjectImmediate(oldRoads.gameObject);
                Transform roadRoot=New("Road Network",root.transform);
                List<Road> roads=RoadPlan(t,roadRoot,towns,ac,clear,asphaltM,gravelM,lineM);
                Paint(t,roads);

                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Adding dense woods and meadow grass",.62f);
                Trees(t,source,ac,clear,towns,roads); Grass(t,ac,clear,towns,roads);
                Transform legacyRocks=DirectChild(root.transform,"Rocks"); if(legacyRocks)Undo.DestroyObjectImmediate(legacyRocks.gameObject);
                Transform fallback=DirectChild(root.transform,"Fallback Vegetation"); if(fallback)Undo.DestroyObjectImmediate(fallback.gameObject);
                Transform natural=DirectChild(root.transform,"Natural Features"); if(!natural)natural=New("Natural Features",root.transform);
                Transform oldRocks=DirectChild(natural,"Rocks"); if(oldRocks)Undo.DestroyObjectImmediate(oldRocks.gameObject);
                EditorUtility.DisplayProgressBar("Hanger 51 Countryside","Adding real collider rocks",.82f);
                Rocks(t,New("Rocks",natural),ac,clear,towns,roads,rockM);

                t.Flush(); EditorUtility.SetDirty(t.terrainData); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes(); Selection.activeGameObject=root;
                Debug.Log($"Step 98 complete. Replaced old grass/dirt terrain, upgraded roads/buildings, moved settlements farther away, added {t.terrainData.treeInstanceCount} trees and collider boulders.",root);
            }
            finally{EditorUtility.ClearProgressBar();}
        }

        [MenuItem("Hanger 51/World/Current/99 - Validate Countryside Visuals")]
        public static void Validate()
        {
            GameObject root=Find(Root); Terrain t=FindTerrain(); if(!root||!t){Debug.LogError("Step 99 failed: run Step 98.");return;}
            Transform rocks=FindChild(root.transform,"Rocks"); int rc=rocks?rocks.childCount:0,bad=0;
            if(rocks)for(int i=0;i<rocks.childCount;i++){GameObject r=rocks.GetChild(i).gameObject;Renderer rr=r.GetComponent<Renderer>();if(!r.GetComponent<MeshCollider>()||!r.GetComponent<MeshFilter>()||!rr||rr.sharedMaterial==null||rr.sharedMaterial.mainTexture==null)bad++;}
            int roads=Count(root.transform,"Road Surface"),build=Count(root.transform,"Detailed House"); bool layers=t.terrainData.terrainLayers.Length>=4;
            bool ok=rc>=180&&bad==0&&roads>=8&&build>=70&&layers&&t.terrainData.treeInstanceCount>=2500&&t.terrainData.detailPrototypes.Length>0;
            if(ok)Debug.Log($"Step 99 passed. buildings={build}, roads={roads}, trees={t.terrainData.treeInstanceCount}, rocks={rc}; all rocks have colliders.",root);
            else Debug.LogError($"Step 99 failed. buildings={build}, roads={roads}, trees={t.terrainData.treeInstanceCount}, rocks={rc}, bad rocks={bad}, terrain layers={layers}.",root);
        }

        static void ResetFolder(){if(AssetDatabase.IsValidFolder(Gen))AssetDatabase.DeleteAsset(Gen);Ensure(Gen+"/Textures");Ensure(Gen+"/Materials");Ensure(Gen+"/Layers");Ensure(Gen+"/Meshes");}
        static Texture2D Tex(string name,Color a,Color b,int kind)
        {
            const int s=256; Texture2D t=new Texture2D(s,s,TextureFormat.RGBA32,true){name="H51_"+name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4}; Color[] p=new Color[s*s];
            for(int y=0;y<s;y++)for(int x=0;x<s;x++){float u=x/(float)s,v=y/(float)s,n=TileFbm(u,v,kind);Color c=Color.Lerp(a,b,n),hcol=c;float h=Hash(x,y,kind+1);if(kind==2||kind==3){if(h>.93f)hcol*=1.25f;if(h<.07f)hcol*=.67f;}if(kind==4&&h>.97f)hcol*=1.4f;if(kind==5&&Mathf.Repeat(v*22,1)<.07f)hcol*=.72f;if(kind==6&&(Mathf.Repeat(v*18,1)<.06f||Mathf.Repeat(u*12+(y/14%2)*.5f,1)<.04f))hcol*=.63f;if(kind==7)hcol=Color.Lerp(a,b,.35f+.45f*Mathf.Abs(Mathf.Sin((u*34+n)*6.283f)));p[y*s+x]=hcol;}
            t.SetPixels(p);t.Apply(true,false);AssetDatabase.CreateAsset(t,Gen+"/Textures/"+name+".asset");return t;
        }
        static TerrainLayer Layer(string name,Texture2D tex,float tile){TerrainLayer l=ScriptableObject.CreateInstance<TerrainLayer>();l.name=name;l.diffuseTexture=tex;l.tileSize=new Vector2(tile,tile);AssetDatabase.CreateAsset(l,Gen+"/Layers/"+name.Replace(" ","_")+".terrainlayer");return l;}
        static Mat MakeMat(string name,Texture2D tex,Color tint,float smooth){Shader s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");Material m=new Material(s){name="H51_"+name,color=tint,mainTexture=tex};if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",tex);if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",tint);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;AssetDatabase.CreateAsset(m,Gen+"/Materials/"+name.Replace(" ","_")+".mat");return new Mat{m=m};}

        static void Shape(Terrain t,TerrainData src,Vector3 ac,float clear)
        {
            TerrainData d=t.terrainData;int n=d.heightmapResolution;if(src.heightmapResolution!=n)return;float[,] h=src.GetHeights(0,0,n,n);Vector3 o=t.transform.position,s=d.size;
            for(int z=0;z<n;z++){float wz=o.z+z/(float)(n-1)*s.z;for(int x=0;x<n;x++){float wx=o.x+x/(float)(n-1)*s.x,dist=Dist(new Vector3(wx,0,wz),ac),fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(clear,clear+1100,dist));float broad=Fbm(wx*.00020f,wz*.00020f,4)*2-1,mid=Fbm(wx*.00065f+20,wz*.00065f+50,3)*2-1,fine=Fbm(wx*.0017f+80,wz*.0017f+15,2)*2-1;float meters=Mathf.Clamp(broad*9+mid*4.5f+fine*1.5f,-13,15);h[z,x]=Mathf.Clamp01(h[z,x]+meters/Mathf.Max(1,s.y)*fade);}}d.SetHeights(0,0,h);
        }

        static void MoveAndArrange(Terrain t,List<Transform> towns,Vector3 ac,float clear,Mat[] walls,Mat roof,Mat wood,Mat trim,Mat glass,Mat foundation)
        {
            Mesh roofMesh=RoofMesh();System.Random rng=new System.Random(Seed+9);
            for(int k=0;k<towns.Count;k++)
            {
                Transform town=towns[k];List<Transform>b=new List<Transform>();for(int i=0;i<town.childCount;i++)if(town.GetChild(i).name.StartsWith("Building")||town.GetChild(i).name.StartsWith("Detailed House"))b.Add(town.GetChild(i));
                Vector3 center=Vector3.zero;foreach(Transform x in b)center+=x.position;if(b.Count>0)center/=b.Count;Vector3 dir=center-ac;dir.y=0;if(dir.sqrMagnitude<1)dir=new Vector3(k%2==0?1:-1,0,k<2?1:-1);dir.Normalize();
                float wanted=clear+(k==0?2600:1950+k*180);Vector3 target=ClampToTerrain(t,ac+dir*wanted,k==0?520:340);Vector3 to=target-center;to.y=0;town.position+=to;center+=to;
                float ang=(k*37+22)*Mathf.Deg2Rad;Vector3 f=new Vector3(Mathf.Sin(ang),0,Mathf.Cos(ang)),r=Vector3.Cross(Vector3.up,f);int streets=k==0?5:3;float spacing=k==0?78:68,len=k==0?300:165;
                for(int i=0;i<b.Count;i++){int street=i%streets,row=i/streets,side=(row%2==0)?-1:1,alongIndex=row/2;float off=(street-(streets-1)*.5f)*spacing,along=-len+25+(alongIndex*36)%(int)(len*2-50);Vector3 p=center+r*off+f*along+r*side*19;p.y=Ground(t,p);Transform br=b[i];br.position=p;br.rotation=Quaternion.LookRotation(-r*side,Vector3.up);while(br.childCount>0)Undo.DestroyObjectImmediate(br.GetChild(0).gameObject);House(br,walls[rng.Next(walls.Length)],roof,wood,trim,glass,foundation,roofMesh,rng);br.name=$"Detailed House {k+1}-{i+1:000}";}
            }
        }
        static void House(Transform p,Mat wall,Mat roof,Mat wood,Mat trim,Mat glass,Mat foundation,Mesh roofMesh,System.Random rng)
        {
            float w=Next(rng,10,16),d=Next(rng,9,15),h=Next(rng,5.2f,7.2f);Box(p,"Foundation",new Vector3(0,.35f,0),new Vector3(w+.3f,.7f,d+.3f),foundation,true);Box(p,"Textured Siding",new Vector3(0,h*.5f+.7f,0),new Vector3(w,h,d),wall,true);
            GameObject q=new GameObject("Pitched Shingle Roof");q.transform.SetParent(p,false);q.transform.localPosition=new Vector3(0,h+.7f,0);q.transform.localScale=new Vector3(w*1.12f,Next(rng,2.3f,3.6f),d*1.12f);q.AddComponent<MeshFilter>().sharedMesh=roofMesh;q.AddComponent<MeshRenderer>().sharedMaterial=roof;
            float z=d*.5f+.06f;Box(p,"Door Trim",new Vector3(0,2.15f,z),new Vector3(2.1f,3.7f,.12f),trim,false);Box(p,"Wood Door",new Vector3(0,2.1f,z+.08f),new Vector3(1.6f,3.2f,.10f),wood,false);Window(p,new Vector3(-w*.28f,3.2f,z+.09f),glass,trim);Window(p,new Vector3(w*.28f,3.2f,z+.09f),glass,trim);if(Next(rng,0,1)<.4f)Box(p,"Chimney",new Vector3(w*.25f,h+2,-d*.12f),new Vector3(.85f,2.6f,.85f),foundation,false);
        }
        static void Window(Transform p,Vector3 x,Mat glass,Mat trim){Box(p,"Window Trim",x,new Vector3(2.25f,2.2f,.12f),trim,false);Box(p,"Window Glass",x+Vector3.forward*.075f,new Vector3(1.82f,1.78f,.09f),glass,false);}
        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 scale,Mat m,bool col){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;if(!col)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;}
        static Mesh RoofMesh(){Mesh m=new Mesh{name="H51_GableRoof"};m.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,.5f)};m.triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,2,5,4,2,4,1,0,1,4,0,4,3};m.uv=new[]{Vector2.zero,Vector2.right,new Vector2(.5f,1),Vector2.zero,Vector2.right,new Vector2(.5f,1)};m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Gen+"/Meshes/GableRoof.asset");return m;}

        static List<Road> RoadPlan(Terrain t,Transform root,List<Transform> towns,Vector3 ac,float clear,Mat asphalt,Mat gravel,Mat line)
        {
            List<Road> roads=new List<Road>();List<Vector3> c=new List<Vector3>();foreach(Transform x in towns)c.Add(Center(x));if(c.Count==0)return roads;
            Vector3 exit=ac+(c[0]-ac).normalized*(clear+100);AddRoad(t,root,roads,"Airport Highway",exit,c[0],8,180,true,asphalt,gravel,line);
            for(int i=1;i<c.Count;i++)AddRoad(t,root,roads,"County Road "+i,c[0],c[i],7,(i%2==0?150:-150),true,asphalt,gravel,line);
            for(int k=0;k<towns.Count;k++){Vector3 f=Quaternion.Euler(0,k*37+22,0)*Vector3.forward,r=Vector3.Cross(Vector3.up,f);int sc=k==0?5:3;float sp=k==0?78:68,len=k==0?320:185;for(int i=0;i<sc;i++){float off=(i-(sc-1)*.5f)*sp;AddRoad(t,root,roads,towns[k].name+" Main "+i,c[k]+r*off-f*len,c[k]+r*off+f*len,5.5f,0,i==sc/2,asphalt,gravel,line);}for(int i=-1;i<=1;i++)AddRoad(t,root,roads,towns[k].name+" Cross "+i,c[k]+f*i*90-r*(k==0?190:110),c[k]+f*i*90+r*(k==0?190:110),4.8f,0,false,asphalt,gravel,line);}
            return roads;
        }
        static void AddRoad(Terrain t,Transform root,List<Road> roads,string name,Vector3 a,Vector3 b,float w,float bend,bool major,Mat asphalt,Mat gravel,Mat line)
        {
            Vector3 d=b-a;d.y=0;float len=d.magnitude;if(len<1)return;Vector3 f=d.normalized,r=Vector3.Cross(Vector3.up,f);int n=Mathf.Clamp(Mathf.CeilToInt(len/28),5,150);List<Vector3> p=new List<Vector3>();
            for(int i=0;i<=n;i++){float u=i/(float)n;Vector3 x=Vector3.Lerp(a,b,u)+r*Mathf.Sin(u*Mathf.PI)*bend;x.y=Ground(t,x)+.06f;p.Add(x);}Road road=new Road{p=p,w=w,major=major,n=name};roads.Add(road);Transform rr=New(name,root);Ribbon(rr,"Gravel Shoulder",p,w+(major?8:4),gravel,0,false);Ribbon(rr,"Road Surface",p,w,asphalt,.055f,true);Ribbon(rr,"Center Line",p,major?.18f:.11f,line,.074f,false);
        }
        static void Ribbon(Transform parent,string n,List<Vector3> p,float w,Mat mat,float y,bool collider)
        {
            int c=p.Count;Vector3[] v=new Vector3[c*2];Vector2[] uv=new Vector2[c*2];int[] tr=new int[(c-1)*6];float dist=0;
            for(int i=0;i<c;i++){Vector3 f=i==0?p[1]-p[0]:i==c-1?p[c-1]-p[c-2]:p[i+1]-p[i-1];f.y=0;f.Normalize();Vector3 r=Vector3.Cross(Vector3.up,f)*w*.5f;if(i>0)dist+=Dist(p[i-1],p[i]);v[i*2]=p[i]-r+Vector3.up*y;v[i*2+1]=p[i]+r+Vector3.up*y;uv[i*2]=new Vector2(0,dist/8);uv[i*2+1]=new Vector2(1,dist/8);if(i<c-1){int q=i*6,j=i*2;tr[q]=j;tr[q+1]=j+2;tr[q+2]=j+1;tr[q+3]=j+1;tr[q+4]=j+2;tr[q+5]=j+3;}}
            Mesh m=new Mesh{name="H51_"+n+"_"+Guid.NewGuid().ToString("N").Substring(0,8)};m.vertices=v;m.uv=uv;m.triangles=tr;m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Gen+"/Meshes/"+m.name+".asset");GameObject g=new GameObject(n);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=m;g.isStatic=true;
        }

        static void Paint(Terrain t,List<Road> roads)
        {
            TerrainData d=t.terrainData;int W=d.alphamapWidth,H=d.alphamapHeight;float[,,] a=new float[H,W,4];Vector3 o=t.transform.position,s=d.size;
            for(int z=0;z<H;z++)for(int x=0;x<W;x++){float nx=x/(float)(W-1),nz=z/(float)(H-1),wx=o.x+nx*s.x,wz=o.z+nz*s.z,field=Fbm(wx*.0006f,wz*.0006f,3)*.34f,rock=Mathf.InverseLerp(22,38,d.GetSteepness(nx,nz))*.65f;a[z,x,3]=rock;a[z,x,1]=field*(1-rock);a[z,x,0]=1-a[z,x,1]-rock;}
            foreach(Road road in roads)for(int q=0;q<road.p.Count;q++){Vector3 p=road.p[q];int cx=Mathf.RoundToInt((p.x-o.x)/s.x*(W-1)),cz=Mathf.RoundToInt((p.z-o.z)/s.z*(H-1)),rx=Mathf.Max(1,Mathf.CeilToInt((road.w*.5f+9)/s.x*W)),rz=Mathf.Max(1,Mathf.CeilToInt((road.w*.5f+9)/s.z*H));for(int z=Mathf.Max(0,cz-rz);z<=Mathf.Min(H-1,cz+rz);z++)for(int x=Mathf.Max(0,cx-rx);x<=Mathf.Min(W-1,cx+rx);x++){float dd=Mathf.Sqrt(Mathf.Pow((x-cx)/(float)rx,2)+Mathf.Pow((z-cz)/(float)rz,2));if(dd>1)continue;float g=(1-dd)*.9f;a[z,x,2]=Mathf.Max(a[z,x,2],g);float rem=1-a[z,x,2],sum=a[z,x,0]+a[z,x,1]+a[z,x,3];if(sum>0){a[z,x,0]*=rem/sum;a[z,x,1]*=rem/sum;a[z,x,3]*=rem/sum;}}}d.SetAlphamaps(0,0,a);
        }

        static void Trees(Terrain t,TerrainData src,Vector3 ac,float clear,List<Transform> towns,List<Road> roads)
        {
            TerrainData d=t.terrainData;if(src.treePrototypes==null||src.treePrototypes.Length==0){Debug.LogWarning("Step 98 found no terrain tree prototypes to reuse.");return;}d.treePrototypes=src.treePrototypes;List<TreeInstance>a=new List<TreeInstance>();System.Random rng=new System.Random(Seed+51);Vector3 o=t.transform.position,s=d.size;int target=6000,tries=0;
            while(a.Count<target&&tries++<90000){float nx=Next(rng,.015f,.985f),nz=Next(rng,.015f,.985f);Vector3 p=new Vector3(o.x+nx*s.x,0,o.z+nz*s.z);if(!Open(p,ac,clear,towns,roads,18))continue;float forest=Fbm(p.x*.00075f+30,p.z*.00075f+60,4);if(forest<.45f||Next(rng,0,1)>Mathf.Clamp01((forest-.42f)*2.2f))continue;float scale=Next(rng,.22f,.52f);a.Add(new TreeInstance{position=new Vector3(nx,d.GetInterpolatedHeight(nx,nz)/Mathf.Max(1,s.y),nz),prototypeIndex=rng.Next(d.treePrototypes.Length),widthScale=scale*Next(rng,.9f,1.12f),heightScale=scale,rotation=Next(rng,0,6.283f),color=Color.white,lightmapColor=Color.white});}d.treeInstances=a.ToArray();
        }
        static void Grass(Terrain t,Vector3 ac,float clear,List<Transform> towns,List<Road> roads)
        {
            Texture2D tex=new Texture2D(64,64,TextureFormat.RGBA32,true);Color[] p=new Color[4096];for(int i=0;i<p.Length;i++)p[i]=Color.clear;System.Random rng=new System.Random(991);for(int b=0;b<24;b++){int x=rng.Next(5,59),top=rng.Next(30,62);for(int y=1;y<top;y++){int xx=Mathf.Clamp(x+(int)((y/(float)top-.5f)*rng.Next(-8,9)),0,63);p[y*64+xx]=new Color(.25f,.48f,.10f,1);}}tex.SetPixels(p);tex.Apply(true,false);tex.name="H51 Meadow Grass";AssetDatabase.CreateAsset(tex,Gen+"/Textures/MeadowGrass.asset");
            TerrainData d=t.terrainData;d.SetDetailResolution(512,16);d.detailPrototypes=new[]{new DetailPrototype{prototypeTexture=tex,renderMode=DetailRenderMode.GrassBillboard,minWidth=.55f,maxWidth=1.1f,minHeight=.55f,maxHeight=1.3f,noiseSpread=.2f,healthyColor=Color.white,dryColor=new Color(.8f,.7f,.45f)}};int W=d.detailWidth,H=d.detailHeight;int[,] map=new int[H,W];Vector3 o=t.transform.position,s=d.size;for(int z=0;z<H;z++)for(int x=0;x<W;x++){Vector3 q=new Vector3(o.x+x/(float)(W-1)*s.x,0,o.z+z/(float)(H-1)*s.z);if(Dist(q,ac)<clear||NearTown(q,towns,30)||NearRoad(q,roads,7))continue;float n=Fbm(q.x*.002f,q.z*.002f,3);if(n>.32f)map[z,x]=Mathf.RoundToInt(Mathf.Lerp(1,6,n));}d.SetDetailLayer(0,0,0,map);
        }

        static void Rocks(Terrain t,Transform root,Vector3 ac,float clear,List<Transform> towns,List<Road> roads,Mat mat)
        {
            Mesh[] mesh=new Mesh[5];for(int i=0;i<5;i++)mesh[i]=Boulder(i);System.Random rng=new System.Random(Seed+88);int made=0,tries=0;Vector3 o=t.transform.position,s=t.terrainData.size;
            while(made<240&&tries++<12000){Vector3 p=new Vector3(o.x+Next(rng,.02f,.98f)*s.x,0,o.z+Next(rng,.02f,.98f)*s.z);if(!Open(p,ac,clear,towns,roads,12))continue;p.y=Ground(t,p);GameObject g=new GameObject($"Rock {++made:000}");g.transform.SetParent(root,false);g.transform.position=p-Vector3.up*Next(rng,.05f,.25f);g.transform.rotation=Quaternion.Euler(Next(rng,-15,15),Next(rng,0,360),Next(rng,-15,15));g.transform.localScale=new Vector3(Next(rng,1.6f,5.5f),Next(rng,1,3.1f),Next(rng,1.5f,5));Mesh m=mesh[rng.Next(mesh.Length)];g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mat;g.AddComponent<MeshCollider>().sharedMesh=m;g.isStatic=true;}
        }
        static Mesh Boulder(int v)
        {
            int seg=10,rings=4;List<Vector3> p=new List<Vector3>{new Vector3(0,.62f,0)};List<int> tr=new List<int>();for(int r=1;r<=rings;r++){float tt=r/(float)(rings+1),phi=tt*Mathf.PI,rad=Mathf.Sin(phi)*.58f,y=Mathf.Cos(phi)*.52f;for(int s=0;s<seg;s++){float a=s/(float)seg*6.283f,j=.78f+Hash(s,r,v+20)*.38f;p.Add(new Vector3(Mathf.Cos(a)*rad*j,y+(Hash(s,r,v+90)-.5f)*.12f,Mathf.Sin(a)*rad*j));}}int bottom=p.Count;p.Add(new Vector3(0,-.52f,0));for(int s=0;s<seg;s++){int n=(s+1)%seg;tr.Add(0);tr.Add(1+n);tr.Add(1+s);}for(int r=0;r<rings-1;r++){int a=1+r*seg,b=a+seg;for(int s=0;s<seg;s++){int n=(s+1)%seg;tr.Add(a+s);tr.Add(b+n);tr.Add(b+s);tr.Add(a+s);tr.Add(a+n);tr.Add(b+n);}}int last=1+(rings-1)*seg;for(int s=0;s<seg;s++){int n=(s+1)%seg;tr.Add(last+s);tr.Add(last+n);tr.Add(bottom);}Mesh m=new Mesh{name="H51_Boulder_"+v};m.SetVertices(p);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Gen+"/Meshes/Boulder_"+v+".asset");return m;
        }

        static bool Open(Vector3 p,Vector3 ac,float clear,List<Transform> towns,List<Road> roads,float road){return Dist(p,ac)>clear&&!NearTown(p,towns,45)&&!NearRoad(p,roads,road);}
        static bool NearTown(Vector3 p,List<Transform> towns,float extra){foreach(Transform t in towns)if(Dist(p,Center(t))<(t.name.Contains("Town")?390:235)+extra)return true;return false;}
        static bool NearRoad(Vector3 p,List<Road> roads,float extra){foreach(Road r in roads)for(int i=0;i<r.p.Count-1;i++)if(Seg(p,r.p[i],r.p[i+1])<r.w*.5f+extra)return true;return false;}
        static float Seg(Vector3 p,Vector3 a,Vector3 b){Vector2 q=new Vector2(p.x,p.z),x=new Vector2(a.x,a.z),d=new Vector2(b.x-a.x,b.z-a.z);return d.sqrMagnitude<.001f?Vector2.Distance(q,x):Vector2.Distance(q,x+d*Mathf.Clamp01(Vector2.Dot(q-x,d)/d.sqrMagnitude));}
        static Vector3 Center(Transform t){if(t.childCount==0)return t.position;Vector3 c=Vector3.zero;int n=0;for(int i=0;i<t.childCount;i++)if(t.GetChild(i).name.Contains("House")||t.GetChild(i).name.StartsWith("Building")){c+=t.GetChild(i).position;n++;}return n>0?c/n:t.position;}
        static Terrain FindTerrain(){GameObject g=Find(TerrainName);Terrain t=g?(g.GetComponent<Terrain>()??g.GetComponentInChildren<Terrain>(true)):null;if(t)return t;Terrain[] a=UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);return a.Length>0?a[0]:null;}
        static GameObject Find(string n){GameObject g=GameObject.Find(n);if(g)return g;foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t&&t.name==n&&t.gameObject.scene.IsValid())return t.gameObject;return null;}
        static Transform FindChild(Transform r,string n){foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t.name==n)return t;return null;}
        static Transform DirectChild(Transform r,string n){for(int i=0;i<r.childCount;i++)if(r.GetChild(i).name==n)return r.GetChild(i);return null;}
        static Vector3 ClampToTerrain(Terrain t,Vector3 p,float margin){Vector3 o=t.transform.position,s=t.terrainData.size;p.x=Mathf.Clamp(p.x,o.x+margin,o.x+s.x-margin);p.z=Mathf.Clamp(p.z,o.z+margin,o.z+s.z-margin);p.y=Ground(t,p);return p;}
        static Transform New(string n,Transform p){GameObject g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
        static Bounds BoundsOf(GameObject g){bool set=false;Bounds b=new Bounds(g.transform.position,Vector3.zero);foreach(Renderer r in g.GetComponentsInChildren<Renderer>(true)){if(!set){b=r.bounds;set=true;}else b.Encapsulate(r.bounds);}foreach(Collider c in g.GetComponentsInChildren<Collider>(true)){if(!set){b=c.bounds;set=true;}else b.Encapsulate(c.bounds);}return b;}
        static int Count(Transform r,string n){int c=0;foreach(Transform t in r.GetComponentsInChildren<Transform>(true))if(t!=r&&t.name.Contains(n))c++;return c;}
        static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
        static float Dist(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return Mathf.Sqrt(x*x+z*z);}
        static float Fbm(float x,float y,int o){float v=0,a=1,sum=0,f=1;for(int i=0;i<o;i++){v+=Mathf.PerlinNoise(x*f,y*f)*a;sum+=a;a*=.5f;f*=2.03f;}return v/sum;}
        static float TileFbm(float u,float v,int seed){float total=0,sum=0,amp=1;for(int o=0;o<4;o++){float period=4*(1<<o),ox=seed*7.31f+o*19.7f,oy=seed*11.13f+o*31.9f,px=u*period+ox,py=v*period+oy;float a=Mathf.PerlinNoise(px,py),b=Mathf.PerlinNoise(px-period,py),cc=Mathf.PerlinNoise(px,py-period),d=Mathf.PerlinNoise(px-period,py-period);float n=Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(cc,d,u),v);total+=n*amp;sum+=amp;amp*=.5f;}return Mathf.Clamp01(total/sum);}
        static float Hash(int x,int y,int s){unchecked{uint h=(uint)(x*374761393+y*668265263+s*1274126177);h=(h^(h>>13))*1274126177u;return (h&0xffffff)/16777215f;}}
        static float Next(System.Random r,float a,float b)=>a+(float)r.NextDouble()*(b-a);
        static void Ensure(string path){string[] p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}}
    }
}
