using System.Collections.Generic;
using ArcaneCode.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcaneCode
{
    // The root stays on the gameplay XY plane. Only the presentation pivot moves.
    public sealed class WorldPropArt : MonoBehaviour
    {
        public Transform Pivot { get; private set; }
        public SpriteRenderer Shadow { get; private set; }
        public MeshRenderer[] Parts { get; private set; }
        float age, hover, bob, spin, tilt, heading;
        Color[] colors;
        MaterialPropertyBlock tint;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public void Initialize(Transform pivot,SpriteRenderer shadow,float height,float amplitude,float speed,float inclination)
        {
            tint=new MaterialPropertyBlock();
            Pivot=pivot; Shadow=shadow; hover=height; bob=amplitude; spin=speed; tilt=inclination;
            Parts=pivot.GetComponentsInChildren<MeshRenderer>(); colors=new Color[Parts.Length];
            for (int i=0;i<Parts.Length;i++) { Parts[i].GetPropertyBlock(tint); colors[i]=tint.GetColor(ColorId); }
            Animate(0);
        }
        public void Aim(Vector2 direction)
        { if (direction.sqrMagnitude>.001f) heading=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg; }
        public void Animate(float dt)
        {
            age+=Mathf.Max(0,dt);
            Pivot.localPosition=new Vector3(0,hover+Mathf.Sin(age*2.8f)*bob,0);
            Pivot.localRotation=Quaternion.Euler(tilt,Mathf.Repeat(age*spin,360),heading);
            int order=100-Mathf.RoundToInt(transform.position.y*15);
            if (Shadow!=null) Shadow.sortingOrder=order;
            foreach (MeshRenderer part in Parts) part.sortingOrder=order+1;
        }
        public void SetOrder(int order)
        {
            foreach (MeshRenderer part in Parts) part.sortingOrder=order;
            if (Shadow!=null) Shadow.sortingOrder=order-1;
        }
        public void SetOpacity(float opacity)
        {
            for (int i=0;i<Parts.Length;i++)
            {
                Color color=colors[i]; color.a*=Mathf.Clamp01(opacity);
                tint.Clear(); tint.SetColor(ColorId,color); Parts[i].SetPropertyBlock(tint);
            }
        }
    }

    // Cached low-poly meshes and one shared URP 2D-compatible material, without colliders.
    public static class WorldProps
    {
        static Mesh cube, sphere, ring;
        static Material material;
        static readonly int ColorId=Shader.PropertyToID("_BaseColor");
        static readonly Color Gold=new Color(1,.73f,.22f);

        static Transform Root(Transform parent,string name,Vector2 at)
        {
            var root=new GameObject(name).transform; root.SetParent(parent,false); root.localPosition=at;
            return root;
        }
        static Transform Pivot(Transform root)
        { var pivot=new GameObject("Modelo 3D").transform; pivot.SetParent(root,false); return pivot; }
        static SpriteRenderer Shadow(Transform root,float width)
        { return WorldArt.Draw(root,"disc",Vector2.zero,new Vector2(width,width*.3f),new Color(0,0,0,.38f),0,false); }
        static MeshRenderer Part(Transform pivot,string name,Mesh mesh,Vector3 position,Vector3 scale,Color color)
        {
            if (material==null)
            {
                Shader shader=Resources.Load<Shader>("WorldProp");
                if (shader==null) throw new System.InvalidOperationException("Shader Resources/WorldProp não encontrado.");
                material=new Material(shader) { name="Arcane Props 2.5D",hideFlags=HideFlags.HideAndDontSave };
            }
            Transform part=Root(pivot,name,Vector2.zero); part.localPosition=position; part.localScale=scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=part.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            var tint=new MaterialPropertyBlock(); tint.SetColor(ColorId,color); renderer.SetPropertyBlock(tint);
            return renderer;
        }
        static WorldPropArt Finish(Transform root,Transform pivot,SpriteRenderer shadow,float hover,float bob,float spin,float tilt=12)
        {
            var art=root.gameObject.AddComponent<WorldPropArt>(); art.Initialize(pivot,shadow,hover,bob,spin,tilt); return art;
        }
        public static WorldPropArt Chest(Transform parent,Vector2 at)
        {
            Transform root=Root(parent,"Baú 3D",at), pivot=Pivot(root);
            Part(pivot,"Madeira",Cube,Vector3.zero,new Vector3(1.05f,.55f,.55f),new Color(.47f,.25f,.09f));
            Part(pivot,"Tampa",Cube,new Vector3(0,.32f,0),new Vector3(1.12f,.16f,.61f),new Color(.65f,.35f,.12f));
            foreach (float x in new[] {-.37f,.37f})
                Part(pivot,"Faixa dourada",Cube,new Vector3(x,.03f,-.01f),new Vector3(.09f,.74f,.63f),Gold);
            Part(pivot,"Fechadura",Cube,new Vector3(0,.1f,-.32f),new Vector3(.18f,.2f,.07f),Gold);
            WorldPropArt art=Finish(root,pivot,Shadow(root,1.25f),.38f,0,0,22);
            pivot.localRotation=Quaternion.Euler(22,-24,0);
            return art;
        }
        public static WorldPropArt Item(Transform parent,Vector2 at,RunItemKind kind,bool ice=false)
        {
            Transform root=Root(parent,kind+" 3D",at), pivot=Pivot(root);
            Color magic=ice?new Color(.32f,.8f,1):new Color(.73f,.38f,1);
            if (kind==RunItemKind.Ring)
            {
                Part(pivot,"Anel",Ring,Vector3.zero,Vector3.one*.48f,Gold);
                Part(pivot,"Gema",Sphere,new Vector3(0,.22f,-.02f),Vector3.one*.17f,magic);
            }
            else if (kind==RunItemKind.Staff)
            {
                Part(pivot,"Cajado",Cube,new Vector3(0,-.06f,0),new Vector3(.09f,.85f,.09f),new Color(.58f,.33f,.14f));
                Part(pivot,"Ponta",Sphere,new Vector3(0,.4f,0),Vector3.one*.25f,ice?magic:new Color(1,.43f,.14f));
                Part(pivot,"Aro",Ring,new Vector3(0,.4f,0),Vector3.one*.32f,Gold);
            }
            else
            {
                Part(pivot,"Páginas",Cube,Vector3.zero,new Vector3(.42f,.58f,.13f),new Color(.94f,.85f,.66f));
                foreach (float z in new[] {-.09f,.09f})
                    Part(pivot,"Capa",Cube,new Vector3(0,0,z),new Vector3(.48f,.65f,.05f),magic);
                Part(pivot,"Lombada",Cube,new Vector3(-.23f,0,0),new Vector3(.07f,.65f,.22f),Gold);
                foreach (float z in new[] {-.13f,.13f})
                    Part(pivot,"Selo",Sphere,new Vector3(0,0,z),new Vector3(.18f,.18f,.05f),Gold);
            }
            return Finish(root,pivot,Shadow(root,.7f),.55f,.07f,65);
        }
        public static WorldPropArt Card(Transform parent,Vector2 at)
        {
            Transform root=Root(parent,"Carta 3D",at), pivot=Pivot(root);
            Part(pivot,"Carta",Cube,Vector3.zero,new Vector3(.52f,.75f,.08f),new Color(.17f,.65f,.55f));
            foreach (float z in new[] {-.055f,.055f})
            {
                Part(pivot,"Moldura",Cube,new Vector3(0,0,z),new Vector3(.43f,.65f,.02f),Gold);
                Part(pivot,"Face",Cube,new Vector3(0,0,z*1.3f),new Vector3(.36f,.57f,.02f),new Color(.1f,.25f,.3f));
                Part(pivot,"Cristal",Sphere,new Vector3(0,0,z*1.7f),new Vector3(.23f,.32f,.06f),new Color(.35f,1,.84f));
            }
            return Finish(root,pivot,Shadow(root,.75f),.6f,.07f,55);
        }
        public static WorldPropArt Resource(Transform parent,Vector2 at,bool coin)
        {
            Transform root=Root(parent,coin?"Moeda 3D":"Cristal XP 3D",at), pivot=Pivot(root);
            Part(pivot,"Recurso",coin?Ring:Sphere,Vector3.zero,new Vector3(.23f,.23f,coin?.09f:.23f),coin?Gold:new Color(.45f,1,.84f));
            if (coin) Part(pivot,"Centro",Sphere,Vector3.zero,new Vector3(.15f,.15f,.05f),Gold);
            return Finish(root,pivot,Shadow(root,.28f),.13f,.025f,100);
        }
        public static WorldPropArt Projectile(Transform parent,Vector2 at,float diameter,Color color,bool ice,Vector2 direction)
        {
            Transform root=Root(parent,"Projétil 3D",at), pivot=Pivot(root);
            Part(pivot,ice?"Cristal":"Chama",Sphere,Vector3.zero,new Vector3(diameter*(ice?1.35f:1),diameter,diameter),color);
            if (!ice) Part(pivot,"Rastro",Sphere,new Vector3(-diameter*.6f,0,0),new Vector3(diameter*1.2f,diameter*.45f,diameter*.45f),new Color(color.r,color.g*.65f,color.b,.65f));
            WorldPropArt art=Finish(root,pivot,null,0,0,ice?150:0,0); art.Aim(direction); art.Animate(0); art.SetOrder(350);
            return art;
        }
        public static WorldPropArt Burst(Transform parent,Vector2 at,Color color)
        {
            Transform root=Root(parent,"Onda mágica 3D",at), pivot=Pivot(root);
            Part(pivot,"Onda",Ring,Vector3.zero,new Vector3(1,1,.5f),color);
            WorldPropArt art=Finish(root,pivot,null,0,0,0,0); art.SetOrder(340); return art;
        }

        static Mesh Cube
        {
            get
            {
                if (cube!=null) return cube;
                var vertices=new List<Vector3>();
                Face(vertices,Vector3.forward,Vector3.right,Vector3.up);
                Face(vertices,Vector3.back,Vector3.left,Vector3.up);
                Face(vertices,Vector3.right,Vector3.back,Vector3.up);
                Face(vertices,Vector3.left,Vector3.forward,Vector3.up);
                Face(vertices,Vector3.up,Vector3.right,Vector3.back);
                Face(vertices,Vector3.down,Vector3.right,Vector3.forward);
                return cube=Mesh("Cube",vertices);
            }
        }
        static void Face(List<Vector3> vertices,Vector3 normal,Vector3 u,Vector3 v)
        {
            Vector3 center=normal*.5f, a=center-(u+v)*.5f, b=center+(u-v)*.5f, c=center+(u+v)*.5f, d=center+(-u+v)*.5f;
            Triangle(vertices,a,b,c); Triangle(vertices,a,c,d);
        }
        static Mesh Sphere
        {
            get
            {
                if (sphere!=null) return sphere;
                var vertices=new List<Vector3>();
                Vector3[] equator={Vector3.forward,Vector3.right,Vector3.back,Vector3.left};
                for (int i=0;i<4;i++)
                {
                    Subdivide(vertices,Vector3.up,equator[i],equator[(i+1)%4]);
                    Subdivide(vertices,Vector3.down,equator[(i+1)%4],equator[i]);
                }
                return sphere=Mesh("Low poly sphere",vertices);
            }
        }
        static void Subdivide(List<Vector3> vertices,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=(a+b).normalized,bc=(b+c).normalized,ca=(c+a).normalized;
            Triangle(vertices,a*.5f,ab*.5f,ca*.5f); Triangle(vertices,ab*.5f,b*.5f,bc*.5f);
            Triangle(vertices,ca*.5f,bc*.5f,c*.5f); Triangle(vertices,ab*.5f,bc*.5f,ca*.5f);
        }
        static Mesh Ring
        {
            get
            {
                if (ring!=null) return ring;
                var vertices=new List<Vector3>();
                for (int segment=0;segment<24;segment++) for (int side=0;side<8;side++)
                {
                    Vector3 a=TorusPoint(segment,side),b=TorusPoint(segment+1,side),c=TorusPoint(segment+1,side+1),d=TorusPoint(segment,side+1);
                    Triangle(vertices,a,b,c); Triangle(vertices,a,c,d);
                }
                return ring=Mesh("Torus",vertices);
            }
        }
        static Vector3 TorusPoint(int segment,int side)
        {
            float a=segment*Mathf.PI*2/24,b=side*Mathf.PI*2/8,r=.42f+.08f*Mathf.Cos(b);
            return new Vector3(r*Mathf.Cos(a),r*Mathf.Sin(a),.08f*Mathf.Sin(b));
        }
        static void Triangle(List<Vector3> vertices,Vector3 a,Vector3 b,Vector3 c)
        { vertices.Add(a); vertices.Add(b); vertices.Add(c); }
        static Mesh Mesh(string name,List<Vector3> vertices)
        {
            var mesh=new Mesh { name="Arcane "+name,hideFlags=HideFlags.HideAndDontSave };
            int[] indices=new int[vertices.Count]; for (int i=0;i<indices.Length;i++) indices[i]=i;
            mesh.SetVertices(vertices); mesh.triangles=indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
